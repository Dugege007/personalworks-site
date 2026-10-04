namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 卡片预览分流：图像开窗口灯箱，视频走外部播放器。
/// </summary>
public enum MediaPreviewKind
{
    Lightbox,
    ExternalPlayer
}

/// <summary>
/// 预览动作与外放源文件选取。
/// </summary>
public static class MediaPreviewRules
{
    /// <summary>
    /// 按路径判定预览方式。
    /// </summary>
    public static MediaPreviewKind KindForPath(string? pathOrName)
    {
        return MediaPathRules.IsVideoFile(pathOrName)
            ? MediaPreviewKind.ExternalPlayer
            : MediaPreviewKind.Lightbox;
    }

    /// <summary>
    /// 外部播放优先中转站源片；缺失则用正式位成片。
    /// </summary>
    public static string? SourcePath(string? stageFullPath, string? previewPath)
    {
        if (!string.IsNullOrWhiteSpace(stageFullPath) && File.Exists(stageFullPath))
        {
            return stageFullPath;
        }

        if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
        {
            return previewPath;
        }

        return null;
    }
}
