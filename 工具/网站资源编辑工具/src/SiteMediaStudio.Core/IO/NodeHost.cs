using System.Diagnostics;
using System.Text;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次 Node 子进程的退出结果。
/// </summary>
public sealed class NodeRunResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public bool Ok => ExitCode == 0;
}

/// <summary>
/// 以 UTF-8 调用系统 PATH 上的 node。
/// </summary>
public static class NodeHost
{
    /// <summary>
    /// 运行脚本；超时或启动失败时抛出。可附加环境变量，不替换整份 PATH。
    /// 输出行回调在进程读线程上触发，调用方自行切回界面线程。
    /// </summary>
    public static NodeRunResult Run(
        string scriptPath,
        IReadOnlyList<string> argumentList,
        string workingDirectory,
        int timeoutMs = 60_000,
        IReadOnlyDictionary<string, string>? environment = null,
        Action<string>? onOutputLine = null)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("找不到 Node 脚本。", scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment != null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
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
            onOutputLine?.Invoke(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 node。");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("系统 PATH 中找不到 node。", ex);
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

            throw new TimeoutException("Node 脚本超时：" + Path.GetFileName(scriptPath));
        }

        process.WaitForExit();
        return new NodeRunResult
        {
            ExitCode = process.ExitCode,
            StdOut = outBuilder.ToString().Trim(),
            StdErr = errBuilder.ToString().Trim()
        };
    }
}
