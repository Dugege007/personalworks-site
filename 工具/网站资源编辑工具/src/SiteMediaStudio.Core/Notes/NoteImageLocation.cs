namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 正文图片地址落在笔记文件夹内、文件夹外，还是远程地址。
/// </summary>
public enum NoteImageLocationKind
{
    /// <summary>
    /// 解析后仍在这篇笔记文件夹内。
    /// </summary>
    Inside = 0,

    /// <summary>
    /// 以 http:// 或 https:// 开头。不在本机查找文件。
    /// </summary>
    Remote,

    /// <summary>
    /// 解析后落到这篇笔记文件夹外面。包括以 .. 跳出文件夹，以及以 / 开头的地址。
    /// </summary>
    Outside
}

/// <summary>
/// 把正文里的一个图片地址解析成笔记文件夹内的位置。
/// 上页预演和配图体检共用这一处，避免两套判定。
/// </summary>
public readonly record struct NoteImageLocation(
    string Reference,
    NoteImageLocationKind Kind,
    string FullPath,
    string RelativePath)
{
    /// <summary>
    /// 解析一个正文地址。百分号编码不完整时保留原文，不因此中断后面的引用。
    /// 路径里含有系统不接受的字符时，仍抛出与原来相同的异常。
    /// </summary>
    public static NoteImageLocation Resolve(string folderFullPath, string raw)
    {
        var reference = raw.Trim();
        if (IsRemote(reference))
        {
            return new NoteImageLocation(reference, NoteImageLocationKind.Remote, "", "");
        }

        var decoded = DecodePercent(reference);
        var fullPath = Path.GetFullPath(Path.Combine(
            folderFullPath,
            decoded.Replace('/', Path.DirectorySeparatorChar)));
        var folderPath = Path.GetFullPath(folderFullPath);
        if (!IsInsideFolder(fullPath, folderPath))
        {
            return new NoteImageLocation(reference, NoteImageLocationKind.Outside, fullPath, "");
        }

        var relativePath = JsonUtil.ToRel(Path.GetRelativePath(folderPath, fullPath));
        return new NoteImageLocation(reference, NoteImageLocationKind.Inside, fullPath, relativePath);
    }

    /// <summary>
    /// 地址是否为网页链接。
    /// </summary>
    public static bool IsRemote(string reference)
    {
        return reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 完整路径是否仍在笔记文件夹内。比较方式与上页预演原来的前缀判断相同。
    /// </summary>
    private static bool IsInsideFolder(string fullPath, string folderFullPath)
    {
        return fullPath.StartsWith(folderFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, folderFullPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 还原百分号编码。编码不完整时保留原文。
    /// </summary>
    public static string DecodePercent(string reference)
    {
        try
        {
            return Uri.UnescapeDataString(reference);
        }
        catch (UriFormatException)
        {
            return reference;
        }
    }
}
