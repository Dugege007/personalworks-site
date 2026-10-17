using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一张待恢复的配图。已发布的原因不为空白时不调用恢复，只在结果里跳过。
/// </summary>
public sealed class NoteImageRestoreTarget
{
    /// <summary>
    /// 相对笔记文件夹的路径。
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 已发布时的原因。空白表示可以恢复。
    /// </summary>
    public string? PublishedReason { get; init; }
}

/// <summary>
/// 恢复或清理里的一个文件结果。
/// </summary>
public sealed class NoteImageFileOutcome
{
    /// <summary>
    /// 相对笔记文件夹的路径。
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 文件名。
    /// </summary>
    public string FileName { get; init; } = "";

    /// <summary>
    /// 这一张是否按预期完成。被占用和失败都不是完成。
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// 是否因为文件被占用而跳过。
    /// </summary>
    public bool Locked { get; init; }

    /// <summary>
    /// 给人看的一句说明。被占用时已带上重试提示。
    /// </summary>
    public string Message { get; init; } = "";
}

/// <summary>
/// 一次恢复或清理的汇总，供结果框和重试使用。
/// </summary>
public sealed class NoteBackupReport
{
    /// <summary>
    /// 每个文件的结果。
    /// </summary>
    public IReadOnlyList<NoteImageFileOutcome> FileList { get; init; } = Array.Empty<NoteImageFileOutcome>();

    /// <summary>
    /// 结果框逐条显示的文字。
    /// </summary>
    public IReadOnlyList<string> DetailLineList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 汇总句。
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// 被占用而留下的相对路径。
    /// </summary>
    public IReadOnlyList<string> RetryRelativePathList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 是否有可以重试的占用项。
    /// </summary>
    public bool CanRetry => RetryRelativePathList.Count > 0;
}

/// <summary>
/// 恢复原图、清理一篇或全部笔记的原图备份。压缩见 <see cref="NoteCompressBatch"/>。
/// 文件替换和送进回收站仍由 <see cref="NoteImageBackup"/> 完成。
/// </summary>
public static class NoteBackupJob
{
    /// <summary>
    /// 清理确认框的正文。数量和总大小来自调用方已经统计好的备份。
    /// </summary>
    public static string FormatPurgeConfirm(NoteImageBackupMeasure measure)
    {
        return "将把 " + measure.FileCount.ToString(CultureInfo.InvariantCulture)
            + " 张原图（共 " + FileSizeFormat.Format(measure.TotalBytes)
            + "）送进回收站，清理后只能从回收站找回。确定继续吗？";
    }

    /// <summary>
    /// 被占用时的结果框句子。已经带有重试提示时不再追加。
    /// </summary>
    public static string RetryText(NoteImageBackupResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var message = result.Message?.Trim() ?? "";
        if (result.Kind != NoteImageBackupKind.Locked)
        {
            return message;
        }

        if (message.Contains("重试", StringComparison.Ordinal))
        {
            return message;
        }

        if (!message.EndsWith('。'))
        {
            message += "。";
        }

        return message + "请关闭该程序后点击「重试」";
    }

    /// <summary>
    /// 列出心得栏目下含有正文的笔记文件夹。以点开头的目录不算笔记。
    /// </summary>
    public static IReadOnlyList<string> ListNoteFolders(string channelDirectory)
    {
        if (string.IsNullOrWhiteSpace(channelDirectory) || !Directory.Exists(channelDirectory))
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(channelDirectory))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.'))
            {
                continue;
            }

            if (!File.Exists(Path.Combine(dir, NoteRules.BodyFileName)))
            {
                continue;
            }

            list.Add(dir);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>
    /// 统计一个心得栏目里全部笔记的原图备份数量和总字节数。
    /// </summary>
    public static NoteImageBackupMeasure MeasureChannel(string channelDirectory)
    {
        var count = 0;
        long total = 0;
        foreach (var folder in ListNoteFolders(channelDirectory))
        {
            var measure = NoteImageBackup.Measure(folder);
            count += measure.FileCount;
            total += measure.TotalBytes;
        }

        return new NoteImageBackupMeasure(count, total);
    }

    /// <summary>
    /// 恢复列出的配图。已发布的只记入结果，不写回文件。被占用的跳过并带上重试说明。
    /// </summary>
    public static NoteBackupReport Restore(
        string noteFolder,
        IReadOnlyList<NoteImageRestoreTarget> targetList,
        IProgress<PublishProgress>? progress = null)
    {
        if (targetList == null || targetList.Count == 0)
        {
            var empty = ReportOf(Array.Empty<NoteImageFileOutcome>(), "没有需要恢复的配图。");
            NoteBackupProgress.Report(progress, 0, 0, "配图", empty.Summary, finishedFile: true);
            return empty;
        }

        var outcomeList = new List<NoteImageFileOutcome>(targetList.Count);
        var total = targetList.Count;
        for (var index = 0; index < total; index++)
        {
            var target = targetList[index];
            var fileName = FileNameOf(target.RelativePath);
            NoteBackupProgress.Report(progress, index, total, fileName, "正在恢复原图", finishedFile: false);
            outcomeList.Add(RestoreOne(noteFolder, target, progress, index, total, fileName));
        }

        return ReportOf(outcomeList, FormatRestoreSummary(outcomeList));
    }

    /// <summary>
    /// 把一篇笔记的原图备份送进回收站。不传回收动作时使用系统回收站。
    /// </summary>
    public static NoteBackupReport Purge(
        string noteFolder,
        IProgress<PublishProgress>? progress = null,
        Action<string>? sendToRecycle = null)
    {
        var measure = NoteImageBackup.Measure(noteFolder);
        var seen = 0;
        NoteBackupProgress.Report(progress, 0, Math.Max(measure.FileCount, 1), "原图备份", "正在清理原图备份", finishedFile: false);
        var purge = NoteImageBackup.Purge(noteFolder, WrapRecycle(progress, measure.FileCount, sendToRecycle, () => seen, value => seen = value));
        var outcomeList = purge.FileList.Select(ToOutcome).ToList();
        var report = ReportOf(outcomeList, FormatPurgeSummary(outcomeList));
        NoteBackupProgress.Report(
            progress,
            Math.Max(measure.FileCount, 1) - 1,
            Math.Max(measure.FileCount, 1),
            "原图备份",
            report.Summary,
            finishedFile: true);
        return report;
    }

    /// <summary>
    /// 清理一个心得栏目下每一篇笔记的原图备份。
    /// </summary>
    public static NoteBackupReport PurgeChannel(
        string channelDirectory,
        IProgress<PublishProgress>? progress = null,
        Action<string>? sendToRecycle = null)
    {
        var measure = MeasureChannel(channelDirectory);
        var folders = ListNoteFolders(channelDirectory);
        var seen = 0;
        var outcomeList = new List<NoteImageFileOutcome>();
        if (folders.Count == 0 || measure.FileCount == 0)
        {
            var empty = ReportOf(outcomeList, FormatPurgeSummary(outcomeList));
            NoteBackupProgress.Report(progress, 0, 0, "原图备份", empty.Summary, finishedFile: true);
            return empty;
        }

        foreach (var folder in folders)
        {
            var folderMeasure = NoteImageBackup.Measure(folder);
            if (folderMeasure.FileCount == 0)
            {
                continue;
            }

            var folderName = Path.GetFileName(folder);
            NoteBackupProgress.Report(
                progress,
                seen,
                measure.FileCount,
                folderName,
                "正在清理「" + folderName + "」的原图备份",
                finishedFile: false);
            var purge = NoteImageBackup.Purge(
                folder,
                WrapRecycle(progress, measure.FileCount, sendToRecycle, () => seen, value => seen = value));
            outcomeList.AddRange(purge.FileList.Select(ToOutcome));
        }

        var report = ReportOf(outcomeList, FormatPurgeSummary(outcomeList));
        NoteBackupProgress.Report(progress, measure.FileCount - 1, measure.FileCount, "原图备份", report.Summary, finishedFile: true);
        return report;
    }

    /// <summary>
    /// 把恢复或清理的结果写入 run.log。
    /// </summary>
    public static void WriteLog(string actionName, NoteBackupReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var lineList = report.FileList
            .Select(item => (item.Ok, item.FileName + "：" + item.Message))
            .ToList();
        var summaryOk = report.FileList.All(item => item.Ok);
        NoteBackupLog.Append(actionName, summaryOk || report.FileList.Count == 0, report.Summary, lineList);
    }

    /// <summary>
    /// 恢复一张。已发布时不碰文件。
    /// </summary>
    private static NoteImageFileOutcome RestoreOne(
        string noteFolder,
        NoteImageRestoreTarget target,
        IProgress<PublishProgress>? progress,
        int index,
        int total,
        string fileName)
    {
        if (!string.IsNullOrWhiteSpace(target.PublishedReason))
        {
            var skipped = fileName + " 已发布，已跳过";
            NoteBackupProgress.Report(progress, index, total, fileName, skipped, finishedFile: true);
            return new NoteImageFileOutcome
            {
                RelativePath = target.RelativePath,
                FileName = fileName,
                Ok = true,
                Message = skipped
            };
        }

        NoteImageBackupResult result;
        try
        {
            result = NoteImageBackup.Restore(noteFolder, target.RelativePath);
        }
        catch (Exception exception) when (exception is ArgumentException
            or DirectoryNotFoundException
            or IOException
            or UnauthorizedAccessException)
        {
            result = NoteImageBackupResult.Failed(target.RelativePath, exception.Message);
        }

        var message = RetryText(result);
        NoteBackupProgress.Report(progress, index, total, fileName, message, finishedFile: true);
        return new NoteImageFileOutcome
        {
            RelativePath = string.IsNullOrWhiteSpace(result.RelativePath) ? target.RelativePath : result.RelativePath,
            FileName = fileName,
            Ok = result.Kind == NoteImageBackupKind.Restored,
            Locked = result.Kind == NoteImageBackupKind.Locked,
            Message = message
        };
    }

    /// <summary>
    /// 把备份结果改成界面用的一条。被占用的说明补上重试。
    /// </summary>
    private static NoteImageFileOutcome ToOutcome(NoteImageBackupResult result)
    {
        return new NoteImageFileOutcome
        {
            RelativePath = result.RelativePath,
            FileName = FileNameOf(result.RelativePath),
            Ok = result.Kind == NoteImageBackupKind.Purged,
            Locked = result.Kind == NoteImageBackupKind.Locked,
            Message = RetryText(result)
        };
    }

    /// <summary>
    /// 在送进回收站之前刷新进度。调用方没有提供回收动作时使用系统回收站。
    /// </summary>
    private static Action<string> WrapRecycle(
        IProgress<PublishProgress>? progress,
        int total,
        Action<string>? sendToRecycle,
        Func<int> readSeen,
        Action<int> writeSeen)
    {
        var inner = sendToRecycle ?? RecycleService.SendToRecycleBin;
        return path =>
        {
            var seen = readSeen() + 1;
            writeSeen(seen);
            var index = Math.Max(0, seen - 1);
            NoteBackupProgress.Report(
                progress,
                index,
                Math.Max(total, 1),
                Path.GetFileName(path),
                "正在送进回收站",
                finishedFile: false);
            inner(path);
        };
    }

    /// <summary>
    /// 收成一份报告。重试名单只保留被占用的文件。
    /// </summary>
    private static NoteBackupReport ReportOf(IReadOnlyList<NoteImageFileOutcome> outcomeList, string summary)
    {
        return new NoteBackupReport
        {
            FileList = outcomeList,
            DetailLineList = outcomeList.Select(item => item.FileName + "：" + item.Message).ToList(),
            Summary = summary,
            RetryRelativePathList = outcomeList.Where(item => item.Locked).Select(item => item.RelativePath).ToList()
        };
    }

    /// <summary>
    /// 恢复汇总。没有文件时由调用方传入单独的句子。
    /// </summary>
    private static string FormatRestoreSummary(IReadOnlyList<NoteImageFileOutcome> outcomeList)
    {
        var published = outcomeList.Count(item => item.Message.Contains("已发布，已跳过", StringComparison.Ordinal));
        var restored = outcomeList.Count(item => item.Ok) - published;
        var locked = outcomeList.Count(item => item.Locked);
        var failed = outcomeList.Count - restored - published - locked;
        return "共 " + outcomeList.Count.ToString(CultureInfo.InvariantCulture)
            + " 张：已恢复 " + restored.ToString(CultureInfo.InvariantCulture)
            + "，已发布跳过 " + published.ToString(CultureInfo.InvariantCulture)
            + "，占用跳过 " + locked.ToString(CultureInfo.InvariantCulture)
            + "，失败 " + Math.Max(0, failed).ToString(CultureInfo.InvariantCulture) + "。";
    }

    /// <summary>
    /// 清理汇总。没有备份时说明没有需要清理的原图。
    /// </summary>
    private static string FormatPurgeSummary(IReadOnlyList<NoteImageFileOutcome> outcomeList)
    {
        if (outcomeList.Count == 0)
        {
            return "没有需要清理的原图备份。";
        }

        var purged = outcomeList.Count(item => item.Ok);
        var locked = outcomeList.Count(item => item.Locked);
        var failed = outcomeList.Count - purged - locked;
        return "共 " + outcomeList.Count.ToString(CultureInfo.InvariantCulture)
            + " 张：已送进回收站 " + purged.ToString(CultureInfo.InvariantCulture)
            + "，占用跳过 " + locked.ToString(CultureInfo.InvariantCulture)
            + "，失败 " + Math.Max(0, failed).ToString(CultureInfo.InvariantCulture) + "。";
    }

    /// <summary>
    /// 从相对路径取出文件名。
    /// </summary>
    private static string FileNameOf(string relativePath)
    {
        var name = Path.GetFileName((relativePath ?? "").Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "这张配图" : name;
    }
}

/// <summary>
/// 压缩、恢复和清理共用的进度快照。当前步是第几张，说明里放质量级别或当前动作。
/// </summary>
internal static class NoteBackupProgress
{
    /// <summary>
    /// 推送一条进度。没有进度对象时什么也不做。
    /// </summary>
    public static void Report(
        IProgress<PublishProgress>? progress,
        int index,
        int total,
        string title,
        string detail,
        bool finishedFile)
    {
        if (progress == null)
        {
            return;
        }

        var safeTotal = Math.Max(total, 1);
        var done = total <= 0 ? safeTotal : finishedFile ? index + 1 : index;
        var percent = total <= 0
            ? 100
            : (int)Math.Round(done * 100.0 / safeTotal);
        progress.Report(new PublishProgress
        {
            Percent = Math.Clamp(percent, 0, 100),
            CurrentStep = Math.Clamp(index + 1, 1, safeTotal),
            TotalStep = safeTotal,
            Title = string.IsNullOrWhiteSpace(title) ? "配图" : title.Trim(),
            Detail = string.IsNullOrWhiteSpace(detail) ? "进行中…" : detail.Trim()
        });
    }
}

/// <summary>
/// 把一批配图处理结果追加到 run.log。
/// </summary>
internal static class NoteBackupLog
{
    /// <summary>
    /// 先写汇总，再按文件各写一行。不写密钥。
    /// </summary>
    public static void Append(string actionName, bool summaryOk, string summary, IReadOnlyList<(bool Ok, string Line)> lineList)
    {
        var action = string.IsNullOrWhiteSpace(actionName) ? "配图" : actionName.Trim();
        RunLog.Append(action, summaryOk, summary ?? "");
        foreach (var line in lineList)
        {
            RunLog.Append(action, line.Ok, line.Line ?? "");
        }
    }
}
