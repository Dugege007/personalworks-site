using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 心得配图体检结果对应的界面文案。检视栏、网格角标和底栏都从这里取句子，避免各写一遍。
/// </summary>
public static class NoteImagePrompt
{
    /// <summary>
    /// 网格角标上的超限字样。
    /// </summary>
    public const string OverLimitBadge = "超限";

    /// <summary>
    /// 检视栏里表示原图备份仍在的字样。
    /// </summary>
    public const string CompressedMark = "已压缩";

    /// <summary>
    /// 底栏上的超限句。没有超限配图时返回空字符串。
    /// </summary>
    public static string FormatOverLimitFooter(int count)
    {
        return count > 0 ? "本篇有 " + count.ToString(CultureInfo.InvariantCulture) + " 张配图超限" : "";
    }

    /// <summary>
    /// 底栏上的缺失句。没有缺失配图时返回空字符串。
    /// </summary>
    public static string FormatMissingFooter(int count)
    {
        return count > 0 ? "本篇有 " + count.ToString(CultureInfo.InvariantCulture) + " 张配图缺失" : "";
    }

    /// <summary>
    /// 底栏上的文件夹外引用句。没有这类引用时返回空字符串。
    /// </summary>
    public static string FormatOutsideFooter(int count)
    {
        return count > 0 ? "本篇有 " + count.ToString(CultureInfo.InvariantCulture) + " 处引用超出笔记文件夹" : "";
    }

    /// <summary>
    /// 底栏要显示的全部句子。没有问题时返回空字符串。多条之间换行。
    /// </summary>
    public static string FormatFooter(NoteImageAuditReport? report)
    {
        if (report == null)
        {
            return "";
        }

        return JoinLines(
            FormatOverLimitFooter(report.OverLimitCount),
            FormatMissingFooter(report.MissingCount),
            FormatOutsideFooter(report.OutsideCount));
    }

    /// <summary>
    /// 检视栏列出的缺失文件名和文件夹外引用。没有问题时返回空字符串。
    /// </summary>
    public static string FormatIssueText(NoteImageAuditReport? report)
    {
        if (report == null)
        {
            return "";
        }

        var missing = report.MissingCount == 0
            ? ""
            : "缺失配图：" + string.Join("、", report.MissingList.Select(DisplayMissing));
        var outside = report.OutsideCount == 0
            ? ""
            : "引用超出笔记文件夹：" + string.Join("、", report.OutsideList.Select(item => item.Reference));
        return JoinLines(missing, outside);
    }

    /// <summary>
    /// 缺失配图或文件夹外引用导致不能上页时的说明。可以上页时返回空。超限不在这句里。
    /// </summary>
    public static string PublishBlockReason(NoteImageAuditReport? report)
    {
        if (report == null || !report.BlocksPublish)
        {
            return "";
        }

        return JoinParts(
            FormatMissingFooter(report.MissingCount),
            FormatOutsideFooter(report.OutsideCount)) + "，不能上页。";
    }

    /// <summary>
    /// 选中这张配图时，检视栏大小旁边的超限警告。未超限时返回空字符串。
    /// </summary>
    public static string WarningFor(NoteImageAuditReport? report, string? relativePath)
    {
        var over = FindOverLimit(report, relativePath);
        return over == null ? "" : NoteImageThreshold.FormatOverLimit(over.Bytes, over.MaxBytes);
    }

    /// <summary>
    /// 这张配图的网格角标。未超限时返回空字符串。
    /// </summary>
    public static string LimitBadgeFor(NoteImageAuditReport? report, string? stageRel, string? folderName)
    {
        if (report == null || string.IsNullOrWhiteSpace(stageRel) || string.IsNullOrWhiteSpace(folderName))
        {
            return "";
        }

        var relativePath = NoteRules.RelativeToNoteFolder(stageRel, folderName);
        return FindOverLimit(report, relativePath) == null ? "" : OverLimitBadge;
    }

    /// <summary>
    /// 这篇笔记的 .originals 里是否有这张配图的备份。有备份即视为已压缩。
    /// </summary>
    public static bool HasOriginalBackup(string noteFolderFullPath, string? imageRelativePath)
    {
        if (string.IsNullOrWhiteSpace(noteFolderFullPath) || string.IsNullOrWhiteSpace(imageRelativePath))
        {
            return false;
        }

        var backupPath = Path.Combine(
            noteFolderFullPath,
            NoteRules.OriginalsDirName,
            imageRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(backupPath);
    }

    /// <summary>
    /// 按相对路径找出超限记录。比较时不区分大小写。
    /// </summary>
    private static NoteOverLimitImage? FindOverLimit(NoteImageAuditReport? report, string? relativePath)
    {
        if (report == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var key = JsonUtil.ToRel(relativePath);
        return report.OverLimitList.FirstOrDefault(item =>
            string.Equals(JsonUtil.ToRel(item.RelativePath), key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 缺失项在界面上优先显示相对路径，没有相对路径时退回正文里的地址。
    /// </summary>
    private static string DisplayMissing(NoteMissingImage image)
    {
        return string.IsNullOrWhiteSpace(image.RelativePath) ? image.Reference : image.RelativePath;
    }

    /// <summary>
    /// 去掉空行后用换行接起来。
    /// </summary>
    private static string JoinLines(params string[] lineList)
    {
        return string.Join("\n", lineList.Where(line => !string.IsNullOrEmpty(line)));
    }

    /// <summary>
    /// 去掉空段后用中文逗号接起来。
    /// </summary>
    private static string JoinParts(params string[] partList)
    {
        return string.Join("，", partList.Where(part => !string.IsNullOrEmpty(part)));
    }
}
