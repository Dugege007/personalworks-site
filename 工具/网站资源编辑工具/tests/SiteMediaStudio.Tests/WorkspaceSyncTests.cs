using System.Globalization;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 同步状态检查：落后、已同步、无法检查、60 秒节流，以及落后时拒绝写入。
/// </summary>
public sealed class WorkspaceSyncTests
{
    /// <summary>
    /// 本机没有 Git 时跳过会真正执行命令的测试。
    /// </summary>
    public sealed class RequiresGitFactAttribute : FactAttribute
    {
        public RequiresGitFactAttribute()
        {
            var probe = new GitProcessRunner().Run(
                Path.GetTempPath(),
                new[] { "--version" },
                TimeSpan.FromSeconds(5));
            if (!probe.Succeeded)
            {
                Skip = "环境没有 Git，跳过真实仓库的同步检查。";
            }
        }
    }

    [Fact]
    public void Behind_ShowsNotice_AndRejectsWrites()
    {
        using var workspace = OpenWorkspace();
        var git = new ScriptedGit { Handle = Repo(workspace.Path, behind: 2) };
        var checker = Checker(git);

        var report = checker.Check(workspace.Path);

        Assert.Equal(WorkspaceSyncKind.Behind, report.Kind);
        Assert.Equal(WorkspaceSyncGuard.BehindNotice, report.Notice);
        Assert.False(WorkspaceSyncGuard.TryBeginWrite(report.Kind, out var reason));
        Assert.Equal(WorkspaceSyncGuard.BehindNotice, reason);
        Assert.Equal(1, git.Count("fetch", "--prune", "origin"));
    }

    [Fact]
    public void Current_AllowsWrites_WhenLocalIsNotBehind()
    {
        using var workspace = OpenWorkspace();
        var git = new ScriptedGit { Handle = Repo(workspace.Path, behind: 0) };
        var checker = Checker(git);

        var report = checker.Check(workspace.Path);

        Assert.Equal(WorkspaceSyncKind.Current, report.Kind);
        Assert.Equal("", report.Notice);
        Assert.True(WorkspaceSyncGuard.TryBeginWrite(report.Kind, out var reason));
        Assert.Equal("", reason);
    }

    [Fact]
    public void FetchFailure_IsUnknown_AndDoesNotBlockWrites()
    {
        using var workspace = OpenWorkspace();
        var git = new ScriptedGit { Handle = Repo(workspace.Path, behind: 3, fetchSucceeds: false) };
        var checker = Checker(git);

        var report = checker.Check(workspace.Path);

        Assert.Equal(WorkspaceSyncKind.Unknown, report.Kind);
        Assert.Equal(WorkspaceSyncGuard.UnknownNotice, report.Notice);
        Assert.True(WorkspaceSyncGuard.TryBeginWrite(report.Kind, out var reason));
        Assert.Equal("", reason);
    }

    [Fact]
    public void MissingRepository_RemoteOrBranch_IsUnknown()
    {
        using var workspace = OpenWorkspace();

        var notRepoGit = new ScriptedGit { Handle = _ => Fail("不是 git 仓库") };
        var notRepo = Checker(notRepoGit).Check(workspace.Path);
        Assert.Equal(WorkspaceSyncKind.Unknown, notRepo.Kind);
        Assert.Equal(WorkspaceSyncGuard.UnknownNotice, notRepo.Notice);
        Assert.True(WorkspaceSyncGuard.TryBeginWrite(notRepo.Kind, out _));

        var noRemoteGit = new ScriptedGit
        {
            Handle = Repo(workspace.Path, behind: 0, hasUpstream: false, remoteName: "")
        };
        var noRemote = Checker(noRemoteGit).Check(workspace.Path);
        Assert.Equal(WorkspaceSyncKind.Unknown, noRemote.Kind);
        Assert.Equal(0, noRemoteGit.Count("fetch", "--prune", "origin"));

        var detachedGit = new ScriptedGit
        {
            Handle = args => Args(args, "rev-parse", "--abbrev-ref", "HEAD")
                ? Ok("HEAD")
                : Repo(workspace.Path, behind: 0)(args)
        };
        var detached = Checker(detachedGit).Check(workspace.Path);
        Assert.Equal(WorkspaceSyncKind.Unknown, detached.Kind);
        Assert.Equal(0, detachedGit.Count("fetch", "--prune", "origin"));
        Assert.True(WorkspaceSyncGuard.TryBeginWrite(detached.Kind, out _));
    }

    [Fact]
    public void OriginBranch_IsUsedWhenUpstreamIsNotConfigured()
    {
        using var workspace = OpenWorkspace();
        var git = new ScriptedGit { Handle = Repo(workspace.Path, behind: 1, hasUpstream: false) };
        var checker = Checker(git);

        var report = checker.Check(workspace.Path);

        Assert.Equal(WorkspaceSyncKind.Behind, report.Kind);
        Assert.Equal(1, git.Count("fetch", "--prune", "origin"));
        Assert.Equal(1, git.Count("rev-list", "--count", "HEAD..origin/main"));
    }

    [Fact]
    public void RunnerException_BecomesUnknown_InsteadOfCrashing()
    {
        using var workspace = OpenWorkspace();
        var git = new ScriptedGit
        {
            Handle = _ => throw new InvalidOperationException("网络中断")
        };
        var checker = Checker(git);

        var report = checker.Check(workspace.Path);

        Assert.Equal(WorkspaceSyncKind.Unknown, report.Kind);
        Assert.Equal(WorkspaceSyncGuard.UnknownNotice, report.Notice);
        Assert.True(WorkspaceSyncGuard.TryBeginWrite(WorkspaceSyncKind.Unchecked, out var uncheckedReason));
        Assert.Equal("", uncheckedReason);
    }

    [Fact]
    public void SameWorkspace_FetchesAtMostOncePerMinute()
    {
        using var workspace = OpenWorkspace();
        var git = new ScriptedGit { Handle = Repo(workspace.Path, behind: 1) };
        var now = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);
        var checker = new WorkspaceSyncChecker(git, () => now);

        var first = checker.Check(workspace.Path);
        var callsAfterFirst = git.Calls.Count;
        Assert.Equal(WorkspaceSyncKind.Behind, first.Kind);

        Assert.Equal(first.Kind, checker.Check(workspace.Path).Kind);
        now = now.AddSeconds(59);
        Assert.Equal(WorkspaceSyncKind.Behind, checker.Check(workspace.Path).Kind);
        Assert.Equal(callsAfterFirst, git.Calls.Count);
        Assert.True(checker.IsFresh(workspace.Path));

        now = now.AddSeconds(1);
        git.Handle = Repo(workspace.Path, behind: 0);
        var synced = checker.Check(workspace.Path);

        Assert.Equal(WorkspaceSyncKind.Current, synced.Kind);
        Assert.True(WorkspaceSyncGuard.TryBeginWrite(synced.Kind, out var reason));
        Assert.Equal("", reason);
        Assert.Equal(2, git.Count("fetch", "--prune", "origin"));
        Assert.Equal(synced.Notice, checker.Peek(workspace.Path).Notice);
    }

    [Fact]
    public void NestedStageDirectory_ReusesTheRepositoryResult()
    {
        using var workspace = OpenWorkspace();
        var stage = Path.Combine(workspace.Path, "作品中转站");
        Directory.CreateDirectory(stage);
        var git = new ScriptedGit { Handle = Repo(workspace.Path, behind: 1) };
        var checker = Checker(git);

        var fromRoot = checker.Check(workspace.Path);
        var calls = git.Calls.Count;
        var fromStage = checker.Check(stage);

        Assert.Equal(WorkspaceSyncKind.Behind, fromRoot.Kind);
        Assert.Equal(WorkspaceSyncKind.Behind, fromStage.Kind);
        Assert.Equal(WorkspaceSyncGuard.BehindNotice, fromStage.Notice);
        Assert.Equal(calls, git.Calls.Count);
        Assert.False(WorkspaceSyncGuard.TryBeginWrite(fromStage.Kind, out _));
    }

    [Fact]
    public async Task InFlightCheck_DoesNotStartASecondFetch()
    {
        using var workspace = OpenWorkspace();
        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        var fetches = 0;
        var git = new ScriptedGit
        {
            Handle = args =>
            {
                if (Args(args, "fetch", "--prune", "origin"))
                {
                    Interlocked.Increment(ref fetches);
                    entered.Set();
                    Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
                }

                return Repo(workspace.Path, behind: 1)(args);
            }
        };
        var checker = Checker(git);

        var first = Task.Run(() => checker.Check(workspace.Path));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => checker.Check(workspace.Path));
        release.Set();
        var firstReport = await first;
        var secondReport = await second;

        Assert.Equal(1, fetches);
        Assert.Equal(WorkspaceSyncKind.Behind, firstReport.Kind);
        Assert.Equal(WorkspaceSyncKind.Behind, secondReport.Kind);
        Assert.False(WorkspaceSyncGuard.TryBeginWrite(checker.Peek(workspace.Path).Kind, out _));
    }

    [RequiresGitFact]
    public void RealRepository_SubdirectoryBehind_ThenSynced()
    {
        var root = Directory.CreateTempSubdirectory("workspace-sync-git-").FullName;
        try
        {
            var origin = Path.Combine(root, "origin");
            var clone = Path.Combine(root, "clone");
            var stage = Path.Combine(clone, "作品中转站");
            Directory.CreateDirectory(origin);
            var git = new GitProcessRunner();

            Assert.True(Git(git, origin, "init", "-b", "main").Succeeded || InitDefaultBranch(git, origin));
            Assert.True(Git(git, origin, "config", "user.email", "sync-test@example.com").Succeeded);
            Assert.True(Git(git, origin, "config", "user.name", "SyncTest").Succeeded);
            Assert.True(Git(git, origin, "config", "commit.gpgsign", "false").Succeeded);
            File.WriteAllText(Path.Combine(origin, "readme.txt"), "初始");
            Assert.True(Git(git, origin, "add", "readme.txt").Succeeded);
            Assert.True(Git(git, origin, "commit", "-m", "初始").Succeeded);
            Assert.True(Git(git, root, "clone", origin, clone).Succeeded);

            File.WriteAllText(Path.Combine(origin, "readme.txt"), "远端更新");
            Assert.True(Git(git, origin, "add", "readme.txt").Succeeded);
            Assert.True(Git(git, origin, "commit", "-m", "远端更新").Succeeded);
            Directory.CreateDirectory(stage);

            var now = new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);
            var checker = new WorkspaceSyncChecker(git, () => now);
            var behind = checker.Check(stage);
            Assert.Equal(WorkspaceSyncKind.Behind, behind.Kind);
            Assert.Equal(WorkspaceSyncGuard.BehindNotice, behind.Notice);
            Assert.False(WorkspaceSyncGuard.TryBeginWrite(behind.Kind, out _));
            Assert.True(checker.IsFresh(clone));

            Assert.True(Git(git, clone, "pull", "--ff-only").Succeeded);
            now = now.AddSeconds(60);
            var current = checker.Check(stage);
            Assert.Equal(WorkspaceSyncKind.Current, current.Kind);
            Assert.Equal("", current.Notice);
            Assert.True(WorkspaceSyncGuard.TryBeginWrite(current.Kind, out var reason));
            Assert.Equal("", reason);

            var loose = Path.Combine(root, "loose");
            Directory.CreateDirectory(loose);
            var unknown = checker.Check(loose);
            Assert.Equal(WorkspaceSyncKind.Unknown, unknown.Kind);
            Assert.Equal(WorkspaceSyncGuard.UnknownNotice, unknown.Notice);
            Assert.True(WorkspaceSyncGuard.TryBeginWrite(unknown.Kind, out _));
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>
    /// 构造检查器。只查一次的用例不需要推动时钟。
    /// </summary>
    private static WorkspaceSyncChecker Checker(IGitCommandRunner git)
    {
        return new WorkspaceSyncChecker(git, () => new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// 准备一个空的临时目录。
    /// </summary>
    private static TempWorkspace OpenWorkspace()
    {
        return new TempWorkspace();
    }

    /// <summary>
    /// 模拟一个已有 origin/main 的仓库。behind 是本地落后的提交数。
    /// </summary>
    private static Func<IReadOnlyList<string>, GitCommandResult> Repo(
        string root,
        int behind,
        bool fetchSucceeds = true,
        bool hasUpstream = true,
        string remoteName = "origin",
        string branch = "main")
    {
        return args =>
        {
            if (Args(args, "rev-parse", "--show-toplevel"))
            {
                return Ok(root);
            }

            if (Args(args, "rev-parse", "--abbrev-ref", "HEAD"))
            {
                return Ok(branch);
            }

            if (Args(args, "rev-parse", "--abbrev-ref", "@{upstream}"))
            {
                return hasUpstream ? Ok(remoteName + "/" + branch) : Fail("没有上游");
            }

            if (Args(args, "remote"))
            {
                return remoteName.Length == 0 ? Ok("") : Ok(remoteName + "\n");
            }

            if (args.Count >= 3 && args[0] == "fetch" && args[1] == "--prune")
            {
                return fetchSucceeds ? Ok("") : new GitCommandResult();
            }

            if (Args(args, "rev-parse", "--verify", "--quiet", remoteName + "/" + branch))
            {
                return Ok("abc");
            }

            if (Args(args, "rev-list", "--count", "HEAD.." + remoteName + "/" + branch))
            {
                return Ok(behind.ToString(CultureInfo.InvariantCulture));
            }

            return Fail("未模拟的命令：" + string.Join(' ', args));
        };
    }

    /// <summary>
    /// 参数是否完全一致。
    /// </summary>
    private static bool Args(IReadOnlyList<string> args, params string[] expected)
    {
        return args.SequenceEqual(expected);
    }

    /// <summary>
    /// 成功的 git 输出。
    /// </summary>
    private static GitCommandResult Ok(string stdout)
    {
        return new GitCommandResult
        {
            Completed = true,
            ExitCode = 0,
            StandardOutput = stdout + "\n"
        };
    }

    /// <summary>
    /// 失败的 git 输出。
    /// </summary>
    private static GitCommandResult Fail(string stderr)
    {
        return new GitCommandResult
        {
            Completed = true,
            ExitCode = 1,
            StandardError = stderr
        };
    }

    /// <summary>
    /// 执行一条真实 git 命令。
    /// </summary>
    private static GitCommandResult Git(GitProcessRunner git, string workingDirectory, params string[] arguments)
    {
        return git.Run(workingDirectory, arguments, TimeSpan.FromSeconds(20));
    }

    /// <summary>
    /// 旧版本 git 不支持 init -b 时，初始化后再建 main。
    /// </summary>
    private static bool InitDefaultBranch(GitProcessRunner git, string origin)
    {
        return Git(git, origin, "init").Succeeded
            && Git(git, origin, "checkout", "-b", "main").Succeeded;
    }

    /// <summary>
    /// 删除临时仓库。个别只读文件删不掉时不让测试失败。
    /// </summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 不访问网络的 git 替身。
    /// </summary>
    private sealed class ScriptedGit : IGitCommandRunner
    {
        private readonly List<string[]> mCallList = new();

        /// <summary>
        /// 按参数返回结果。测试会替换它。
        /// </summary>
        public Func<IReadOnlyList<string>, GitCommandResult> Handle { get; set; } =
            _ => new GitCommandResult();

        /// <summary>
        /// 已经执行过的参数。
        /// </summary>
        public IReadOnlyList<string[]> Calls
        {
            get
            {
                lock (mCallList)
                {
                    return mCallList.ToList();
                }
            }
        }

        /// <summary>
        /// 统计完全匹配的调用次数。
        /// </summary>
        public int Count(params string[] arguments)
        {
            return Calls.Count(call => call.SequenceEqual(arguments));
        }

        /// <summary>
        /// 记录参数并交给脚本。
        /// </summary>
        public GitCommandResult Run(string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            _ = workingDirectory;
            _ = timeout;
            lock (mCallList)
            {
                mCallList.Add(arguments.ToArray());
            }

            return Handle(arguments);
        }
    }

    /// <summary>
    /// 一个空的临时目录。
    /// </summary>
    private sealed class TempWorkspace : IDisposable
    {
        private readonly string mPath = Directory.CreateTempSubdirectory("workspace-sync-").FullName;

        public string Path => mPath;

        public void Dispose()
        {
            TryDelete(mPath);
        }
    }
}
