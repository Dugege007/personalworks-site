using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 后台比较本地分支和 GitHub 上的对应分支。同一工作区 60 秒内最多执行一次 fetch。
/// 调用 <see cref="Check"/> 的线程会等待 git，界面线程只应调用 <see cref="Peek"/> 和 <see cref="IsFresh"/>。
/// </summary>
public sealed class WorkspaceSyncChecker
{
    /// <summary>
    /// 同一工作区两次 fetch 之间的最短间隔。
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// git fetch 的默认超时。超时后按无法检查处理，不把界面留在等待里。
    /// </summary>
    public static readonly TimeSpan DefaultFetchTimeout = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan mCommandTimeout = TimeSpan.FromSeconds(15);
    private readonly IGitCommandRunner mGit;
    private readonly Func<DateTimeOffset> mUtcNow;
    private readonly TimeSpan mInterval;
    private readonly TimeSpan mFetchTimeout;
    private readonly object mGate = new();
    private string mWorkspaceKey = "";
    private string mRepositoryKey = "";
    private DateTimeOffset mCheckedAt;
    private bool mHasResult;
    private bool mRunning;
    private string mRunningKey = "";
    private WorkspaceSyncReport mReport = WorkspaceSyncReport.Unchecked;

    /// <summary>
    /// 使用本机 git 和系统时钟。
    /// </summary>
    public WorkspaceSyncChecker(IGitCommandRunner git)
        : this(git, () => DateTimeOffset.UtcNow, DefaultInterval, DefaultFetchTimeout)
    {
    }

    /// <summary>
    /// 指定时钟和间隔，便于测试节流。git 替身负责模拟 fetch 和比较结果。
    /// </summary>
    public WorkspaceSyncChecker(
        IGitCommandRunner git,
        Func<DateTimeOffset> utcNow,
        TimeSpan? interval = null,
        TimeSpan? fetchTimeout = null)
    {
        mGit = git ?? throw new ArgumentNullException(nameof(git));
        mUtcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        mInterval = interval ?? DefaultInterval;
        mFetchTimeout = fetchTimeout ?? DefaultFetchTimeout;
    }

    /// <summary>
    /// 最近一次已完成的结果。路径不属于这次结果对应的工作区时返回尚未检查。
    /// 不等待正在进行的 git。
    /// </summary>
    public WorkspaceSyncReport Peek(string workspacePath)
    {
        var key = TryNormalize(workspacePath);
        lock (mGate)
        {
            if (!mHasResult || key.Length == 0 || !IsSameWorkspace(key))
            {
                return WorkspaceSyncReport.Unchecked;
            }

            return mReport;
        }
    }

    /// <summary>
    /// 这个路径在节流窗口内是否已经有结果。为真时不应再启动 fetch。
    /// </summary>
    public bool IsFresh(string workspacePath)
    {
        var key = TryNormalize(workspacePath);
        lock (mGate)
        {
            return key.Length > 0 && IsFreshUnlocked(key);
        }
    }

    /// <summary>
    /// 距离可以再次 fetch 还要多久。已经过期或从未检查时为零。
    /// </summary>
    public TimeSpan TimeUntilStale(string workspacePath)
    {
        var key = TryNormalize(workspacePath);
        lock (mGate)
        {
            if (key.Length == 0 || !IsFreshUnlocked(key))
            {
                return TimeSpan.Zero;
            }

            var left = mInterval - (mUtcNow() - mCheckedAt);
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// 执行或复用一次检查。60 秒内的重复调用直接返回上次结果，不再 fetch。
    /// 其他工作区正在检查时，本方法会在当前线程等到那次结束再继续，因此不要在界面线程调用。
    /// </summary>
    public WorkspaceSyncReport Check(string workspacePath)
    {
        var key = TryNormalize(workspacePath);
        if (key.Length == 0)
        {
            return WorkspaceSyncReport.Unknown(mUtcNow());
        }

        while (true)
        {
            var iAmRunner = false;
            var waitForCurrent = false;
            lock (mGate)
            {
                if (IsFreshUnlocked(key))
                {
                    return mReport;
                }

                if (!mRunning)
                {
                    mRunning = true;
                    mRunningKey = key;
                    iAmRunner = true;
                }
                else if (IsSameWorkspace(key) || KeyEquals(mRunningKey, key))
                {
                    // 同一工作区已经在检查。等它结束再读结果，避免第二个人拿到「尚未检查」把结论盖掉。
                    waitForCurrent = true;
                }
            }

            if (waitForCurrent)
            {
                WaitUntilIdle();
                continue;
            }

            if (!iAmRunner)
            {
                WaitUntilIdle();
                continue;
            }

            var checkedAt = mUtcNow();
            WorkspaceSyncReport report;
            try
            {
                report = Probe(workspacePath, checkedAt);
            }
            catch (Exception)
            {
                report = WorkspaceSyncReport.Unknown(checkedAt);
            }

            lock (mGate)
            {
                try
                {
                    if (KeyEquals(mRunningKey, key))
                    {
                        mReport = report;
                        mHasResult = true;
                        mWorkspaceKey = key;
                        mRepositoryKey = NormalizeRepository(report.RepositoryRoot);
                        mCheckedAt = checkedAt;
                    }
                }
                finally
                {
                    mRunning = false;
                    Monitor.PulseAll(mGate);
                }
            }

            return report;
        }
    }

    /// <summary>
    /// 比较本地分支和远程跟踪分支。任何失败都收成无法检查。
    /// </summary>
    private WorkspaceSyncReport Probe(string workspacePath, DateTimeOffset checkedAt)
    {
        if (!Directory.Exists(workspacePath))
        {
            return WorkspaceSyncReport.Unknown(checkedAt);
        }

        var top = mGit.Run(workspacePath, new[] { "rev-parse", "--show-toplevel" }, mCommandTimeout);
        if (!top.Succeeded)
        {
            return WorkspaceSyncReport.Unknown(checkedAt);
        }

        var repository = NormalizeRepository(FirstLine(top.StandardOutput));
        if (repository.Length == 0)
        {
            return WorkspaceSyncReport.Unknown(checkedAt);
        }

        var gitDirectory = Directory.Exists(repository) ? repository : workspacePath;
        var head = mGit.Run(gitDirectory, new[] { "rev-parse", "--abbrev-ref", "HEAD" }, mCommandTimeout);
        if (!head.Succeeded)
        {
            return WorkspaceSyncReport.Unknown(checkedAt, repository);
        }

        var branch = FirstLine(head.StandardOutput);
        if (branch.Length == 0 || string.Equals(branch, "HEAD", StringComparison.Ordinal))
        {
            return WorkspaceSyncReport.Unknown(checkedAt, repository);
        }

        var upstream = mGit.Run(
            gitDirectory,
            new[] { "rev-parse", "--abbrev-ref", "@{upstream}" },
            mCommandTimeout);
        string remote;
        string upstreamRef;
        if (upstream.Succeeded)
        {
            upstreamRef = FirstLine(upstream.StandardOutput);
            var slash = upstreamRef.IndexOf('/');
            if (slash <= 0 || slash >= upstreamRef.Length - 1)
            {
                return WorkspaceSyncReport.Unknown(checkedAt, repository);
            }

            remote = upstreamRef[..slash];
        }
        else
        {
            var remotes = mGit.Run(gitDirectory, new[] { "remote" }, mCommandTimeout);
            if (!remotes.Succeeded || !HasRemote(remotes.StandardOutput, "origin"))
            {
                return WorkspaceSyncReport.Unknown(checkedAt, repository);
            }

            remote = "origin";
            upstreamRef = "origin/" + branch;
        }

        var fetch = mGit.Run(gitDirectory, new[] { "fetch", "--prune", remote }, mFetchTimeout);
        if (!fetch.Succeeded)
        {
            return WorkspaceSyncReport.Unknown(checkedAt, repository);
        }

        if (!upstream.Succeeded)
        {
            var verify = mGit.Run(
                gitDirectory,
                new[] { "rev-parse", "--verify", "--quiet", upstreamRef },
                mCommandTimeout);
            if (!verify.Succeeded)
            {
                return WorkspaceSyncReport.Unknown(checkedAt, repository);
            }
        }

        var behind = mGit.Run(
            gitDirectory,
            new[] { "rev-list", "--count", "HEAD.." + upstreamRef },
            mCommandTimeout);
        if (!behind.Succeeded
            || !int.TryParse(FirstLine(behind.StandardOutput), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return WorkspaceSyncReport.Unknown(checkedAt, repository);
        }

        if (count > 0)
        {
            return WorkspaceSyncReport.Behind(checkedAt, repository);
        }

        return WorkspaceSyncReport.Current(checkedAt, repository);
    }

    /// <summary>
    /// 等到另一次检查放开锁。只应由 <see cref="Check"/> 在后台线程调用。
    /// </summary>
    private void WaitUntilIdle()
    {
        lock (mGate)
        {
            while (mRunning)
            {
                Monitor.Wait(mGate);
            }
        }
    }

    /// <summary>
    /// 调用方已持有锁。窗口内且路径属于同一工作区或同一仓库时为真。
    /// </summary>
    private bool IsFreshUnlocked(string key)
    {
        if (!mHasResult)
        {
            return false;
        }

        if (mUtcNow() - mCheckedAt >= mInterval)
        {
            return false;
        }

        return IsSameWorkspace(key);
    }

    /// <summary>
    /// 路径与上次检查的工作区相同，或者落在上次定位到的仓库根里面。
    /// </summary>
    private bool IsSameWorkspace(string key)
    {
        if (KeyEquals(key, mWorkspaceKey))
        {
            return true;
        }

        return mRepositoryKey.Length > 0 && IsSameOrChild(key, mRepositoryKey);
    }

    /// <summary>
    /// 把仓库根收成绝对路径。git 没有给出路径时返回空字符串。
    /// </summary>
    private static string NormalizeRepository(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return "";
        }

        return TryNormalize(repositoryRoot.Trim());
    }

    /// <summary>
    /// 收成绝对路径并去掉末尾分隔符。无法解析时返回空字符串。
    /// </summary>
    private static string TryNormalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return "";
        }
    }

    /// <summary>
    /// 路径相同，或者 path 在 root 下面。用来把投放箱子目录和仓库根看成同一个工作区。
    /// </summary>
    private static bool IsSameOrChild(string path, string root)
    {
        if (KeyEquals(path, root))
        {
            return true;
        }

        var prefix = root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, KeyComparison);
    }

    /// <summary>
    /// 路径是否指向同一位置。Windows 不区分大小写。
    /// </summary>
    private static bool KeyEquals(string left, string right)
    {
        return string.Equals(left, right, KeyComparison);
    }

    /// <summary>
    /// 远程列表里是否有指定名称。
    /// </summary>
    private static bool HasRemote(string text, string name)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var raw in normalized.Split('\n'))
        {
            if (string.Equals(raw.Trim(), name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 取第一行非空文本。
    /// </summary>
    private static string FirstLine(string text)
    {
        var normalized = (text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length > 0)
            {
                return line;
            }
        }

        return "";
    }

    /// <summary>
    /// 工作区路径比较方式。
    /// </summary>
    private static StringComparison KeyComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
