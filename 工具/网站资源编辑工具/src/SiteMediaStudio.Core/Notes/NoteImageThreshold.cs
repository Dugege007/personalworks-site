using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 心得配图的大小阈值、超限判定，以及检视栏用的大小文案。
/// </summary>
public static class NoteImageThreshold
{
    /// <summary>
    /// 1 KB 等于 1024 字节，与检视栏的大小显示一致。
    /// </summary>
    public const long BytesPerKilobyte = 1024;

    /// <summary>
    /// 普通配图（JPG、PNG、WebP）的默认上限：500 KB。
    /// </summary>
    public const long DefaultImageMaxBytes = 500 * BytesPerKilobyte;

    /// <summary>
    /// GIF 的默认上限：2 MB。
    /// </summary>
    public const long DefaultGifMaxBytes = 2 * BytesPerKilobyte * BytesPerKilobyte;

    /// <summary>
    /// 文件大小严格大于阈值时为超限。等于阈值不算超限。
    /// </summary>
    public static bool IsOverLimit(long bytes, long maxBytes)
    {
        return bytes > maxBytes;
    }

    /// <summary>
    /// 按扩展名选择普通配图阈值或 GIF 阈值，再判断是否超限。
    /// </summary>
    public static bool IsOverLimit(long bytes, string? pathOrExtension, long imageMaxBytes, long gifMaxBytes)
    {
        return IsOverLimit(bytes, MaxBytesFor(pathOrExtension, imageMaxBytes, gifMaxBytes));
    }

    /// <summary>
    /// 按扩展名选择普通配图阈值或 GIF 阈值，再判断是否超限。
    /// </summary>
    public static bool IsOverLimit(long bytes, string? pathOrExtension, NoteImageLimits limits)
    {
        return IsOverLimit(bytes, pathOrExtension, limits.ImageMaxBytes, limits.GifMaxBytes);
    }

    /// <summary>
    /// GIF 使用 GIF 阈值，其余扩展名使用普通配图阈值。
    /// </summary>
    public static long MaxBytesFor(string? pathOrExtension, long imageMaxBytes, long gifMaxBytes)
    {
        return IsGif(pathOrExtension) ? gifMaxBytes : imageMaxBytes;
    }

    /// <summary>
    /// 扩展名或路径是否为 GIF。不区分大小写。
    /// </summary>
    public static bool IsGif(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return false;
        }

        var text = pathOrExtension.Trim();
        var extension = text.StartsWith('.') && text.IndexOf('/') < 0 && text.IndexOf('\\') < 0
            ? text
            : Path.GetExtension(text);
        return string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 超限时返回「超出上限 {超出大小}（上限 {阈值}）」。未超限时返回空字符串。大小格式与检视栏相同。
    /// </summary>
    public static string FormatOverLimit(long bytes, long maxBytes)
    {
        if (!IsOverLimit(bytes, maxBytes))
        {
            return "";
        }

        var over = bytes - maxBytes;
        return "超出上限 " + FileSizeFormat.Format(over) + "（上限 " + FileSizeFormat.Format(maxBytes) + "）";
    }

    /// <summary>
    /// 把字节数写成设置框里的 KB 文本。能整除时写整数，否则保留小数，避免把多余的字节抹掉。
    /// </summary>
    public static string FormatWholeKilobytes(long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), "字节数不能为负数。");
        }

        if (bytes % BytesPerKilobyte == 0)
        {
            return (bytes / BytesPerKilobyte).ToString(CultureInfo.InvariantCulture);
        }

        return (bytes / (double)BytesPerKilobyte).ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 把设置框里的整数 KB 换算成字节。空文本、小数和负数都不接受。
    /// </summary>
    public static bool TryParseWholeKilobytes(string? text, out long bytes, out NoteImageKilobyteParseFailure failure)
    {
        bytes = 0;
        failure = NoteImageKilobyteParseFailure.None;
        if (string.IsNullOrWhiteSpace(text)
            || !long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var kilobytes))
        {
            failure = NoteImageKilobyteParseFailure.NotInteger;
            return false;
        }

        try
        {
            bytes = checked(kilobytes * BytesPerKilobyte);
        }
        catch (OverflowException)
        {
            failure = NoteImageKilobyteParseFailure.Overflow;
            return false;
        }

        return true;
    }
}

/// <summary>
/// 设置框里的 KB 文本为什么不能换成字节。
/// </summary>
public enum NoteImageKilobyteParseFailure
{
    /// <summary>
    /// 文本可以换成字节。
    /// </summary>
    None = 0,

    /// <summary>
    /// 不是不小于 0 的整数。
    /// </summary>
    NotInteger,

    /// <summary>
    /// 整数太大，乘以 1024 后超出长整数范围。
    /// </summary>
    Overflow
}
