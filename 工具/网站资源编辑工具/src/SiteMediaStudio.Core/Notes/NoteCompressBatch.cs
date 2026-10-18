using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一张待压缩的心得配图。发布原因由界面用 <see cref="NoteImageGuard"/> 填好，压缩器自己不读索引。
/// </summary>
public sealed class NoteCompressTarget
{
    /// <summary>
    /// 相对笔记文件夹的路径。
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 已发布时的原因。空白表示可以压缩。应使用 <see cref="NoteImageGuard.PublishedBlockText"/>。
    /// </summary>
    public string? PublishedReason { get; init; }
}

/// <summary>
/// 一批配图的压缩结果，供结果框、重试和 run.log 使用。
/// </summary>
public sealed class NoteCompressBatchResult
{
    /// <summary>
    /// 每张配图的压缩结果，顺序与请求一致。
    /// </summary>
    public IReadOnlyList<NoteCompressResult> FileList { get; init; } = Array.Empty<NoteCompressResult>();

    /// <summary>
    /// 结果框里逐张显示的文字。每张一块，块内用换行分开大小、质量和原因。
    /// </summary>
    public IReadOnlyList<string> DetailLineList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 这一批的汇总句。
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// 因文件被占用而跳过的相对路径，供「重试」只处理这些配图。
    /// </summary>
    public IReadOnlyList<string> RetryRelativePathList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 是否有被占用而跳过的配图。
    /// </summary>
    public bool CanRetry => RetryRelativePathList.Count > 0;
}

/// <summary>
/// 按顺序压缩一篇笔记里的多张配图，并给出进度、结果文字和运行日志。
/// 单张压缩仍由 <see cref="NoteImageCompressor"/> 完成。
/// </summary>
public static class NoteCompressBatch
{
    /// <summary>
    /// 没有配图时的结果。
    /// </summary>
    public static NoteCompressBatchResult Empty { get; } = new()
    {
        Summary = "没有需要压缩的配图。"
    };

    /// <summary>
    /// 压缩列出的配图。一张失败或被占用时继续处理其余配图。
    /// 模板里的阈值、照片信息开关、外部程序路径、手动质量和是否从原图备份开始，会原样传给每一张。
    /// 进度的当前步是第几张，说明文字是当前质量级别。
    /// </summary>
    public static NoteCompressBatchResult Compress(
        string noteFolder,
        IReadOnlyList<NoteCompressTarget> targetList,
        NoteCompressOptions template,
        IProgress<PublishProgress>? progress = null)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (targetList == null || targetList.Count == 0)
        {
            NoteBackupProgress.Report(progress, 0, 0, "配图", Empty.Summary, finishedFile: true);
            return Empty;
        }

        var fileList = new List<NoteCompressResult>(targetList.Count);
        var total = targetList.Count;
        for (var index = 0; index < total; index++)
        {
            var target = targetList[index];
            var fileName = FileNameOf(target.RelativePath);
            NoteBackupProgress.Report(progress, index, total, fileName, "准备压缩", finishedFile: false);
            fileList.Add(CompressOne(noteFolder, target, template, progress, index, total, fileName));
        }

        return FromFileList(fileList);
    }

    /// <summary>
    /// 把已经得到的单张结果收成一批。测试可以用它检查汇总、重试名单和结果文字。
    /// </summary>
    public static NoteCompressBatchResult FromFileList(IReadOnlyList<NoteCompressResult> fileList)
    {
        if (fileList == null)
        {
            throw new ArgumentNullException(nameof(fileList));
        }

        if (fileList.Count == 0)
        {
            return Empty;
        }

        return new NoteCompressBatchResult
        {
            FileList = fileList,
            DetailLineList = fileList.Select(FormatDetail).ToList(),
            Summary = FormatSummary(fileList),
            RetryRelativePathList = fileList
                .Where(item => item.Status == NoteCompressStatus.SkippedLocked)
                .Select(item => item.RelativePath)
                .ToList()
        };
    }

    /// <summary>
    /// 一张配图在结果框里的文字：原大小、新大小、最终质量参数、是否缩小尺寸，以及成功或失败原因。
    /// </summary>
    public static string FormatDetail(NoteCompressResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var quality = string.IsNullOrWhiteSpace(result.FinalQualityParameter)
            ? "无"
            : result.FinalQualityParameter.Trim();
        var shrink = result.DidShrinkDimensions ? "已缩小尺寸" : "未缩小尺寸";
        var fileName = string.IsNullOrWhiteSpace(result.FileName) ? FileNameOf(result.RelativePath) : result.FileName;
        var lineList = new List<string>
        {
            fileName
                + "：原大小 " + FileSizeFormat.Format(result.SourceByteCount)
                + "，新大小 " + FileSizeFormat.Format(result.ResultByteCount)
                + "，最终质量参数：" + quality
                + "，" + shrink + "。"
        };
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            lineList.Add(result.Message.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.ExternalToolNote))
        {
            lineList.Add(result.ExternalToolNote.Trim());
        }

        return string.Join("\n", lineList);
    }

    /// <summary>
    /// 一批压缩的汇总句。没有配图时说明没有需要压缩的配图。
    /// </summary>
    public static string FormatSummary(IReadOnlyList<NoteCompressResult> fileList)
    {
        if (fileList == null || fileList.Count == 0)
        {
            return Empty.Summary;
        }

        var succeeded = 0;
        var stillOver = 0;
        var kept = 0;
        var published = 0;
        var locked = 0;
        var failed = 0;
        foreach (var item in fileList)
        {
            switch (item.Status)
            {
                case NoteCompressStatus.Succeeded:
                    succeeded++;
                    break;
                case NoteCompressStatus.StillOverLimit:
                    stillOver++;
                    break;
                case NoteCompressStatus.AbandonedLarger:
                    kept++;
                    break;
                case NoteCompressStatus.SkippedPublished:
                    published++;
                    break;
                case NoteCompressStatus.SkippedLocked:
                    locked++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        return "共 " + fileList.Count.ToString(CultureInfo.InvariantCulture)
            + " 张：成功 " + succeeded.ToString(CultureInfo.InvariantCulture)
            + "，压缩后仍超限 " + stillOver.ToString(CultureInfo.InvariantCulture)
            + "，已保留原图 " + kept.ToString(CultureInfo.InvariantCulture)
            + "，已发布跳过 " + published.ToString(CultureInfo.InvariantCulture)
            + "，占用跳过 " + locked.ToString(CultureInfo.InvariantCulture)
            + "，失败 " + failed.ToString(CultureInfo.InvariantCulture) + "。";
    }

    /// <summary>
    /// 把这一批写入 run.log。汇总单独一行，每张配图再写一行。
    /// 占用、仍超限和失败记为失败，其余记为成功。
    /// </summary>
    public static void WriteLog(NoteCompressBatchResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var lineList = new List<(bool Ok, string Line)>();
        foreach (var item in result.FileList)
        {
            var ok = item.Status is NoteCompressStatus.Succeeded
                or NoteCompressStatus.AbandonedLarger
                or NoteCompressStatus.SkippedPublished;
            lineList.Add((ok, FormatDetail(item).Replace('\n', ' ')));
        }

        var summaryOk = result.FileList.All(item => item.Status != NoteCompressStatus.Failed
            && item.Status != NoteCompressStatus.SkippedLocked);
        NoteBackupLog.Append("压缩配图", summaryOk || result.FileList.Count == 0, result.Summary, lineList);
    }

    /// <summary>
    /// 压缩一张。定位或写盘抛出的异常变成这一张的失败，不中断整批。
    /// </summary>
    private static NoteCompressResult CompressOne(
        string noteFolder,
        NoteCompressTarget target,
        NoteCompressOptions template,
        IProgress<PublishProgress>? progress,
        int index,
        int total,
        string fileName)
    {
        try
        {
            var options = template.ForImage(
                target.PublishedReason,
                snapshot => NoteBackupProgress.Report(
                    progress,
                    index,
                    total,
                    fileName,
                    snapshot.Detail,
                    finishedFile: false),
                template.ManualQuality,
                template.StartFromOriginalBackup);
            var result = NoteImageCompressor.Compress(noteFolder, target.RelativePath, options);
            NoteBackupProgress.Report(progress, index, total, fileName, result.Message, finishedFile: true);
            return result;
        }
        catch (Exception exception) when (exception is ArgumentException
            or DirectoryNotFoundException
            or IOException
            or UnauthorizedAccessException)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message) ? "压缩失败。" : exception.Message.Trim();
            var failed = new NoteCompressResult
            {
                Status = NoteCompressStatus.Failed,
                RelativePath = target.RelativePath,
                FileName = fileName,
                Message = message,
                FailureReason = message
            };
            NoteBackupProgress.Report(progress, index, total, fileName, message, finishedFile: true);
            return failed;
        }
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
