namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 执行结果框一行的色义；窗口按左侧栏计数色对齐。
/// </summary>
public enum ResultLineKind
{
    Body,
    CountIngest,
    CountRestore,
    CountHide,
    CountWithdraw,
    CountRecycle,
    Success,
    Failure
}

/// <summary>
/// 执行结果框的一行。
/// </summary>
public sealed class ResultTextLine
{
    public ResultTextLine(string text, ResultLineKind kind = ResultLineKind.Body)
    {
        Text = text;
        Kind = kind;
    }

    public string Text { get; }
    public ResultLineKind Kind { get; }
}

/// <summary>
/// 本批各动作的标记数与完成数。
/// </summary>
public readonly record struct ExecuteResultCounts(
    int IngestMarked,
    int IngestOk,
    int RestoreMarked,
    int RestoreOk,
    int HideMarked,
    int HideOk,
    int WithdrawMarked,
    int WithdrawOk,
    int RecycleMarked,
    int RecycleOk,
    int CopyMarked,
    int CopyOk,
    int StarMarked = 0,
    int StarOk = 0);

/// <summary>
/// 本机执行结果框正文。
/// </summary>
public static class ExecuteResultComposer
{
    /// <summary>
    /// 成功结果：只用到的动作与步骤，耗时与资源分列。
    /// </summary>
    public static IReadOnlyList<ResultTextLine> BuildSuccess(
        BatchRunResult result,
        ExecuteResultCounts counts,
        IReadOnlyList<ResultTextLine>? copyLineList = null,
        string? logPath = null)
    {
        var lineList = new List<ResultTextLine>
        {
            new("执行已完成！（需要再点“发布上线”按钮才会更新正式网页）"),
            new("")
        };
        AppendCount(lineList, "上页", counts.IngestOk, counts.IngestMarked, ResultLineKind.CountIngest);
        AppendCount(lineList, "显示", counts.RestoreOk, counts.RestoreMarked, ResultLineKind.CountRestore);
        AppendCount(lineList, "隐藏", counts.HideOk, counts.HideMarked, ResultLineKind.CountHide);
        AppendCount(lineList, "待撤", counts.WithdrawOk, counts.WithdrawMarked, ResultLineKind.CountWithdraw);
        AppendCount(lineList, "回收", counts.RecycleOk, counts.RecycleMarked, ResultLineKind.CountRecycle);
        AppendCount(lineList, "文案", counts.CopyOk, counts.CopyMarked, ResultLineKind.Body);
        AppendCount(lineList, "星级", counts.StarOk, counts.StarMarked, ResultLineKind.Body);
        AppendTimings(lineList, result);
        AppendResources(lineList, result.ResourceList);
        AppendLedgerRetries(lineList, result.ResourceList);
        AppendCopy(lineList, copyLineList);
        AppendLog(lineList, logPath);
        return lineList;
    }

    /// <summary>
    /// 失败结果：保留失败步骤，耗时与资源仍按新格式。
    /// </summary>
    public static IReadOnlyList<ResultTextLine> BuildFailure(
        BatchRunResult result,
        string intentPath,
        IReadOnlyList<ResultTextLine>? copyLineList = null,
        string? logPath = null)
    {
        var lineList = new List<ResultTextLine>
        {
            new(result.CompletedStepList.Count == 0
                ? "尚未完成任何步。"
                : "已完成：" + string.Join("；", result.CompletedStepList)),
            new("失败步骤：" + (string.IsNullOrWhiteSpace(result.FailedStep) ? "未知步骤" : result.FailedStep),
                ResultLineKind.Failure),
            new("")
        };
        if (!string.IsNullOrWhiteSpace(result.FailureText))
        {
            lineList.Add(new(result.FailureText.Trim()));
            lineList.Add(new(""));
        }

        lineList.Add(new("意图文件：" + intentPath));
        AppendTimings(lineList, result);
        AppendResources(lineList, result.ResourceList);
        AppendLedgerRetries(lineList, result.ResourceList);
        AppendCopy(lineList, copyLineList);
        AppendLog(lineList, logPath);
        return lineList;
    }

    /// <summary>
    /// 对照草稿与意图，列出名称 / 描述变更。
    /// </summary>
    public static IReadOnlyList<ResultTextLine> BuildCopyLines(
        WorkspaceSession session,
        IntentDocument document,
        CopyDraftFile? draftFile = null)
    {
        if (session == null || document == null)
        {
            return Array.Empty<ResultTextLine>();
        }

        var file = draftFile ?? CopyDraftStore.Load();
        var lineList = new List<ResultTextLine>();
        foreach (var item in document.Items.Where(row => row.Intent == MediaIntentCodes.CopyUpdate))
        {
            var key = CopyDraftCompiler.KeyOf(item);
            var entry = string.IsNullOrWhiteSpace(key)
                ? null
                : CopyDraftStore.Find(file, session.Profile, key);
            var published = ResolvePublished(session, item);
            var subject = SubjectName(session, item, entry, published.Title);
            if (string.Equals(item.Target, "media", StringComparison.Ordinal))
            {
                AppendCopyChange(
                    lineList,
                    subject,
                    "名称",
                    entry?.PublishedTitle ?? published.Title,
                    item.Title);
            }

            AppendCopyChange(
                lineList,
                subject,
                "描述",
                entry?.PublishedDescription ?? published.Description,
                item.Description);
        }

        return lineList;
    }

    /// <summary>
    /// 拼成纯文本，供日志或无色窗。
    /// </summary>
    public static string ToPlainText(IReadOnlyList<ResultTextLine> lineList)
    {
        return string.Join(Environment.NewLine, lineList.Select(line => line.Text));
    }

    private static void AppendCount(
        List<ResultTextLine> lineList,
        string title,
        int ok,
        int marked,
        ResultLineKind kind)
    {
        if (ok <= 0 && marked <= 0)
        {
            return;
        }

        lineList.Add(new($"{title}：{ok}/{marked}", kind));
    }

    private static void AppendTimings(List<ResultTextLine> lineList, BatchRunResult result)
    {
        if (result.StepTimingList.Count == 0 && result.Elapsed <= TimeSpan.Zero)
        {
            return;
        }

        EnsureBlank(lineList);
        lineList.Add(new("各步骤耗时："));
        foreach (var item in CommandStepTiming.CoalesceByTitle(result.StepTimingList))
        {
            lineList.Add(new(CommandStepTiming.DisplayTitle(item.Title) + "：" + CommandStepTiming.FormatCompact(item.Elapsed)));
        }

        lineList.Add(new("合计：" + CommandStepTiming.FormatCompact(result.Elapsed)));
    }

    private static void AppendResources(
        List<ResultTextLine> lineList,
        IReadOnlyList<CommandResourceResult> resourceList)
    {
        if (resourceList.Count == 0)
        {
            return;
        }

        EnsureBlank(lineList);
        lineList.Add(new($"资源{resourceList.Count}项："));
        foreach (var item in resourceList)
        {
            var name = string.IsNullOrWhiteSpace(item.Name) ? "条目" : item.Name;
            lineList.Add(new((item.Ok ? "[成功] " : "[失败] ") + name,
                item.Ok ? ResultLineKind.Success : ResultLineKind.Failure));
        }
    }

    /// <summary>
    /// 列出本批台账改名重试过的资源，不记为失败。
    /// </summary>
    private static void AppendLedgerRetries(
        List<ResultTextLine> lineList,
        IReadOnlyList<CommandResourceResult> resourceList)
    {
        var textList = new List<string>();
        CommandResourceResult.AppendRetryBlock(textList, resourceList);
        if (textList.Count == 0)
        {
            return;
        }

        EnsureBlank(lineList);
        foreach (var text in textList)
        {
            if (text.Length == 0)
            {
                continue;
            }

            lineList.Add(new(text));
        }
    }

    private static void AppendCopy(List<ResultTextLine> lineList, IReadOnlyList<ResultTextLine>? copyLineList)
    {
        if (copyLineList == null || copyLineList.Count == 0)
        {
            return;
        }

        EnsureBlank(lineList);
        lineList.Add(new($"文案{copyLineList.Count}条："));
        lineList.AddRange(copyLineList);
    }

    private static void AppendLog(List<ResultTextLine> lineList, string? logPath)
    {
        EnsureBlank(lineList);
        var path = string.IsNullOrWhiteSpace(logPath) ? RunLog.FilePath : logPath;
        lineList.Add(new("日志已写入本机：" + path));
    }

    private static void EnsureBlank(List<ResultTextLine> lineList)
    {
        if (lineList.Count > 0 && lineList[^1].Text.Length > 0)
        {
            lineList.Add(new(""));
        }
    }

    private static void AppendCopyChange(
        List<ResultTextLine> lineList,
        string subject,
        string field,
        string? before,
        string? after)
    {
        var left = DisplayCopyValue(before);
        var right = DisplayCopyValue(after);
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return;
        }

        lineList.Add(new($"[成功] {subject}的{field}：{left}更改为{right}", ResultLineKind.Success));
    }

    private static string DisplayCopyValue(string? text)
    {
        return CopyText.IsFilled(text) ? text!.Trim() : "（空）";
    }

    private static (string Title, string Description) ResolvePublished(WorkspaceSession session, IntentItem item)
    {
        var target = item.Target ?? "";
        if (string.Equals(target, "channel", StringComparison.Ordinal))
        {
            session.ChannelLeadDict.TryGetValue(item.Channel ?? "", out var lead);
            return ("", lead ?? "");
        }

        var work = FindWork(session, item.Channel, item.WorkId);
        if (string.Equals(target, "work", StringComparison.Ordinal))
        {
            return ("", work?.Summary ?? "");
        }

        var media = FindMedia(work, item.Object);
        return (media?.DisplayName ?? "", media?.Description ?? "");
    }

    private static string SubjectName(
        WorkspaceSession session,
        IntentItem item,
        CopyDraftEntry? entry,
        string publishedTitle)
    {
        var target = item.Target ?? "";
        if (string.Equals(target, "channel", StringComparison.Ordinal))
        {
            var channel = session.Profile.Channels.FirstOrDefault(row =>
                string.Equals(row.Key, item.Channel, StringComparison.Ordinal));
            return CopyText.IsFilled(channel?.Zh) ? channel!.Zh : (item.Channel ?? "栏目");
        }

        if (string.Equals(target, "work", StringComparison.Ordinal))
        {
            var work = FindWork(session, item.Channel, item.WorkId);
            return CopyText.IsFilled(work?.Title) ? work!.Title : (item.WorkId ?? "项目");
        }

        if (CopyText.IsFilled(entry?.PublishedTitle) || CopyText.IsFilled(publishedTitle))
        {
            return CopyText.IsFilled(entry?.PublishedTitle) ? entry!.PublishedTitle.Trim() : publishedTitle.Trim();
        }

        if (CopyText.IsFilled(item.LabelBefore))
        {
            return item.LabelBefore!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(item.Object))
        {
            return Path.GetFileName(item.Object.Replace('/', Path.DirectorySeparatorChar));
        }

        return item.WorkId ?? "资源";
    }

    private static WorkCatalogItem? FindWork(WorkspaceSession session, string? channel, string? workId)
    {
        if (string.IsNullOrWhiteSpace(workId))
        {
            return null;
        }

        return session.Works.FirstOrDefault(item =>
            string.Equals(item.Id, workId, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(channel)
                || string.Equals(item.Channel, channel, StringComparison.Ordinal)));
    }

    private static WorkMediaItem? FindMedia(WorkCatalogItem? work, string? objectKey)
    {
        if (work == null || string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        var rel = JsonUtil.ToRel(objectKey);
        return work.Media.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Src)
            && string.Equals(JsonUtil.ToRel(item.Src), rel, StringComparison.Ordinal));
    }
}
