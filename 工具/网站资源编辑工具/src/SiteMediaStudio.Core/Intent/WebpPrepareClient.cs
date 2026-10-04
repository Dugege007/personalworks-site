namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 旁路调用既有 <c>prepare-initial-batch.py</c>，不在 C# 内转码。
/// </summary>
public static class WebpPrepareClient
{
    /// <summary>
    /// 单张压成网页规格 WebP；源图不覆盖。
    /// </summary>
    public static WebpPrepareResult Prepare(
        string sourcePath,
        string outputPath,
        string? channel,
        Action<double, string>? onProgress = null)
    {
        var (maxSide, minSide) = WebpPrepareRules.SidesFor(channel);
        return Prepare(
            sourcePath,
            outputPath,
            maxSide,
            minSide,
            lossless: false,
            WebpPrepareRules.FixedQualityFor(channel),
            onProgress);
    }

    /// <summary>
    /// 按指定长边压成 WebP；视频封面用 1280。
    /// </summary>
    public static WebpPrepareResult Prepare(
        string sourcePath,
        string outputPath,
        int maxSide,
        int minSide,
        Action<double, string>? onProgress = null)
    {
        return Prepare(sourcePath, outputPath, maxSide, minSide, lossless: false, quality: null, onProgress);
    }

    /// <summary>
    /// 按指定长边压成 WebP。<paramref name="lossless"/> 时不缩小、不降质。
    /// <paramref name="quality"/> 有值时固定该质量，不再按体积往下降。
    /// </summary>
    public static WebpPrepareResult Prepare(
        string sourcePath,
        string outputPath,
        int maxSide,
        int minSide,
        bool lossless,
        int? quality,
        Action<double, string>? onProgress = null)
    {
        var script = ToolPaths.FindPrepareScript();
        if (script == null)
        {
            throw new FileNotFoundException("找不到 prepare-initial-batch.py。");
        }

        var argumentList = new List<string>
        {
            "--source",
            sourcePath,
            "--output",
            outputPath,
            "--apply",
            "--max-side",
            maxSide.ToString()
        };
        if (lossless)
        {
            argumentList.Add("--lossless");
        }
        else
        {
            argumentList.Add("--min-side");
            argumentList.Add(minSide.ToString());
            if (quality is int fixedQuality)
            {
                argumentList.Add("--quality");
                argumentList.Add(fixedQuality.ToString());
                argumentList.Add("--min-quality");
                argumentList.Add(fixedQuality.ToString());
            }
        }
        var cwd = Path.GetDirectoryName(script) ?? Directory.GetCurrentDirectory();
        var run = PythonHost.Run(
            script,
            argumentList,
            cwd,
            onOutputLine: line =>
            {
                if (onProgress == null)
                {
                    return;
                }

                if (ProcessProgressParser.TryParsePrepareLine(line, out var fraction, out var detail))
                {
                    onProgress(fraction, string.IsNullOrWhiteSpace(detail) ? "正在压图" : detail);
                }
            });
        return new WebpPrepareResult
        {
            Ok = run.Ok,
            ExitCode = run.ExitCode,
            StdOut = run.StdOut,
            StdErr = run.StdErr,
            OutputPath = outputPath
        };
    }
}

/// <summary>
/// 一次压图调用的结果。
/// </summary>
public sealed class WebpPrepareResult
{
    public bool Ok { get; init; }
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public string OutputPath { get; init; } = "";

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

            return string.IsNullOrWhiteSpace(StdOut) ? $"压图退出码 {ExitCode}。" : StdOut;
        }
    }
}
