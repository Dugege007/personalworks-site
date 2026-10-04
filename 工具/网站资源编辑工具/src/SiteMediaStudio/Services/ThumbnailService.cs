using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 后台线程解码预览图，网格缩略图写入 EXE 旁 thumbs 缓存。
/// </summary>
public sealed class ThumbnailService
{
    private readonly SemaphoreSlim mLimit = new(4);
    private readonly ConcurrentDictionary<string, BitmapSource> mCacheDict = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 按目标宽度解码；decodePx 为 0 时按原像素。失败返回空。
    /// </summary>
    public async Task<BitmapSource?> LoadAsync(
        string fullPath,
        int decodePx,
        CancellationToken token,
        string? coverIdentity = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        var writeUtc = File.GetLastWriteTimeUtc(fullPath);
        var cacheKey = ThumbMemoryKey.Build(fullPath, decodePx, writeUtc);
        if (mCacheDict.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        await mLimit.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            return await Task.Run(() => Decode(fullPath, decodePx, writeUtc, cacheKey, coverIdentity), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            mLimit.Release();
        }
    }

    /// <summary>
    /// 清空内存中的已解码图，不删磁盘缓存。
    /// </summary>
    public void ClearMemory()
    {
        mCacheDict.Clear();
    }

    /// <summary>
    /// 丢掉某条源文件的内存与 thumbs 磁盘缓存，便于改封面后重解。
    /// </summary>
    public void Forget(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        foreach (var key in mCacheDict.Keys)
        {
            if (key.StartsWith(fullPath + "|", StringComparison.OrdinalIgnoreCase))
            {
                mCacheDict.TryRemove(key, out _);
            }
        }

        if (!File.Exists(fullPath))
        {
            return;
        }

        var writeUtc = File.GetLastWriteTimeUtc(fullPath);
        foreach (var decodePx in EnumerateForgetPx())
        {
            try
            {
                var disk = ThumbCacheStore.ResolveFile(fullPath, decodePx, writeUtc);
                if (File.Exists(disk))
                {
                    File.Delete(disk);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// 网格边长步进与检视清晰图边长。
    /// </summary>
    private static IEnumerable<int> EnumerateForgetPx()
    {
        yield return 0;
        yield return 1600;
        for (var px = 120; px <= 480; px += 20)
        {
            yield return px;
        }
    }

    /// <summary>
    /// 在工作线程读盘解码并冻结，供 UI 线程直接绑定。
    /// </summary>
    private BitmapSource? Decode(
        string fullPath,
        int decodePx,
        DateTime writeUtc,
        string cacheKey,
        string? coverIdentity)
    {
        if (mCacheDict.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        if (decodePx > 0)
        {
            var diskPath = ThumbCacheStore.ResolveFile(fullPath, decodePx, writeUtc);
            var fromDisk = TryLoadJpeg(diskPath);
            if (fromDisk != null)
            {
                mCacheDict[cacheKey] = fromDisk;
                return fromDisk;
            }
        }

        if (MediaPathRules.IsVideoFile(fullPath))
        {
            var videoFrame = DecodeVideo(fullPath, decodePx, writeUtc, coverIdentity);
            if (videoFrame == null)
            {
                return null;
            }

            if (decodePx > 0)
            {
                TrySaveJpeg(videoFrame, ThumbCacheStore.ResolveFile(fullPath, decodePx, writeUtc));
            }

            mCacheDict[cacheKey] = videoFrame;
            return videoFrame;
        }

        using var stream = File.OpenRead(fullPath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
        {
            return null;
        }

        BitmapSource frame = decoder.Frames[0];
        if (decodePx > 0 && frame.PixelWidth > decodePx)
        {
            var scale = (double)decodePx / frame.PixelWidth;
            var scaled = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
            scaled.Freeze();
            frame = scaled;
        }
        else if (!frame.IsFrozen)
        {
            frame.Freeze();
        }

        if (decodePx > 0)
        {
            TrySaveJpeg(frame, ThumbCacheStore.ResolveFile(fullPath, decodePx, writeUtc));
        }

        mCacheDict[cacheKey] = frame;
        return frame;
    }

    /// <summary>
    /// 先读手设封面缓存，再 FFmpeg 默认帧，最后退资源管理器缩略图。
    /// </summary>
    private static BitmapSource? DecodeVideo(string fullPath, int decodePx, DateTime writeUtc, string? coverIdentity)
    {
        if (!string.IsNullOrWhiteSpace(coverIdentity))
        {
            var length = new FileInfo(fullPath).Length;
            var hit = VideoCoverStore.TryRead(coverIdentity, length, writeUtc);
            if (hit != null)
            {
                var cached = TryLoadJpeg(hit.ImagePath);
                if (cached != null)
                {
                    return ScaleIfNeeded(cached, decodePx);
                }
            }
        }

        var jpegBytes = FfmpegFrameExtractor.TryExtractJpeg(fullPath, VideoCoverRules.DefaultPositionSec(null));
        if (jpegBytes is { Length: > 0 })
        {
            using var stream = new MemoryStream(jpegBytes);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                return ScaleIfNeeded(decoder.Frames[0], decodePx);
            }
        }

        var shellFrame = ShellVideoFrame.TryExtract(fullPath, decodePx > 0 ? decodePx : 1280);
        return shellFrame == null ? null : ScaleIfNeeded(shellFrame, decodePx);
    }

    /// <summary>
    /// 按目标宽度缩小并冻结。
    /// </summary>
    private static BitmapSource ScaleIfNeeded(BitmapSource frame, int decodePx)
    {
        if (decodePx > 0 && frame.PixelWidth > decodePx)
        {
            var scale = (double)decodePx / frame.PixelWidth;
            var scaled = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
            scaled.Freeze();
            return scaled;
        }

        if (!frame.IsFrozen)
        {
            frame.Freeze();
        }

        return frame;
    }

    /// <summary>
    /// 读取已缓存的 JPEG；损坏或不存在时返回空。
    /// </summary>
    private static BitmapSource? TryLoadJpeg(string cachePath)
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(cachePath);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return null;
            }

            var frame = decoder.Frames[0];
            if (!frame.IsFrozen)
            {
                frame.Freeze();
            }

            return frame;
        }
        catch (IOException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// 把网格缩略图写成 JPEG；写盘失败不影响预览。
    /// </summary>
    private static void TrySaveJpeg(BitmapSource source, string cachePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var output = File.Create(cachePath);
            encoder.Save(output);
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
    }
}
