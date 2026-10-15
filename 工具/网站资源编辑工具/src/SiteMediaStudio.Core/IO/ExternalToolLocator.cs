using System.Diagnostics;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 外部程序的查找结果。找不到文件时不抛异常，用 <see cref="DisplayText"/> 说明「未找到」。
/// </summary>
public sealed class ExternalToolStatus
{
    /// <summary>
    /// 设置窗口在找不到程序时显示的文字。
    /// </summary>
    public const string NotFoundText = "未找到";

    /// <summary>
    /// 文件已经找到，但版本探测超时或没有读到输出版本时显示的文字。
    /// </summary>
    public const string VersionUnreadText = "未能读取版本";

    /// <summary>
    /// 是否找到了可执行文件。
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// 找到的可执行文件路径。没找到时为空。
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// 程序输出的版本原文。没读到时为空。
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 版本探测是否因为超过时限被中止。
    /// </summary>
    public bool VersionTimedOut { get; init; }

    /// <summary>
    /// 设置窗口直接显示的文字：版本原文、「未能读取版本」或「未找到」。
    /// </summary>
    public string DisplayText { get; init; } = NotFoundText;

    /// <summary>
    /// 三级查找都没有命中。
    /// </summary>
    public static ExternalToolStatus NotFound()
    {
        return new ExternalToolStatus
        {
            Found = false,
            DisplayText = NotFoundText
        };
    }

    /// <summary>
    /// 找到了文件，并读到了一行版本。
    /// </summary>
    public static ExternalToolStatus WithVersion(string filePath, string version)
    {
        return new ExternalToolStatus
        {
            Found = true,
            FilePath = filePath,
            Version = version,
            DisplayText = version
        };
    }

    /// <summary>
    /// 找到了文件，但没有读到版本。
    /// </summary>
    public static ExternalToolStatus Unreadable(string filePath)
    {
        return new ExternalToolStatus
        {
            Found = true,
            FilePath = filePath,
            DisplayText = VersionUnreadText
        };
    }

    /// <summary>
    /// 找到了文件，版本探测超时后已中止程序。
    /// </summary>
    public static ExternalToolStatus TimedOut(string filePath)
    {
        return new ExternalToolStatus
        {
            Found = true,
            FilePath = filePath,
            VersionTimedOut = true,
            DisplayText = VersionUnreadText
        };
    }
}

/// <summary>
/// 一次外部程序查找。路径环境变量为空字符串时不搜索系统 PATH；为 null 时使用当前进程的 PATH。
/// </summary>
public sealed class ExternalToolRequest
{
    /// <summary>
    /// 本机设置里填写的路径。空或文件不存在时继续往下找。
    /// </summary>
    public string? ConfiguredPath { get; init; }

    /// <summary>
    /// 常见安装位置，按顺序使用第一个存在的文件。
    /// </summary>
    public IReadOnlyList<string> CommonPathList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 在 PATH 中查找的文件名。先把整个 PATH 找遍第一个名字，再找下一个名字。
    /// </summary>
    public IReadOnlyList<string> FileNameList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 用来搜索的 PATH 文本。null 表示读取当前进程的环境变量。
    /// </summary>
    public string? PathEnvironment { get; init; }

    /// <summary>
    /// 找到文件后是否启动它来读取版本。只解析路径时设为 false，避免把现有 FFmpeg 查找变成会启动程序的操作。
    /// </summary>
    public bool ProbeVersion { get; init; } = true;

    /// <summary>
    /// 传给程序的版本参数。pngquant、oxipng 和 gifsicle 都使用 --version。
    /// </summary>
    public IReadOnlyList<string> VersionArgumentList { get; init; } = new[] { "--version" };

    /// <summary>
    /// 版本探测的最长等待毫秒数。小于等于 0 时改用默认值，避免一直等下去。
    /// </summary>
    public int VersionTimeoutMs { get; init; } = ExternalToolLocator.DefaultVersionTimeoutMs;
}

/// <summary>
/// 按「本机设置路径、常见安装位置、系统 PATH」查找外部程序，并在时限内探测版本。
/// </summary>
public static class ExternalToolLocator
{
    /// <summary>
    /// 版本探测的默认时限。程序卡住时到点就中止，不把界面留在等待里。
    /// </summary>
    public const int DefaultVersionTimeoutMs = 3_000;

    /// <summary>
    /// 版本行最长保留的字符数，避免把整段帮助文本塞进设置窗口。
    /// </summary>
    private const int MaxVersionLength = 240;

    /// <summary>
    /// FFmpeg 的常见安装位置。抽帧和压码共用这一份，不再各写一遍。
    /// </summary>
    public static readonly string[] FfmpegCommonPathList =
    {
        @"C:\ffmpeg\bin\ffmpeg.exe",
        @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
        @"C:\Program Files\MediaCoder\ffmpeg.exe",
        @"C:\Program Files\MediaCoder\tools\ffmpeg.exe",
        @"C:\Program Files\MediaCoder\tools64\ffmpeg.exe"
    };

    /// <summary>
    /// 在 PATH 中查找 FFmpeg 时使用的文件名。先找带 .exe 的名字。
    /// </summary>
    public static readonly string[] FfmpegFileNameList = { "ffmpeg.exe", "ffmpeg" };

    /// <summary>
    /// 查找 pngquant。
    /// </summary>
    public static ExternalToolStatus LocatePngquant(
        string? configuredPath,
        string? pathEnvironment = null,
        int versionTimeoutMs = DefaultVersionTimeoutMs)
    {
        return LocateNamed("pngquant", configuredPath, pathEnvironment, versionTimeoutMs);
    }

    /// <summary>
    /// 查找 oxipng。
    /// </summary>
    public static ExternalToolStatus LocateOxipng(
        string? configuredPath,
        string? pathEnvironment = null,
        int versionTimeoutMs = DefaultVersionTimeoutMs)
    {
        return LocateNamed("oxipng", configuredPath, pathEnvironment, versionTimeoutMs);
    }

    /// <summary>
    /// 查找 gifsicle。
    /// </summary>
    public static ExternalToolStatus LocateGifsicle(
        string? configuredPath,
        string? pathEnvironment = null,
        int versionTimeoutMs = DefaultVersionTimeoutMs)
    {
        return LocateNamed("gifsicle", configuredPath, pathEnvironment, versionTimeoutMs);
    }

    /// <summary>
    /// 按请求查找。找不到时返回「未找到」，不抛异常。
    /// </summary>
    public static ExternalToolStatus Locate(ExternalToolRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var filePath = ResolvePath(
            request.ConfiguredPath,
            request.CommonPathList,
            request.FileNameList,
            request.PathEnvironment);
        if (filePath == null)
        {
            return ExternalToolStatus.NotFound();
        }

        if (!request.ProbeVersion)
        {
            return ExternalToolStatus.Unreadable(filePath);
        }

        return ProbeVersion(filePath, request);
    }

    /// <summary>
    /// 只解析路径，不启动程序。顺序与 <see cref="Locate"/> 相同。
    /// </summary>
    public static string? ResolvePath(
        string? configuredPath,
        IReadOnlyList<string>? commonPathList,
        IReadOnlyList<string>? fileNameList,
        string? pathEnvironment = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        if (commonPathList != null)
        {
            foreach (var candidate in commonPathList)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return FindOnPath(fileNameList, pathEnvironment);
    }

    #region 查找

    /// <summary>
    /// 用同一个安装位置规则查找三个压缩程序之一。
    /// </summary>
    private static ExternalToolStatus LocateNamed(
        string fileName,
        string? configuredPath,
        string? pathEnvironment,
        int versionTimeoutMs)
    {
        return Locate(new ExternalToolRequest
        {
            ConfiguredPath = configuredPath,
            CommonPathList = CommonInstallPathList(fileName),
            FileNameList = new[] { fileName + ".exe", fileName },
            PathEnvironment = pathEnvironment,
            VersionArgumentList = new[] { "--version" },
            VersionTimeoutMs = versionTimeoutMs
        });
    }

    /// <summary>
    /// Scoop、winget、Chocolatey 和 Program Files 下的常见安装位置。
    /// </summary>
    private static IReadOnlyList<string> CommonInstallPathList(string fileName)
    {
        var pathList = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            pathList.Add(Path.Combine(userProfile, "scoop", "shims", fileName + ".exe"));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            pathList.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links", fileName + ".exe"));
        }

        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(commonAppData))
        {
            pathList.Add(Path.Combine(commonAppData, "chocolatey", "bin", fileName + ".exe"));
        }

        pathList.Add(Path.Combine(@"C:\Program Files", fileName, fileName + ".exe"));
        pathList.Add(Path.Combine(@"C:\Program Files (x86)", fileName, fileName + ".exe"));
        return pathList;
    }

    /// <summary>
    /// 在 PATH 的每个目录里查找。一个文件名找完所有目录后，才找下一个文件名。
    /// </summary>
    private static string? FindOnPath(IReadOnlyList<string>? fileNameList, string? pathEnvironment)
    {
        if (fileNameList == null || fileNameList.Count == 0)
        {
            return null;
        }

        var pathEnv = pathEnvironment ?? Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        var dirList = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var fileName in fileNameList)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            foreach (var dir in dirList)
            {
                var trimmed = dir.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var candidate = Path.Combine(trimmed, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    #endregion

    #region 版本探测

    /// <summary>
    /// 启动程序读取版本。超时后结束进程并返回，不把调用方留在等待里。
    /// </summary>
    private static ExternalToolStatus ProbeVersion(string filePath, ExternalToolRequest request)
    {
        var timeoutMs = request.VersionTimeoutMs > 0
            ? request.VersionTimeoutMs
            : DefaultVersionTimeoutMs;
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in request.VersionArgumentList ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(argument))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return ExternalToolStatus.Unreadable(filePath);
            }

            // 先开始读管道，再等待退出。否则输出把管道写满时，进程和等待会互相卡住。
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                TryKill(process);
                Observe(stdoutTask);
                Observe(stderrTask);
                return ExternalToolStatus.TimedOut(filePath);
            }

            process.WaitForExit();
            var version = FirstLine(ReadCompleted(stdoutTask));
            if (version.Length == 0)
            {
                version = FirstLine(ReadCompleted(stderrTask));
            }

            if (version.Length == 0)
            {
                return ExternalToolStatus.Unreadable(filePath);
            }

            return ExternalToolStatus.WithVersion(filePath, version);
        }
        catch (Exception)
        {
            // 文件存在但无法启动时，仍然报告已找到，避免把启动失败冒充成「未找到」或抛到界面上。
            return ExternalToolStatus.Unreadable(filePath);
        }
    }

    /// <summary>
    /// 读取已经结束的输出。读超时或失败时当作没有输出，不把异常抛出查找流程。
    /// </summary>
    private static string ReadCompleted(Task<string> readTask)
    {
        try
        {
            if (!readTask.Wait(1_000))
            {
                Observe(readTask);
                return "";
            }

            return readTask.Result ?? "";
        }
        catch (AggregateException)
        {
            return "";
        }
    }

    /// <summary>
    /// 超时后不再等待输出，但要观察任务异常，避免进程结束后抛出未观察异常。
    /// </summary>
    private static void Observe(Task readTask)
    {
        _ = readTask.ContinueWith(
            finished =>
            {
                _ = finished.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 结束卡住的程序及其子进程。进程已经退出时忽略。
    /// </summary>
    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        try
        {
            process.WaitForExit(500);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    /// <summary>
    /// 取第一行非空文本，并限制长度。
    /// </summary>
    private static string FirstLine(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            return line.Length <= MaxVersionLength ? line : line[..MaxVersionLength];
        }

        return "";
    }

    #endregion
}
