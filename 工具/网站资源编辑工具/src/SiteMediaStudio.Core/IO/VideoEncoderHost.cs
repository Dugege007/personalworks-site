namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次编码器解析结果。
/// </summary>
public sealed class VideoEncoderHit
{
    public required string Kind { get; init; }
    public required string FileName { get; init; }
}

/// <summary>
/// 探测本机 MediaCoder 命令行与 FFmpeg；图形界面不算可用编码器。
/// </summary>
public static class VideoEncoderHost
{
    private static readonly string[] MediaCoderCliNameList = { "mc.exe", "mc" };
    private static readonly string[] FfmpegNameList = { "ffmpeg.exe", "ffmpeg" };
    private static readonly string[] FfprobeNameList = { "ffprobe.exe", "ffprobe" };

    private static readonly string[] MediaCoderCliPathList =
    {
        @"C:\Program Files\MediaCoder\mc.exe",
        @"C:\Program Files (x86)\MediaCoder\mc.exe"
    };

    private static readonly string[] FfmpegPathList =
    {
        @"C:\ffmpeg\bin\ffmpeg.exe",
        @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
        @"C:\Program Files\MediaCoder\ffmpeg.exe",
        @"C:\Program Files\MediaCoder\tools\ffmpeg.exe",
        @"C:\Program Files\MediaCoder\tools64\ffmpeg.exe"
    };

    /// <summary>
    /// 是否已找到可在子进程里压码的编码器。
    /// </summary>
    public static bool CanEncode(AppSettings? settings = null)
    {
        return Resolve(settings) != null;
    }

    /// <summary>
    /// 顺序：设置中的 MediaCoder CLI → 常见 CLI 路径 → 设置中的 FFmpeg → PATH / 常见 FFmpeg。
    /// </summary>
    public static VideoEncoderHit? Resolve(AppSettings? settings = null)
    {
        settings ??= AppSettingsStore.Load();
        var mediaCoder = ResolveConfiguredCli(settings.MediaCoderPath)
            ?? ResolveExisting(MediaCoderCliPathList)
            ?? FindOnPath(MediaCoderCliNameList);
        if (mediaCoder != null)
        {
            return new VideoEncoderHit { Kind = "mediacoder", FileName = mediaCoder };
        }

        var ffmpeg = FfmpegFrameExtractor.ResolveFfmpeg(settings.FfmpegPath)
            ?? ResolveExisting(FfmpegPathList);
        if (ffmpeg != null)
        {
            return new VideoEncoderHit { Kind = "ffmpeg", FileName = ffmpeg };
        }

        return null;
    }

    /// <summary>
    /// 解析 ffprobe；优先与 FFmpeg 同目录。
    /// </summary>
    public static string? ResolveFfprobe(AppSettings? settings = null)
    {
        var encoder = Resolve(settings);
        if (encoder != null && encoder.Kind == "ffmpeg")
        {
            var sibling = Path.Combine(
                Path.GetDirectoryName(encoder.FileName) ?? "",
                OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
            if (File.Exists(sibling))
            {
                return sibling;
            }
        }

        return FindOnPath(FfprobeNameList);
    }

    /// <summary>
    /// 配置路径仅当是命令行可执行文件时采用；<c>MediaCoder.exe</c> 为图形界面，不算。
    /// </summary>
    private static string? ResolveConfiguredCli(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || !File.Exists(configuredPath))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(configuredPath);
        if (string.Equals(name, "MediaCoder", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return configuredPath;
    }

    /// <summary>
    /// 返回第一个存在的路径。
    /// </summary>
    private static string? ResolveExisting(IEnumerable<string> pathList)
    {
        return pathList.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// 在 PATH 中查找可执行文件。
    /// </summary>
    private static string? FindOnPath(IReadOnlyList<string> nameList)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in nameList)
            {
                var candidate = Path.Combine(dir.Trim(), name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
