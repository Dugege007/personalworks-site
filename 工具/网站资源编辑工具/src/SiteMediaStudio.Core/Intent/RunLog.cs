namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机运行日志，不进 Git。
/// </summary>
public static class RunLog
{
    private static readonly AsyncLocal<string?> mOverridePath = new();
    private static Func<string>? mTestPathFactory;

    /// <summary>
    /// 这次调用要写入的日志。测试设置了覆盖路径或测试工厂时，不再使用本机设置文件夹。
    /// </summary>
    public static string FilePath
    {
        get
        {
            var path = mOverridePath.Value;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var factory = mTestPathFactory;
            if (factory != null)
            {
                path = factory();
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException("测试日志路径不能为空。");
                }

                EnsureDirectory(path);
                mOverridePath.Value = path;
                return path;
            }

            return MachineFilePath();
        }
    }

    /// <summary>
    /// 本机设置文件夹中的运行日志：%AppData%/SiteMediaStudio/run.log。
    /// </summary>
    public static string MachineFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SiteMediaStudio");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "run.log");
    }

    /// <summary>
    /// 在当前异步流里改用指定日志文件，直到释放。供测试把日志指到自己的临时文件。
    /// </summary>
    public static IDisposable UsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("日志路径不能为空。", nameof(path));
        }

        EnsureDirectory(path);
        var previous = mOverridePath.Value;
        mOverridePath.Value = path;
        return new RestoreOverride(previous);
    }

    /// <summary>
    /// 测试进程启动时注册。当前异步流还没有覆盖路径时，用工厂生成一个临时日志文件并记住。
    /// 正式程序不注册，仍然写入本机设置文件夹。
    /// </summary>
    public static void SetTestPathFactory(Func<string>? factory)
    {
        mTestPathFactory = factory;
    }

    /// <summary>
    /// 追加一次执行或发布的结果与各步耗时。不写密钥。
    /// </summary>
    public static void AppendOutcome(string kind, BatchRunResult result)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var mark = result.Ok ? "成功" : "失败";
        var head = $"[{stamp}] {mark} {kind} 合计 {CommandStepTiming.FormatElapsed(result.Elapsed)}";
        if (!result.Ok && !string.IsNullOrWhiteSpace(result.FailedStep))
        {
            head += " 停在 " + result.FailedStep;
        }

        var lineList = new List<string> { head };
        foreach (var item in result.StepTimingList)
        {
            var title = string.IsNullOrWhiteSpace(item.Title) ? "步骤" : item.Title;
            lineList.Add("  " + title + "  " + CommandStepTiming.FormatElapsed(item.Elapsed));
        }

        foreach (var item in result.ResourceList)
        {
            var resourceMark = item.Ok ? "[成功]" : "[失败]";
            var name = string.IsNullOrWhiteSpace(item.Name) ? "条目" : item.Name;
            lineList.Add("  " + resourceMark + " " + name);
        }

        File.AppendAllText(FilePath, string.Join(Environment.NewLine, lineList) + Environment.NewLine);
    }

    /// <summary>
    /// 追加一步结果。不写密钥。
    /// </summary>
    public static void Append(string step, bool ok, string detail)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var mark = ok ? "成功" : "失败";
        var body = string.IsNullOrWhiteSpace(detail) ? "" : " " + TrimDetail(detail);
        File.AppendAllText(FilePath, $"[{stamp}] {mark} {step}{body}{Environment.NewLine}");
    }

    /// <summary>
    /// 去掉过长输出，避免日志膨胀。
    /// </summary>
    private static string TrimDetail(string detail)
    {
        var normalized = detail.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= 800)
        {
            return normalized.Replace('\n', ' ');
        }

        return normalized[..800].Replace('\n', ' ') + "…";
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed class RestoreOverride : IDisposable
    {
        private readonly string? mPrevious;
        private bool mDisposed;

        public RestoreOverride(string? previous)
        {
            mPrevious = previous;
        }

        public void Dispose()
        {
            if (mDisposed)
            {
                return;
            }

            mDisposed = true;
            mOverridePath.Value = mPrevious;
        }
    }
}
