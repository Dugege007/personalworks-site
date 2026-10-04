namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// PersonalWorks 上页压图规格：原片派生 WebP，不覆盖源图。
/// </summary>
public static class WebpPrepareRules
{
    public const int DefaultMaxSide = 2560;
    public const int DefaultMinSide = 1920;
    public const int ProfileMaxSide = 1600;
    public const int ProfileMinSide = 1200;
    public const int NoteMaxSide = 1600;
    public const int NoteMinSide = 320;

    /// <summary>
    /// 施工图不缩小：脚本 <c>--max-side 0</c>。
    /// </summary>
    public const int KeepSourceSide = 0;

    /// <summary>
    /// 是否须在入库前压成网页规格 WebP。
    /// </summary>
    public static bool NeedsPrepare(StageItem item, WorkspaceProfile? profile)
    {
        if (!string.Equals(
                profile?.SiteCatalog.Kind,
                "personalworks-ts",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (item.IsPack
            || MediaPathRules.IsVideoFile(item.FullPath ?? item.StageRel)
            || MediaPathRules.IsAudioFile(item.FullPath ?? item.StageRel))
        {
            return false;
        }

        var extension = Path.GetExtension(item.FullPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = Path.GetExtension(item.StageRel);
        }

        return !string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 上页对象键扩展名：视频统一 MP4；须压图时统一为 WebP。
    /// </summary>
    public static string AllocateExtension(StageItem item, WorkspaceProfile? profile)
    {
        if (MediaPathRules.IsVideoFile(item.FullPath ?? item.StageRel))
        {
            return ".mp4";
        }

        return NeedsPrepare(item, profile) ? ".webp" : Path.GetExtension(item.StageRel);
    }

    /// <summary>
    /// 与 <c>prepare-initial-batch.py</c> 相同的成片相对路径。
    /// </summary>
    public static string PreparedStageRel(string sourceStageRel, string? workId, string? objectKey)
    {
        var source = JsonUtil.ToRel(sourceStageRel);
        var slash = source.LastIndexOf('/');
        var parent = slash < 0 ? "" : source[..slash];
        var slot = Path.GetFileNameWithoutExtension(
            (objectKey ?? Path.GetFileName(source)).Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(slot))
        {
            slot = "image";
        }

        var project = string.IsNullOrWhiteSpace(workId) ? "ready" : workId.Trim();
        var rel = string.IsNullOrEmpty(parent)
            ? $".site-ready/{project}/{slot}.webp"
            : $"{parent}/.site-ready/{project}/{slot}.webp";
        return JsonUtil.ToRel(rel);
    }

    /// <summary>
    /// 施工图有损 WebP 质量。保持源图像素，不按体积再降。
    /// </summary>
    public const int ConstructionQuality = 20;

    /// <summary>
    /// 施工图不缩小。
    /// </summary>
    public static bool KeepsSourcePixels(string? channel)
    {
        return string.Equals(channel, CatalogNotice.ConstructionChannelKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// 施工图固定质量 20；其它栏目沿用脚本默认质量。
    /// </summary>
    public static int? FixedQualityFor(string? channel)
    {
        return KeepsSourcePixels(channel) ? ConstructionQuality : null;
    }

    /// <summary>
    /// 形象照长边约 1600；施工图保持源图像素；其余栏目沿用首轮批次 2560 / 1920。
    /// </summary>
    public static (int MaxSide, int MinSide) SidesFor(string? channel)
    {
        if (KeepsSourcePixels(channel))
        {
            return (KeepSourceSide, KeepSourceSide);
        }

        if (string.Equals(channel, "profile", StringComparison.Ordinal))
        {
            return (ProfileMaxSide, ProfileMinSide);
        }

        if (NoteRules.IsNotesChannel(channel))
        {
            return (NoteMaxSide, NoteMinSide);
        }

        return (DefaultMaxSide, DefaultMinSide);
    }
}
