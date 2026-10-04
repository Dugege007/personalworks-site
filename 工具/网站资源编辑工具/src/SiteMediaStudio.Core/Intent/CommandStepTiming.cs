namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一条命令步的实测耗时。
/// </summary>
public sealed class CommandStepTiming
{
    public string Title { get; init; } = "";
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// 中文耗时：不足 0.1 秒、秒、分秒或小时。
    /// </summary>
    public static string FormatElapsed(TimeSpan span)
    {
        if (span < TimeSpan.Zero || double.IsNaN(span.TotalSeconds))
        {
            return "不足 0.1 秒";
        }

        if (span.TotalSeconds < 0.1)
        {
            return "不足 0.1 秒";
        }

        if (span.TotalHours >= 1)
        {
            return (int)span.TotalHours + " 小时 " + span.Minutes + " 分 " + span.Seconds + " 秒";
        }

        if (span.TotalMinutes >= 1)
        {
            return (int)span.TotalMinutes + " 分 " + span.Seconds + " 秒";
        }

        if (span.TotalSeconds >= 10)
        {
            return ((int)Math.Round(span.TotalSeconds)).ToString() + " 秒";
        }

        return span.TotalSeconds.ToString("0.0") + " 秒";
    }

    /// <summary>
    /// 执行结果框用的短耗时：不高于 0.1 秒写「不足0.1s」，其余为一位小数秒。
    /// </summary>
    public static string FormatCompact(TimeSpan span)
    {
        if (span < TimeSpan.Zero || double.IsNaN(span.TotalSeconds) || span.TotalSeconds <= 0.1)
        {
            return "不足0.1s";
        }

        return span.TotalSeconds.ToString("0.0") + "s";
    }

    /// <summary>
    /// 结果框步名；内容补丁缩为补丁。
    /// </summary>
    public static string DisplayTitle(string? title)
    {
        return title switch
        {
            "内容补丁" => "补丁",
            "重挂台账" => "重挂",
            _ => string.IsNullOrWhiteSpace(title) ? "步骤" : title
        };
    }

    /// <summary>
    /// 把标题相同的连续步合成一条，耗时相加。同类批量（COS、压图等）只记一步。
    /// </summary>
    public static IReadOnlyList<CommandStepTiming> CoalesceByTitle(IReadOnlyList<CommandStepTiming> timingList)
    {
        if (timingList.Count == 0)
        {
            return timingList;
        }

        var mergedList = new List<CommandStepTiming>();
        foreach (var item in timingList)
        {
            var title = string.IsNullOrWhiteSpace(item.Title) ? "步骤" : item.Title;
            if (mergedList.Count > 0
                && string.Equals(mergedList[^1].Title, title, StringComparison.Ordinal))
            {
                var last = mergedList[^1];
                mergedList[^1] = new CommandStepTiming
                {
                    Title = last.Title,
                    Elapsed = last.Elapsed + item.Elapsed
                };
                continue;
            }

            mergedList.Add(new CommandStepTiming
            {
                Title = title,
                Elapsed = item.Elapsed
            });
        }

        return mergedList;
    }

    /// <summary>
    /// 结果框与日志共用的各步耗时块。无计时则不写。同类标题只出一行。
    /// </summary>
    public static void AppendBlock(
        ICollection<string> lines,
        IReadOnlyList<CommandStepTiming> timingList,
        TimeSpan total)
    {
        var stepList = CoalesceByTitle(timingList);
        if (stepList.Count == 0 && total <= TimeSpan.Zero)
        {
            return;
        }

        if (lines.Count > 0)
        {
            lines.Add("");
        }

        lines.Add("各步耗时：");
        foreach (var item in stepList)
        {
            var title = string.IsNullOrWhiteSpace(item.Title) ? "步骤" : item.Title;
            lines.Add(title + "  " + FormatElapsed(item.Elapsed));
        }

        lines.Add("合计 " + FormatElapsed(total));
    }
}

/// <summary>
/// 一条资源在本批中的成败。
/// </summary>
public sealed class CommandResourceResult
{
    public string Name { get; init; } = "";
    public bool Ok { get; init; }

    /// <summary>
    /// 台账改名成功前重试了几次；零则不统计。
    /// </summary>
    public int LedgerRenameRetries { get; init; }

    /// <summary>
    /// 结果框与日志共用的资源名单。无条目则不写。
    /// </summary>
    public static void AppendBlock(
        ICollection<string> lines,
        IReadOnlyList<CommandResourceResult> resourceList)
    {
        if (resourceList.Count == 0)
        {
            return;
        }

        if (lines.Count > 0)
        {
            lines.Add("");
        }

        lines.Add("资源：");
        foreach (var item in resourceList)
        {
            var mark = item.Ok ? "[成功] " : "[失败] ";
            var name = string.IsNullOrWhiteSpace(item.Name) ? "条目" : item.Name;
            lines.Add(mark + name);
        }

        AppendRetryBlock(lines, resourceList);
    }

    /// <summary>
    /// 列出本批台账改名重试过的资源，不记为失败。
    /// </summary>
    public static void AppendRetryBlock(
        ICollection<string> lines,
        IReadOnlyList<CommandResourceResult> resourceList)
    {
        var retryList = resourceList
            .Where(item => item.LedgerRenameRetries > 0)
            .ToList();
        if (retryList.Count == 0)
        {
            return;
        }

        if (lines.Count > 0)
        {
            lines.Add("");
        }

        lines.Add($"台账改名重试{retryList.Count}项：");
        foreach (var item in retryList)
        {
            var name = string.IsNullOrWhiteSpace(item.Name) ? "条目" : item.Name;
            lines.Add($"{name}：{item.LedgerRenameRetries} 次");
        }
    }

    /// <summary>
    /// 组装一条资源结果。
    /// </summary>
    public static CommandResourceResult Of(string? name, bool ok, int ledgerRenameRetries = 0)
    {
        return new CommandResourceResult
        {
            Name = string.IsNullOrWhiteSpace(name) ? "条目" : name,
            Ok = ok,
            LedgerRenameRetries = ledgerRenameRetries
        };
    }
}
