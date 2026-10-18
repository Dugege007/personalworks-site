using System.Diagnostics;
using System.Text;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次 git 命令的结果。找不到程序、无法启动或超时时 <see cref="Completed"/> 为 false，调用方不应据此崩溃。
/// </summary>
public sealed class GitCommandResult
{
    /// <summary>
    /// 进程已退出。false 表示没有跑完。
    /// </summary>
    public bool Completed { get; init; }

    /// <summary>
    /// 进程退出码。没有跑完时为 0，不能单独当成成功。
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// 标准输出。
    /// </summary>
    public string StandardOutput { get; init; } = "";

    /// <summary>
    /// 标准错误。
    /// </summary>
    public string StandardError { get; init; } = "";

    /// <summary>
    /// 进程正常结束且退出码为 0。
    /// </summary>
    public bool Succeeded => Completed && ExitCode == 0;
}

/// <summary>
/// 执行本机 git。检查器依赖这个接口，测试可以替换成不访问网络的替身。
/// </summary>
public interface IGitCommandRunner
{
    /// <summary>
    /// 在指定目录执行 git。超时、找不到程序或无法启动时返回未完成结果，不抛异常。
    /// </summary>
    GitCommandResult Run(string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout);
}

/// <summary>
/// 调用本机 git 可执行文件。不经过外壳，因此参数不会被再解释一遍。
/// </summary>
public sealed class GitProcessRunner : IGitCommandRunner
{
    /// <summary>
    /// 在工作目录执行 git。工作目录不存在时视为无法执行。
    /// </summary>
    public GitCommandResult Run(string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || arguments == null || arguments.Count == 0)
        {
            return new GitCommandResult();
        }

        if (timeout <= TimeSpan.Zero || !Directory.Exists(workingDirectory))
        {
            return new GitCommandResult();
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        // 只作用于这一次进程，不改用户的 git 配置。避免凭据提示把检查挂住，并允许读取当前用户有权限的仓库。
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("safe.directory=*");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("core.quotepath=false");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("i18n.logOutputEncoding=utf-8");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument ?? "");
        }

        try
        {
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        }
        catch (InvalidOperationException)
        {
            return new GitCommandResult();
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new GitCommandResult();
            }

            process.StandardInput.Close();
            // 先读管道再等待退出，避免输出写满管道后两边互相卡住。
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue)))
            {
                TryKill(process);
                Observe(stdoutTask);
                Observe(stderrTask);
                return new GitCommandResult();
            }

            process.WaitForExit();
            return new GitCommandResult
            {
                Completed = true,
                ExitCode = process.ExitCode,
                StandardOutput = ReadCompleted(stdoutTask),
                StandardError = ReadCompleted(stderrTask)
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return new GitCommandResult();
        }
    }

    /// <summary>
    /// 读取已经结束的输出。读失败时当作空文本，不把异常抛出检查流程。
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
    /// 结束卡住的 git 及其子进程。进程已经退出时忽略。
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
}
