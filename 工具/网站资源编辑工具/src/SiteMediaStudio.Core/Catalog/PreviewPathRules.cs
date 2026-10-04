namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 预览文件选取：有正式位 / 网页图则优先，否则用投放箱原片。
/// </summary>
public static class PreviewPathRules
{
    /// <summary>
    /// 返回第一个存在的路径。
    /// </summary>
    public static string? PreferWebThenOriginal(string? webFullPath, string? originalFullPath)
    {
        if (!string.IsNullOrWhiteSpace(webFullPath) && File.Exists(webFullPath))
        {
            return webFullPath;
        }

        if (!string.IsNullOrWhiteSpace(originalFullPath) && File.Exists(originalFullPath))
        {
            return originalFullPath;
        }

        return null;
    }
}
