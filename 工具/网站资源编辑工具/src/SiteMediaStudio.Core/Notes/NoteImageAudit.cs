namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 只读检查一篇心得的配图：哪些超过大小上限，哪些被正文引用但文件不存在。
/// 不修改投放箱，也不改变上页预演。上页预演仍然在第一张有问题的引用处停止。
/// </summary>
public static class NoteImageAudit
{
    /// <summary>
    /// 用工作区共享配置里的阈值检查一篇笔记。配置文件不存在时使用默认阈值。配置损坏时抛出异常，不用默认值顶上。
    /// </summary>
    public static NoteImageAuditReport Inspect(string folderFullPath, WorkspaceProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        return Inspect(folderFullPath, NoteImageLimitsStore.Load(profile));
    }

    /// <summary>
    /// 用给定阈值检查一篇笔记。普通配图和 GIF 各自使用自己的上限。等于上限不算超限。
    /// </summary>
    public static NoteImageAuditReport Inspect(string folderFullPath, NoteImageLimits limits)
    {
        if (string.IsNullOrWhiteSpace(folderFullPath))
        {
            throw new ArgumentException("笔记文件夹路径不能为空。", nameof(folderFullPath));
        }

        if (limits == null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        var folder = Path.GetFullPath(folderFullPath);
        return new NoteImageAuditReport
        {
            OverLimitList = ListOverLimit(folder, limits),
            MissingList = ListMissing(folder, ReadBody(folder))
        };
    }

    /// <summary>
    /// 列出夹内超过上限的配图。网页成品和原图备份不参与统计。
    /// </summary>
    private static List<NoteOverLimitImage> ListOverLimit(string folderFullPath, NoteImageLimits limits)
    {
        var overLimitList = new List<NoteOverLimitImage>();
        if (!Directory.Exists(folderFullPath))
        {
            return overLimitList;
        }

        foreach (var file in Directory.EnumerateFiles(folderFullPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = JsonUtil.ToRel(Path.GetRelativePath(folderFullPath, file));
            if (MediaPathRules.IsNoteAuxiliaryRel(relativePath))
            {
                continue;
            }

            if (!NoteRules.StoredImageExtSet.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            var bytes = new FileInfo(file).Length;
            if (!NoteImageThreshold.IsOverLimit(bytes, file, limits))
            {
                continue;
            }

            overLimitList.Add(new NoteOverLimitImage
            {
                RelativePath = relativePath,
                FullPath = file,
                Bytes = bytes,
                MaxBytes = NoteImageThreshold.MaxBytesFor(file, limits.ImageMaxBytes, limits.GifMaxBytes)
            });
        }

        overLimitList.Sort((left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return overLimitList;
    }

    /// <summary>
    /// 列出正文引用了但文件不存在的全部配图。遇到缺失不会停，远程地址不算缺失。
    /// </summary>
    private static List<NoteMissingImage> ListMissing(string folderFullPath, string body)
    {
        var missingList = new List<NoteMissingImage>();
        var seenFullPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in NoteRules.ListImageTargets(body))
        {
            if (!TryResolveStoredImage(folderFullPath, raw, out var reference, out var fullPath, out var relativePath))
            {
                continue;
            }

            if (MediaPathRules.IsNoteAuxiliaryRel(relativePath))
            {
                continue;
            }

            if (File.Exists(fullPath))
            {
                continue;
            }

            if (!seenFullPathSet.Add(fullPath))
            {
                continue;
            }

            missingList.Add(new NoteMissingImage
            {
                Reference = reference,
                RelativePath = relativePath
            });
        }

        return missingList;
    }

    /// <summary>
    /// 读取正文。有前言时只检查前言之后的内容，与上页预演使用的正文范围相同。没有正文时返回空字符串。
    /// </summary>
    private static string ReadBody(string folderFullPath)
    {
        var bodyPath = Path.Combine(folderFullPath, NoteRules.BodyFileName);
        if (!File.Exists(bodyPath))
        {
            return "";
        }

        var source = File.ReadAllText(bodyPath);
        if (NoteRules.TryReadFrontmatter(source, out _, out var stripped, out _))
        {
            return stripped;
        }

        return source;
    }

    /// <summary>
    /// 把正文里的一个地址解析成笔记文件夹内的配图。远程地址，以及不是配图的地址，返回假。
    /// </summary>
    private static bool TryResolveStoredImage(
        string folderFullPath,
        string raw,
        out string reference,
        out string fullPath,
        out string relativePath)
    {
        reference = raw.Trim();
        fullPath = "";
        relativePath = "";
        if (reference.Length == 0 || IsRemote(reference) || !IsStoredImageReference(reference))
        {
            return false;
        }

        var decoded = DecodeReference(reference);
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(
                folderFullPath,
                decoded.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }

        relativePath = JsonUtil.ToRel(Path.GetRelativePath(folderFullPath, fullPath));
        return true;
    }

    /// <summary>
    /// 地址是否为网页链接。这类地址不在本机查找文件。
    /// </summary>
    private static bool IsRemote(string reference)
    {
        return reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 地址是否属于心得允许存放的配图。判断扩展名时去掉网址里的查询参数和片段，并且不区分大小写。
    /// </summary>
    private static bool IsStoredImageReference(string reference)
    {
        var text = reference;
        var query = text.IndexOf('?');
        var hash = text.IndexOf('#');
        var cut = query < 0 ? hash : hash < 0 ? query : Math.Min(query, hash);
        if (cut >= 0)
        {
            text = text[..cut];
        }

        return NoteRules.StoredImageExtSet.Contains(Path.GetExtension(text));
    }

    /// <summary>
    /// 还原地址里的百分号编码。编码不完整时保留原文，避免一张坏引用挡住后面的缺失配图。
    /// </summary>
    private static string DecodeReference(string reference)
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

/// <summary>
/// 一篇笔记的配图体检结果。只描述当前磁盘和正文。
/// </summary>
public sealed class NoteImageAuditReport
{
    /// <summary>
    /// 超过大小上限的配图，按相对路径排序。
    /// </summary>
    public IReadOnlyList<NoteOverLimitImage> OverLimitList { get; init; } = Array.Empty<NoteOverLimitImage>();

    /// <summary>
    /// 正文引用了但文件不存在的配图，按正文中第一次出现的顺序排列。同一文件只列一次。
    /// </summary>
    public IReadOnlyList<NoteMissingImage> MissingList { get; init; } = Array.Empty<NoteMissingImage>();

    /// <summary>
    /// 超限配图的张数。
    /// </summary>
    public int OverLimitCount => OverLimitList.Count;

    /// <summary>
    /// 缺失配图的张数。
    /// </summary>
    public int MissingCount => MissingList.Count;
}

/// <summary>
/// 一张超过大小上限的配图。
/// </summary>
public sealed class NoteOverLimitImage
{
    /// <summary>
    /// 相对这篇笔记文件夹的路径。子文件夹会保留。
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 本机上的完整路径。
    /// </summary>
    public string FullPath { get; init; } = "";

    /// <summary>
    /// 文件大小，单位是字节。
    /// </summary>
    public long Bytes { get; init; }

    /// <summary>
    /// 这张配图适用的上限，单位是字节。GIF 用 GIF 上限，其余用普通配图上限。
    /// </summary>
    public long MaxBytes { get; init; }
}

/// <summary>
/// 正文引用了、但本机上不存在的一张配图。
/// </summary>
public sealed class NoteMissingImage
{
    /// <summary>
    /// 正文里写下的地址。
    /// </summary>
    public string Reference { get; init; } = "";

    /// <summary>
    /// 相对这篇笔记文件夹的路径。子文件夹会保留，供界面列出文件名。
    /// </summary>
    public string RelativePath { get; init; } = "";
}
