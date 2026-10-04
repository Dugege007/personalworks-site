namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机运行日志，不进 Git。
/// </summary>
public static class RunLog
{
    /// <summary>
    /// %AppData%/SiteMediaStudio/run.log
    /// </summary>
    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SiteMediaStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "run.log");
        }
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
}
