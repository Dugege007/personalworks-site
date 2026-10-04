using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 查看序「按时间」：有拍摄时间用拍摄时间，否则用资源创建时间。
/// </summary>
public static class ResourceSortTime
{
    /// <summary>
    /// 拍摄时间为空时退到创建时间。两者都空则空，比较时排在实有时间之后。
    /// </summary>
    public static DateTime? Choose(DateTime? capturedUtc, DateTime? createdUtc)
    {
        return capturedUtc ?? createdUtc;
    }
}

/// <summary>
/// 解析 EXIF 里的拍摄时间文本。结果视为本机本地时间。
/// </summary>
public static class ExifDateTime
{
    /// <summary>
    /// 识别 <c>yyyy:MM:dd HH:mm:ss</c>，其次按当前区域格式。
    /// </summary>
    public static bool TryParseLocal(string? text, out DateTime local)
    {
        local = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().TrimEnd('\0');
        if (DateTime.TryParseExact(
            trimmed,
            new[] { "yyyy:MM:dd HH:mm:ss", "yyyy:MM:dd HH:mm:ss.fff" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var exif))
        {
            local = DateTime.SpecifyKind(exif, DateTimeKind.Local);
            return true;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            local = parsed.Kind == DateTimeKind.Utc
                ? parsed.ToLocalTime()
                : DateTime.SpecifyKind(parsed, DateTimeKind.Local);
            return true;
        }

        return false;
    }
}
