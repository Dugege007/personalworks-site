using System.Diagnostics;
using System.Globalization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 用本机 FFmpeg 抽出一帧 JPEG。未安装则跳过。
/// </summary>
public static class FfmpegFrameExtractor
{

    /// <summary>
    /// 抽帧到字节；失败返回空。
    /// </summary>
    public static byte[]? TryExtractJpeg(
        string videoPath,
        double positionSec,
        string? ffmpegPath = null,
        int timeoutMs = 15_000,
        bool accurate = false,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            return null;
        }

        var exe = ResolveFfmpeg(ffmpegPath);
        if (exe == null)
        {
            return null;
        }

        var output = Path.Combine(Path.GetTempPath(), "sms-frame-" + Guid.NewGuid().ToString("N") + ".jpg");
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var argument in BuildExtractArgumentList(videoPath, positionSec, output, accurate))
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            if (process == null)
            {
                return null;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var register = token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(true);
                }
                catch (InvalidOperationException)
                {
                }

                return null;
            }

            process.WaitForExit();
            if (token.IsCancellationRequested || process.ExitCode != 0 || !File.Exists(output))
            {
                return null;
            }

            return File.ReadAllBytes(output);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(output))
                {
                    File.Delete(output);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// 组装抽帧参数；<paramref name="accurate"/> 时从关键帧解到指定秒。
    /// </summary>
    public static IReadOnlyList<string> BuildExtractArgumentList(
        string videoPath,
        double positionSec,
        string outputPath,
        bool accurate)
    {
        var argumentList = new List<string>
        {
            "-hide_banner",
            "-loglevel",
            "error"
        };
        if (accurate)
        {
            argumentList.Add("-accurate_seek");
            argumentList.Add("-an");
        }

        argumentList.Add("-ss");
        argumentList.Add(Math.Max(0, positionSec).ToString("0.###", CultureInfo.InvariantCulture));
        argumentList.Add("-i");
        argumentList.Add(videoPath);
        argumentList.Add("-frames:v");
        argumentList.Add("1");
        argumentList.Add("-q:v");
        argumentList.Add("3");
        argumentList.Add("-y");
        argumentList.Add(outputPath);
        return argumentList;
    }

    /// <summary>
    /// 设置路径、常见安装位或 PATH 上的 ffmpeg。只检查文件是否存在，不启动程序。
    /// </summary>
    public static string? ResolveFfmpeg(string? configuredPath)
    {
        return ExternalToolLocator.ResolvePath(
            configuredPath,
            ExternalToolLocator.FfmpegCommonPathList,
            ExternalToolLocator.FfmpegFileNameList);
    }
}
