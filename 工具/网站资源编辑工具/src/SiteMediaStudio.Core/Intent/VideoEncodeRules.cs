namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 视频上页压码与伴生封面键。
/// </summary>
public static class VideoEncodeRules
{
    public const int DefaultAudioBitrateKbps = 128;
    public const int MinAudioBitrateKbps = 128;
    public const int MaxAudioBitrateKbps = 192;
    public const int PosterMaxSide = 1280;
    public const string MissingEncoderMessage = "找不到可用的视频编码器（须 MediaCoder 命令行或 FFmpeg）。";
    public const string MissingPosterMessage = "无法生成视频封面（须默认帧或手设封面）。";

    /// <summary>
    /// PersonalWorks 视频须在入库前压成网页 MP4。
    /// </summary>
    public static bool NeedsEncode(StageItem item, WorkspaceProfile? profile)
    {
        if (!string.Equals(
                profile?.SiteCatalog.Kind,
                "personalworks-ts",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (CatalogNotice.IsConstructionChannel(item.ChannelKey))
        {
            return false;
        }

        return MediaPathRules.IsVideoFile(item.FullPath ?? item.StageRel);
    }

    /// <summary>
    /// 正式位视频对象键一律 <c>.mp4</c>。
    /// </summary>
    public static bool IsFormalVideoObject(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || IsPosterObject(objectKey))
        {
            return false;
        }

        return string.Equals(Path.GetExtension(objectKey), ".mp4", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 伴生封面对象键。
    /// </summary>
    public static bool IsPosterObject(string? objectKey)
    {
        return !string.IsNullOrWhiteSpace(objectKey)
            && objectKey.EndsWith(".poster.webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <c>{nn}.mp4</c> → <c>{nn}.poster.webp</c>。
    /// </summary>
    public static string PosterObjectKey(string videoObjectKey)
    {
        var rel = JsonUtil.ToRel(videoObjectKey);
        var slash = rel.LastIndexOf('/');
        var name = slash < 0 ? rel : rel[(slash + 1)..];
        var stem = Path.GetFileNameWithoutExtension(name);
        var poster = stem + ".poster.webp";
        return slash < 0 ? poster : rel[..(slash + 1)] + poster;
    }

    /// <summary>
    /// 成片相对路径，写入投放箱旁 <c>.site-ready</c>。
    /// </summary>
    public static string PreparedStageRel(string sourceStageRel, string? workId, string? objectKey)
    {
        return PreparedRel(sourceStageRel, workId, objectKey, ".mp4");
    }

    /// <summary>
    /// 伴生封面相对路径。
    /// </summary>
    public static string PreparedPosterRel(string sourceStageRel, string? workId, string? objectKey)
    {
        var videoRel = PreparedStageRel(sourceStageRel, workId, objectKey);
        return Path.ChangeExtension(videoRel, null) + ".poster.webp";
    }

    /// <summary>
    /// 由已写成片路径推导封面路径。
    /// </summary>
    public static string PosterRelFromPrepared(string preparedVideoRel)
    {
        var rel = JsonUtil.ToRel(preparedVideoRel);
        return Path.ChangeExtension(rel, null) + ".poster.webp";
    }

    /// <summary>
    /// 音频码率：缺省 128；超出 128～192 则拒绝。
    /// </summary>
    public static bool TryAudioBitrate(int? kbps, out int value, out string? rejectText)
    {
        var raw = kbps is null or 0 ? DefaultAudioBitrateKbps : kbps.Value;
        if (raw < MinAudioBitrateKbps || raw > MaxAudioBitrateKbps)
        {
            value = DefaultAudioBitrateKbps;
            rejectText = $"音频码率须为 {MinAudioBitrateKbps}～{MaxAudioBitrateKbps} kbps。";
            return false;
        }

        value = raw;
        rejectText = null;
        return true;
    }

    /// <summary>
    /// 与压图成片相同的目录规则，仅扩展名不同。
    /// </summary>
    private static string PreparedRel(string sourceStageRel, string? workId, string? objectKey, string extension)
    {
        var source = JsonUtil.ToRel(sourceStageRel);
        var slash = source.LastIndexOf('/');
        var parent = slash < 0 ? "" : source[..slash];
        var slot = Path.GetFileNameWithoutExtension(
            (objectKey ?? Path.GetFileName(source)).Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(slot) || slot.EndsWith(".poster", StringComparison.OrdinalIgnoreCase))
        {
            slot = "video";
        }

        var project = string.IsNullOrWhiteSpace(workId) ? "ready" : workId.Trim();
        var rel = string.IsNullOrEmpty(parent)
            ? $".site-ready/{project}/{slot}{extension}"
            : $"{parent}/.site-ready/{project}/{slot}{extension}";
        return JsonUtil.ToRel(rel);
    }
}
