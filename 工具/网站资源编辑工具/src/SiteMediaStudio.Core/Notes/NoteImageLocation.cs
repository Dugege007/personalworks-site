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
    /// <paramref name="noteSlug"/> 有值时，会把已是站点态的
    /// <c>notes/&lt;slug&gt;/...</c>（可叠多层、可带前导 /）收成夹内相对路径再解析，
    /// 避免把站点前缀再拼进心得夹导致叠层。
    /// </summary>
    public static NoteImageLocation Resolve(string folderFullPath, string raw, string? noteSlug = null)
    {
        var reference = raw.Trim();
        if (IsRemote(reference))
        {
            return new NoteImageLocation(reference, NoteImageLocationKind.Remote, "", "");
        }

        var decoded = DecodePercent(reference);
        var lookup = decoded;
        if (!string.IsNullOrWhiteSpace(noteSlug)
            && TryStripSiteNotesPath(decoded, noteSlug, out var stripped))
        {
            lookup = stripped;
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            folderFullPath,
            lookup.Replace('/', Path.DirectorySeparatorChar)));
        var folderPath = Path.GetFullPath(folderFullPath);
        if (!IsInsideFolder(fullPath, folderPath))
        {
            return new NoteImageLocation(reference, NoteImageLocationKind.Outside, fullPath, "");
        }

        if (!File.Exists(fullPath)
            && !string.IsNullOrWhiteSpace(noteSlug)
            && TryFindPublishableAlternate(fullPath, out var alternate))
        {
            fullPath = alternate;
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
    /// 去掉重复的 <c>notes/&lt;slug&gt;/</c> 前缀（含前导 /），得到夹内相对路径或空串。
    /// 不是该 slug 的站点态时返回原路径（仍去掉首尾空白并把 \ 收成 /）。
    /// </summary>
    public static string CollapseNotesSlugPrefixes(string? pathOrKey, string slug)
    {
        var text = NormalizeSlashes(pathOrKey);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return text;
        }

        var prefix = NoteRules.ChannelKey + "/" + slug.Trim() + "/";
        while (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            text = text[prefix.Length..];
        }

        return text;
    }

    /// <summary>
    /// 把夹内相对路径或已是站点态的地址收成单层 <c>notes/&lt;slug&gt;/...</c>，不会叠多层。
    /// </summary>
    public static string ToSiteObjectKey(string slug, string? pathOrKey)
    {
        var rest = CollapseNotesSlugPrefixes(pathOrKey, slug);
        if (string.IsNullOrWhiteSpace(rest))
        {
            return NoteRules.ChannelKey + "/" + slug.Trim();
        }

        return NoteRules.ChannelKey + "/" + slug.Trim() + "/" + rest;
    }

    /// <summary>
    /// 是否为该 slug 的站点态配图路径（可叠多层、可带前导 /）。
    /// </summary>
    public static bool TryStripSiteNotesPath(string? pathOrKey, string slug, out string relativeInsideFolder)
    {
        relativeInsideFolder = "";
        var text = NormalizeSlashes(pathOrKey);
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var prefix = NoteRules.ChannelKey + "/" + slug.Trim() + "/";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativeInsideFolder = CollapseNotesSlugPrefixes(text, slug);
        return relativeInsideFolder.Length > 0;
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

    private static string NormalizeSlashes(string? pathOrKey)
    {
        return (pathOrKey ?? "").Trim().Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    /// 站点态常为 .webp，中转站源文件可能仍是 png／jpg。同主名且可上页的扩展名视为同一张图。
    /// </summary>
    private static bool TryFindPublishableAlternate(string fullPath, out string alternate)
    {
        alternate = "";
        var dir = Path.GetDirectoryName(fullPath);
        var stem = Path.GetFileNameWithoutExtension(fullPath);
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(stem))
        {
            return false;
        }

        foreach (var ext in NoteRules.PublishImageExtSet)
        {
            var candidate = Path.Combine(dir, stem + ext);
            if (string.Equals(candidate, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.Exists(candidate))
            {
                alternate = Path.GetFullPath(candidate);
                return true;
            }
        }

        return false;
    }
}
