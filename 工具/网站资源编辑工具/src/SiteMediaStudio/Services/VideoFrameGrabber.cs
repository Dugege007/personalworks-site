using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 检视拖进度取帧：优先 FFmpeg 解到指定秒，缺编码器时才用 MediaPlayer。
/// </summary>
internal sealed class VideoFrameGrabber : IDisposable
{
    private MediaPlayer? mPlayer;
    private string? mSourcePath;
    private int mGrabGeneration;

    public double DurationSec { get; private set; }

    public int NaturalWidth { get; private set; }

    public int NaturalHeight { get; private set; }

    public bool CanSeek =>
        !string.IsNullOrWhiteSpace(mSourcePath)
        && DurationSec > 0
        && ((mPlayer != null && NaturalWidth > 0)
            || FfmpegFrameExtractor.ResolveFfmpeg(AppSettingsStore.Load().FfmpegPath) != null);

    /// <summary>
    /// 打开片源并读取时长。失败返回 false。
    /// </summary>
    public async Task<bool> OpenAsync(string path, CancellationToken token)
    {
        Close();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        mSourcePath = path;
        if (await OpenMediaPlayerAsync(path, token).ConfigureAwait(true))
        {
            return CanSeek;
        }

        var duration = VideoEncodeClient.TryReadDurationSec(path);
        if (duration is > 0 && FfmpegFrameExtractor.ResolveFfmpeg(AppSettingsStore.Load().FfmpegPath) != null)
        {
            DurationSec = duration.Value;
            return CanSeek;
        }

        mSourcePath = null;
        return false;
    }

    /// <summary>
    /// 停在指定秒并抓一帧。不能解码则空。
    /// </summary>
    public async Task<BitmapSource?> GrabAsync(double positionSec, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(mSourcePath) || DurationSec <= 0)
        {
            return null;
        }

        var clamped = Math.Clamp(positionSec, 0, DurationSec);
        var generation = Interlocked.Increment(ref mGrabGeneration);
        var path = mSourcePath;
        var ffmpegPath = AppSettingsStore.Load().FfmpegPath;
        byte[]? jpegBytes = null;
        try
        {
            jpegBytes = await Task.Run(
                () => FfmpegFrameExtractor.TryExtractJpeg(
                    path,
                    clamped,
                    ffmpegPath,
                    8_000,
                    accurate: true,
                    token),
                token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (token.IsCancellationRequested || generation != mGrabGeneration)
        {
            return null;
        }

        if (jpegBytes is { Length: > 0 })
        {
            return DecodeJpeg(jpegBytes);
        }

        return await GrabWithMediaPlayerAsync(clamped, token, generation).ConfigureAwait(true);
    }

    /// <summary>
    /// 释放当前片源。
    /// </summary>
    public void Close()
    {
        Interlocked.Increment(ref mGrabGeneration);
        if (mPlayer != null)
        {
            mPlayer.Close();
            mPlayer = null;
        }

        mSourcePath = null;
        DurationSec = 0;
        NaturalWidth = 0;
        NaturalHeight = 0;
    }

    public void Dispose()
    {
        Close();
    }

    /// <summary>
    /// 用系统播放器打开以读尺寸与时长。
    /// </summary>
    private async Task<bool> OpenMediaPlayerAsync(string path, CancellationToken token)
    {
        var player = new MediaPlayer
        {
            ScrubbingEnabled = true,
            Volume = 0
        };
        var opened = new TaskCompletionSource<bool>();
        void OnOpened(object? sender, EventArgs e)
        {
            opened.TrySetResult(true);
        }

        void OnFailed(object? sender, ExceptionEventArgs e)
        {
            opened.TrySetResult(false);
        }

        player.MediaOpened += OnOpened;
        player.MediaFailed += OnFailed;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
        using var register = linked.Token.Register(() => opened.TrySetResult(false));
        player.Open(new Uri(path));
        var ok = await opened.Task.ConfigureAwait(true);
        player.MediaOpened -= OnOpened;
        player.MediaFailed -= OnFailed;
        if (!ok || player.NaturalVideoWidth <= 0)
        {
            player.Close();
            return false;
        }

        player.Pause();
        mPlayer = player;
        NaturalWidth = player.NaturalVideoWidth;
        NaturalHeight = player.NaturalVideoHeight;
        DurationSec = player.NaturalDuration.HasTimeSpan
            ? player.NaturalDuration.TimeSpan.TotalSeconds
            : 0;
        return DurationSec > 0;
    }

    /// <summary>
    /// 无 FFmpeg 时回退：等到指针靠近目标再截一帧。
    /// </summary>
    private async Task<BitmapSource?> GrabWithMediaPlayerAsync(
        double positionSec,
        CancellationToken token,
        int generation)
    {
        var player = mPlayer;
        if (player == null || NaturalWidth <= 0)
        {
            return null;
        }

        player.Pause();
        player.Position = TimeSpan.FromSeconds(positionSec);
        var deadline = DateTime.UtcNow.AddMilliseconds(400);
        try
        {
            while (DateTime.UtcNow < deadline
                && Math.Abs(player.Position.TotalSeconds - positionSec) > 0.08)
            {
                await Task.Delay(40, token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (token.IsCancellationRequested || generation != mGrabGeneration)
        {
            return null;
        }

        var width = Math.Max(1, NaturalWidth);
        var height = Math.Max(1, NaturalHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawVideo(player, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 把抽帧 JPEG 解成可绑到检视的位图。
    /// </summary>
    private static BitmapSource? DecodeJpeg(byte[] jpegBytes)
    {
        using var stream = new MemoryStream(jpegBytes);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
        {
            return null;
        }

        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}

/// <summary>
/// 把检视帧写成封面缓存字节。
/// </summary>
internal static class VideoCoverEncoder
{
    /// <summary>
    /// 编码为 JPEG。本机无 WebP 编码器时用此格式落缓存。
    /// </summary>
    public static byte[] ToJpeg(BitmapSource source)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
