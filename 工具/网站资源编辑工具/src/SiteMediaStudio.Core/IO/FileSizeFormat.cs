using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 文件大小按 1024 进位：KB 用整数，超过 1024 KB 用 MB，超过 1024 MB 用 GB。
/// </summary>
public static class FileSizeFormat
{
    private const long BytesPerKb = 1024;
    private const long BytesPerMb = BytesPerKb * 1024;
    private const long BytesPerGb = BytesPerMb * 1024;

    /// <summary>
    /// 把字节数写成界面用的大小。不足 1KB 且大于 0 时记为 1 KB。
    /// </summary>
    public static string Format(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 KB";
        }

        if (bytes > 1024 * BytesPerMb)
        {
            return FormatScaled(bytes, BytesPerGb) + " GB";
        }

        if (bytes > 1024 * BytesPerKb)
        {
            return FormatScaled(bytes, BytesPerMb) + " MB";
        }

        var kb = (bytes + 1023) / 1024;
        return kb + " KB";
    }

    /// <summary>
    /// 最多两位小数，末尾 0 不写。
    /// </summary>
    private static string FormatScaled(long bytes, long unit)
    {
        var text = (bytes / (double)unit).ToString("0.00", CultureInfo.InvariantCulture);
        return text.TrimEnd('0').TrimEnd('.');
    }
}
