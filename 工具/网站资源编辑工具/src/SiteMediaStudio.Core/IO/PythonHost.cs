using System.Diagnostics;
using System.Text;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次 Python 子进程的退出结果。
/// </summary>
public sealed class PythonRunResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public bool Ok => ExitCode == 0;
}

/// <summary>
/// 调用本机 Python；优先使用已含 Pillow 的工具虚拟环境。
/// </summary>
public static class PythonHost
{
    private static readonly object Gate = new();
    private static string? ResolvedFileName;
    private static IReadOnlyList<string>? ResolvedPrefixList;
    private static bool Resolved;

    /// <summary>
    /// 是否已找到能 <c>import PIL</c> 的解释器。
    /// </summary>
    public static bool CanRunPrepare(string? startDir = null)
    {
        return TryResolve(startDir, out _, out _);
    }

    /// <summary>
    /// 运行脚本；超时或启动失败时抛出。标准输出按行走 <paramref name="onOutputLine"/>。
    /// </summary>
    public static PythonRunResult Run(
        string scriptPath,
        IReadOnlyList<string> argumentList,
        string workingDirectory,
        int timeoutMs = 180_000,
        Action<string>? onOutputLine = null)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("找不到 Python 脚本。", scriptPath);
        }

        if (!TryResolve(workingDirectory, out var fileName, out var prefixList))
        {
            throw new InvalidOperationException("找不到可用的 Python（须已安装 Pillow）。");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var prefix in prefixList)
        {
            startInfo.ArgumentList.Add(prefix);
        }

        startInfo.ArgumentList.Add("-u");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";
        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var outBuilder = new StringBuilder();
        var errBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            outBuilder.AppendLine(e.Data);
            onOutputLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            errBuilder.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 Python。");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("系统 PATH 中找不到 Python。", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException("Python 脚本超时：" + Path.GetFileName(scriptPath));
        }

        process.WaitForExit();
        return new PythonRunResult
        {
            ExitCode = process.ExitCode,
            StdOut = outBuilder.ToString().Trim(),
            StdErr = errBuilder.ToString().Trim()
        };
    }

    /// <summary>
    /// 解析解释器；结果缓存到进程退出。
    /// </summary>
    private static bool TryResolve(
        string? startDir,
        out string fileName,
        out IReadOnlyList<string> prefixList)
    {
        lock (Gate)
        {
            if (Resolved)
            {
                fileName = ResolvedFileName ?? "";
                prefixList = ResolvedPrefixList ?? Array.Empty<string>();
                return !string.IsNullOrWhiteSpace(ResolvedFileName);
            }

            foreach (var candidate in EnumerateCandidates(startDir))
            {
                if (!CanImportPillow(candidate.FileName, candidate.PrefixList))
                {
                    continue;
                }

                ResolvedFileName = candidate.FileName;
                ResolvedPrefixList = candidate.PrefixList;
                Resolved = true;
                fileName = candidate.FileName;
                prefixList = candidate.PrefixList;
                return true;
            }

            Resolved = true;
            ResolvedFileName = null;
            ResolvedPrefixList = null;
            fileName = "";
            prefixList = Array.Empty<string>();
            return false;
        }
    }

    /// <summary>
    /// 候选顺序：施工图工具虚拟环境，再系统 <c>py -3</c> / <c>python</c>。
    /// </summary>
    private static IEnumerable<(string FileName, IReadOnlyList<string> PrefixList)> EnumerateCandidates(
        string? startDir)
    {
        var toolRoot = ToolPaths.FindToolRoot(startDir);
        if (toolRoot != null)
        {
            var venv = Path.GetFullPath(Path.Combine(
                toolRoot,
                "..",
                "PDF转JPG_施工图用",
                ".venv",
                "Scripts",
                "python.exe"));
            if (File.Exists(venv))
            {
                yield return (venv, Array.Empty<string>());
            }
        }

        yield return ("py", new[] { "-3" });
        yield return ("python", Array.Empty<string>());
        yield return ("python3", Array.Empty<string>());
    }

    /// <summary>
    /// 探测解释器能否导入 Pillow。
    /// </summary>
    private static bool CanImportPillow(string fileName, IReadOnlyList<string> prefixList)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var prefix in prefixList)
        {
            startInfo.ArgumentList.Add(prefix);
        }

        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("import PIL");

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(8_000))
            {
                try
                {
                    process.Kill(true);
                }
                catch (InvalidOperationException)
                {
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
