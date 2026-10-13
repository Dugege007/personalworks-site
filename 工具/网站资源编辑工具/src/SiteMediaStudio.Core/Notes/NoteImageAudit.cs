namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 只读检查一篇心得的配图：哪些超过大小上限，哪些被正文引用但文件不存在，哪些引用落到笔记文件夹外面。
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
        var references = ListReferences(folder, ReadBody(folder));
        return new NoteImageAuditReport
        {
            OverLimitList = ListOverLimit(folder, limits),
            MissingList = references.MissingList,
            OutsideList = references.OutsideList
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
    /// 列出正文里的缺失配图，以及落到笔记文件夹外面的引用。
    /// 遇到问题不会停。远程地址两者都不算。文件夹外的引用即使文件不存在，也不再记成缺失。
    /// </summary>
    private static (List<NoteMissingImage> MissingList, List<NoteOutsideReference> OutsideList) ListReferences(
        string folderFullPath,
        string body)
    {
        var missingList = new List<NoteMissingImage>();
        var outsideList = new List<NoteOutsideReference>();
        var seenMissingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOutsideSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in NoteRules.ListImageTargets(body))
        {
            NoteImageLocation location;
            try
            {
                location = NoteImageLocation.Resolve(folderFullPath, raw);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                continue;
            }

            if (location.Kind == NoteImageLocationKind.Outside)
            {
                if (seenOutsideSet.Add(location.FullPath))
                {
                    outsideList.Add(new NoteOutsideReference
                    {
                        Reference = location.Reference
                    });
                }

                continue;
            }

            if (location.Kind != NoteImageLocationKind.Inside
                || MediaPathRules.IsNoteAuxiliaryRel(location.RelativePath)
                || !IsStoredImageReference(location.Reference)
                || File.Exists(location.FullPath)
                || !seenMissingSet.Add(location.FullPath))
            {
                continue;
            }

            missingList.Add(new NoteMissingImage
            {
                Reference = location.Reference,
                RelativePath = location.RelativePath
            });
        }

        return (missingList, outsideList);
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
    /// 正文引用落到笔记文件夹外面的地址，按正文中第一次出现的顺序排列。同一路径只列一次。
    /// </summary>
    public IReadOnlyList<NoteOutsideReference> OutsideList { get; init; } = Array.Empty<NoteOutsideReference>();

    /// <summary>
    /// 超限配图的张数。
    /// </summary>
    public int OverLimitCount => OverLimitList.Count;

    /// <summary>
    /// 缺失配图的张数。
    /// </summary>
    public int MissingCount => MissingList.Count;

    /// <summary>
    /// 落到笔记文件夹外面的引用条数。
    /// </summary>
    public int OutsideCount => OutsideList.Count;

    /// <summary>
    /// 有缺失配图或文件夹外引用时，这篇笔记不能上页。超限本身不禁止上页。
    /// </summary>
    public bool BlocksPublish => MissingCount > 0 || OutsideCount > 0;
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

/// <summary>
/// 正文引用了笔记文件夹外面的一张图片。文件是否存在都不改变这一分类。
/// </summary>
public sealed class NoteOutsideReference
{
    /// <summary>
    /// 正文里写下的地址。
    /// </summary>
    public string Reference { get; init; } = "";
}
