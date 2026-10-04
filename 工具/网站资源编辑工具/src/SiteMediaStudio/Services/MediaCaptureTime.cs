using System.IO;
using System.Windows.Media.Imaging;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 读取资源时间：图像拍摄时间优先，没有则用文件或目录的创建时间。
/// </summary>
public static class MediaCaptureTime
{
    /// <summary>
    /// 返回 UTC。路径缺失或读不到时间时为空。
    /// </summary>
    public static DateTime? ResolveUtc(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (Directory.Exists(path))
            {
                return Directory.GetCreationTimeUtc(path);
            }

            if (!File.Exists(path))
            {
                return null;
            }

            return ResourceSortTime.Choose(TryReadTakenUtc(path), File.GetCreationTimeUtc(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 只读图像元数据里的拍摄时间，不解码像素。
    /// </summary>
    private static DateTime? TryReadTakenUtc(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0 || decoder.Frames[0].Metadata is not BitmapMetadata meta)
            {
                return null;
            }

            if (TryQueryTaken(meta, out var taken))
            {
                return ToUtc(taken);
            }

            if (ExifDateTime.TryParseLocal(ReadDateTakenText(meta), out var local))
            {
                return local.ToUniversalTime();
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (FileFormatException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }

        return null;
    }

    /// <summary>
    /// 读取 Windows 成像属性里的拍摄时间。
    /// </summary>
    private static bool TryQueryTaken(BitmapMetadata meta, out DateTime taken)
    {
        taken = default;
        if (!meta.ContainsQuery("System.Photo.DateTaken"))
        {
            return false;
        }

        switch (meta.GetQuery("System.Photo.DateTaken"))
        {
            case DateTime value:
                taken = value;
                return true;
            case string text when ExifDateTime.TryParseLocal(text, out var local):
                taken = local;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 读取 <c>DateTaken</c> 文本。该属性在部分格式上会抛出不支持。
    /// </summary>
    private static string? ReadDateTakenText(BitmapMetadata meta)
    {
        try
        {
            return meta.DateTaken;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// 未标明种类的时间按本机本地时间换到 UTC。
    /// </summary>
    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
