using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 旁路调用 MediaCoder CLI 或 FFmpeg，产出网页 MP4 与封面 WebP。
/// </summary>
public static class VideoEncodeClient
{
    /// <summary>
    /// 压成网页 MP4，并写出伴生封面。源片不覆盖。
    /// </summary>
    public static VideoEncodeResult Prepare(
        string sourcePath,
        string outputMp4Path,
        string outputPosterPath,
        string originalStageRel,
        AppSettings? settings = null,
        Action<double, string>? onProgress = null)
    {
        settings ??= AppSettingsStore.Load();
        if (!VideoEncodeRules.TryAudioBitrate(settings.VideoAudioBitrateKbps, out var audioKbps, out var bitrateText))
        {
            return Fail(bitrateText ?? VideoEncodeRules.MissingEncoderMessage);
        }

        var encoder = VideoEncoderHost.Resolve(settings);
        if (encoder == null)
        {
            return Fail(VideoEncodeRules.MissingEncoderMessage);
        }

        if (!File.Exists(sourcePath))
        {
            return Fail("找不到投放箱视频：" + originalStageRel);
        }

        var sourceInfo = new FileInfo(sourcePath);
        var coverHit = VideoCoverStore.TryRead(originalStageRel, sourceInfo.Length, sourceInfo.LastWriteTimeUtc);
        var durationSec = TryReadDurationSec(sourcePath, settings);
        var positionSec = coverHit?.Record.PositionSec
            ?? VideoCoverRules.DefaultPositionSec(durationSec);

        Directory.CreateDirectory(Path.GetDirectoryName(outputMp4Path) ?? ".");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPosterPath) ?? ".");

        void ReportEncode(double fraction, string detail)
        {
            onProgress?.Invoke(0.92 * Math.Clamp(fraction, 0, 1), detail);
        }

        var encode = encoder.Kind == "mediacoder"
            ? RunMediaCoder(encoder.FileName, sourcePath, outputMp4Path, audioKbps, durationSec, ReportEncode)
            : RunFfmpegEncode(encoder.FileName, sourcePath, outputMp4Path, audioKbps, durationSec, ReportEncode);
        if (!encode.Ok)
        {
            return encode;
        }

        onProgress?.Invoke(0.93, "正在写封面");
        var poster = WritePoster(sourcePath, outputPosterPath, positionSec, coverHit?.ImagePath, settings);
        if (!poster.Ok)
        {
            return poster;
        }

        onProgress?.Invoke(1, "压码完成");
        return new VideoEncodeResult
        {
            Ok = true,
            StdOut = encode.StdOut,
            OutputPath = outputMp4Path,
            PosterPath = outputPosterPath
        };
    }

    /// <summary>
    /// 探测片源时长；失败则空，封面回退 1 秒。
    /// </summary>
    public static double? TryReadDurationSec(string videoPath, AppSettings? settings = null)
    {
        var probe = VideoEncoderHost.ResolveFfprobe(settings);
        if (probe == null)
        {
            return null;
        }

        var run = RunProcess(
            probe,
            [
                "-v", "error",
                "-show_entries", "format=duration",
                "-of", "default=noprint_wrappers=1:nokey=1",
                videoPath
            ],
            15_000);
        if (!run.Ok || !double.TryParse(run.StdOut.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
        {
            return null;
        }

        return sec;
    }

    /// <summary>
    /// 探测成片视频峰值近似码率（bps）。供隔离验收。
    /// </summary>
    public static long? TryReadVideoBitrate(string videoPath, AppSettings? settings = null)
    {
        var probe = VideoEncoderHost.ResolveFfprobe(settings);
        if (probe == null)
        {
            return null;
        }

        var run = RunProcess(
            probe,
            [
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=bit_rate",
                "-of", "default=noprint_wrappers=1:nokey=1",
                videoPath
            ],
            15_000);
        if (run.Ok && long.TryParse(run.StdOut.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var streamRate)
            && streamRate > 0)
        {
            return streamRate;
        }

        run = RunProcess(
            probe,
            [
                "-v", "error",
                "-show_entries", "format=bit_rate",
                "-of", "default=noprint_wrappers=1:nokey=1",
                videoPath
            ],
            15_000);
        return run.Ok && long.TryParse(run.StdOut.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var formatRate)
            ? formatRate
            : null;
    }

    /// <summary>
    /// MediaCoder 命令行：只传输入、输出与码率上限。
    /// </summary>
    private static VideoEncodeResult RunMediaCoder(
        string exe,
        string sourcePath,
        string outputPath,
        int audioKbps,
        double? durationSec,
        Action<double, string>? onProgress)
    {
        var run = RunProcess(
            exe,
            [
                "-vbitrate", "6000",
                "-abitrate", audioKbps.ToString(CultureInfo.InvariantCulture),
                "-o", outputPath,
                sourcePath
            ],
            1_800_000,
            line => ReportEncodeLine(line, durationSec, onProgress));
        if (!run.Ok || !File.Exists(outputPath))
        {
            return Fail(string.IsNullOrWhiteSpace(run.FailureText) ? "MediaCoder 压码失败。" : run.FailureText, run);
        }

        return run;
    }

    /// <summary>
    /// FFmpeg 网页预设：H.264 + AAC + faststart，不放大。
    /// </summary>
    private static VideoEncodeResult RunFfmpegEncode(
        string exe,
        string sourcePath,
        string outputPath,
        int audioKbps,
        double? durationSec,
        Action<double, string>? onProgress)
    {
        var run = RunProcess(
            exe,
            [
                "-hide_banner",
                "-nostats",
                "-progress",
                "pipe:1",
                "-y",
                "-i", sourcePath,
                "-c:v", "libx264",
                "-pix_fmt", "yuv420p",
                "-crf", "21",
                "-maxrate", "6M",
                "-bufsize", "12M",
                "-c:a", "aac",
                "-b:a", audioKbps.ToString(CultureInfo.InvariantCulture) + "k",
                "-ac", "2",
                "-movflags", "+faststart",
                outputPath
            ],
            1_800_000,
            line => ReportEncodeLine(line, durationSec, onProgress));
        if (!run.Ok || !File.Exists(outputPath))
        {
            return Fail(string.IsNullOrWhiteSpace(run.FailureText) ? "FFmpeg 压码失败。" : run.FailureText, run);
        }

        return run;
    }

    /// <summary>
    /// 手设封面优先；否则按秒抽一帧。长边不超过 1280。
    /// </summary>
    private static VideoEncodeResult WritePoster(
        string sourcePath,
        string outputPosterPath,
        double positionSec,
        string? coverImagePath,
        AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(coverImagePath) && File.Exists(coverImagePath))
        {
            if (coverImagePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(coverImagePath, outputPosterPath, overwrite: true);
                return new VideoEncodeResult { Ok = true, OutputPath = outputPosterPath, PosterPath = outputPosterPath };
            }

            var converted = ConvertImageToPoster(coverImagePath, outputPosterPath, settings);
            if (converted.Ok)
            {
                return converted;
            }
        }

        var ffmpeg = VideoEncoderHost.Resolve(settings);
        if (ffmpeg is not { Kind: "ffmpeg" })
        {
            return Fail(VideoEncodeRules.MissingPosterMessage);
        }

        var run = RunProcess(
            ffmpeg.FileName,
            [
                "-hide_banner",
                "-y",
                "-ss", positionSec.ToString("0.###", CultureInfo.InvariantCulture),
                "-i", sourcePath,
                "-frames:v", "1",
                "-vf", $"scale='min({VideoEncodeRules.PosterMaxSide}\\,iw)':-2",
                outputPosterPath
            ],
            60_000);
        if (!run.Ok || !File.Exists(outputPosterPath))
        {
            return Fail(VideoEncodeRules.MissingPosterMessage, run);
        }

        return run;
    }

    /// <summary>
    /// 把 JPEG 封面转成正式位 WebP。
    /// </summary>
    private static VideoEncodeResult ConvertImageToPoster(string imagePath, string outputPosterPath, AppSettings settings)
    {
        var ffmpeg = VideoEncoderHost.Resolve(settings);
        if (ffmpeg is { Kind: "ffmpeg" })
        {
            var run = RunProcess(
                ffmpeg.FileName,
                [
                    "-hide_banner",
                    "-y",
                    "-i", imagePath,
                    "-vf", $"scale='min({VideoEncodeRules.PosterMaxSide}\\,iw)':-2",
                    outputPosterPath
                ],
                60_000);
            if (run.Ok && File.Exists(outputPosterPath))
            {
                return run;
            }
        }

        if (ToolPaths.FindPrepareScript() != null && PythonHost.CanRunPrepare())
        {
            var prepared = WebpPrepareClient.Prepare(
                imagePath,
                outputPosterPath,
                VideoEncodeRules.PosterMaxSide,
                VideoEncodeRules.PosterMaxSide);
            return new VideoEncodeResult
            {
                Ok = prepared.Ok,
                ExitCode = prepared.ExitCode,
                StdOut = prepared.StdOut,
                StdErr = prepared.Ok ? prepared.StdErr : (string.IsNullOrWhiteSpace(prepared.FailureText)
                    ? VideoEncodeRules.MissingPosterMessage
                    : prepared.FailureText),
                PosterPath = outputPosterPath
            };
        }

        return Fail(VideoEncodeRules.MissingPosterMessage);
    }

    /// <summary>
    /// 把编码器一行映射到步内分数。
    /// </summary>
    private static void ReportEncodeLine(string line, double? durationSec, Action<double, string>? onProgress)
    {
        if (onProgress == null)
        {
            return;
        }

        if (ProcessProgressParser.TryParseEncodeLine(line, durationSec, out var fraction, out var detail))
        {
            onProgress(fraction, string.IsNullOrWhiteSpace(detail) ? "正在压码" : detail);
        }
    }

    /// <summary>
    /// 启动编码器子进程。
    /// </summary>
    private static VideoEncodeResult RunProcess(
        string fileName,
        IReadOnlyList<string> argumentList,
        int timeoutMs,
        Action<string>? onLine = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var outBuilder = new StringBuilder();
        var errBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            outBuilder.AppendLine(e.Data);
            onLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            errBuilder.AppendLine(e.Data);
            onLine?.Invoke(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                return Fail("无法启动编码器。");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Fail("无法启动编码器：" + ex.Message);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }

            return Fail("视频压码超时。");
        }

        process.WaitForExit();
        return new VideoEncodeResult
        {
            Ok = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            StdOut = outBuilder.ToString().Trim(),
            StdErr = errBuilder.ToString().Trim()
        };
    }

    /// <summary>
    /// 组装失败结果。
    /// </summary>
    private static VideoEncodeResult Fail(string text, VideoEncodeResult? run = null)
    {
        return new VideoEncodeResult
        {
            Ok = false,
            ExitCode = run?.ExitCode ?? 1,
            StdOut = run?.StdOut ?? "",
            StdErr = text
        };
    }
}

/// <summary>
/// 一次视频压码调用的结果。
/// </summary>
public sealed class VideoEncodeResult
{
    public bool Ok { get; init; }
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public string PosterPath { get; init; } = "";

    /// <summary>
    /// 给窗口与运行日志看的失败说明。
    /// </summary>
    public string FailureText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(StdErr))
            {
                return StdErr;
            }

            return string.IsNullOrWhiteSpace(StdOut) ? $"压码退出码 {ExitCode}。" : StdOut;
        }
    }
}
