using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 从压图脚本与编码器输出解析真实进度，不编造百分比。
/// </summary>
public static class ProcessProgressParser
{
    private static readonly Regex PrepareLineRegex = new(
        @"^进度\s+(\d+(?:\.\d+)?)\s*%(?:\s+(.*))?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FfmpegKeyRegex = new(
        @"^(out_time_us|out_time_ms|out_time|time)=(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FfmpegTimeInStatsRegex = new(
        @"\btime=(\d{1,3}:\d{2}:\d{2}(?:\.\d+)?|\d{1,2}:\d{2}(?:\.\d+)?)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClockRegex = new(
        @"^(?:(\d+):)?(\d{1,2}):(\d{2}(?:\.\d+)?)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PercentLabelRegex = new(
        @"\bprogress(?:ion)?\s*[:=]\s*(\d{1,3}(?:\.\d+)?)\s*%",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// 解析压图脚本的 <c>进度 45% 质量 80</c>。
    /// </summary>
    public static bool TryParsePrepareLine(string? line, out double fraction, out string? detail)
    {
        fraction = 0;
        detail = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = PrepareLineRegex.Match(line.Trim());
        if (!match.Success
            || !TryPercent(match.Groups[1].Value, out fraction))
        {
            return false;
        }

        detail = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = null;
        }

        return true;
    }

    /// <summary>
    /// 解析 FFmpeg <c>-progress</c> 键或统计行里的已处理时间。
    /// </summary>
    public static bool TryParseMediaTimeSec(string? line, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        var keyMatch = FfmpegKeyRegex.Match(trimmed);
        if (keyMatch.Success)
        {
            return TryReadTimeValue(keyMatch.Groups[1].Value, keyMatch.Groups[2].Value.Trim(), out seconds);
        }

        var stats = FfmpegTimeInStatsRegex.Match(trimmed);
        return stats.Success && TryParseClock(stats.Groups[1].Value, out seconds);
    }

    /// <summary>
    /// 解析带 Progress 标签的百分比（MediaCoder 等）。
    /// </summary>
    public static bool TryParsePercentLine(string? line, out double fraction)
    {
        fraction = 0;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = PercentLabelRegex.Match(line.Trim());
        return match.Success && TryPercent(match.Groups[1].Value, out fraction);
    }

    /// <summary>
    /// 已处理时长相对片长；无片长则 0。
    /// </summary>
    public static double FractionOfDuration(double currentSec, double durationSec)
    {
        if (durationSec <= 0 || currentSec <= 0)
        {
            return 0;
        }

        return Math.Clamp(currentSec / durationSec, 0, 1);
    }

    /// <summary>
    /// 压码步说明：只写百分比。
    /// </summary>
    public static string FormatEncodePercent(double fraction)
    {
        var percent = (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100);
        return "压码 " + percent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// 进度说明用的时钟。
    /// </summary>
    public static string FormatClock(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            return "00:00";
        }

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? span.ToString(@"h\:mm\:ss")
            : span.ToString(@"mm\:ss");
    }

    /// <summary>
    /// 把编码器一行转成步内分数与说明；对不上则假。
    /// </summary>
    public static bool TryParseEncodeLine(
        string? line,
        double? durationSec,
        out double fraction,
        out string? detail)
    {
        fraction = 0;
        detail = null;
        if (TryParseMediaTimeSec(line, out var seconds) && durationSec is > 0.05)
        {
            fraction = FractionOfDuration(seconds, durationSec.Value);
            detail = FormatEncodePercent(fraction);
            return true;
        }

        if (TryParsePercentLine(line, out fraction))
        {
            detail = FormatEncodePercent(fraction);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 读取 FFmpeg 进度键右侧的时间。
    /// </summary>
    private static bool TryReadTimeValue(string key, string value, out double seconds)
    {
        seconds = 0;
        if (string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (key.Equals("out_time_us", StringComparison.OrdinalIgnoreCase)
            || key.Equals("out_time_ms", StringComparison.OrdinalIgnoreCase))
        {
            // FFmpeg 的 out_time_ms 名虽带 ms，值与 out_time_us 相同，都是微秒。
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var us)
                && us >= 0
                && AcceptSeconds(us / 1_000_000.0, out seconds);
        }

        return TryParseClock(value, out seconds);
    }

    /// <summary>
    /// 解析 <c>HH:MM:SS.xx</c> 或 <c>MM:SS.xx</c>。
    /// </summary>
    private static bool TryParseClock(string text, out double seconds)
    {
        seconds = 0;
        var match = ClockRegex.Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var hourText = match.Groups[1].Success ? match.Groups[1].Value : "0";
        if (!int.TryParse(hourText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
            || !double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var secs))
        {
            return false;
        }

        return AcceptSeconds(hours * 3600 + minutes * 60 + secs, out seconds);
    }

    /// <summary>
    /// 百分数转 0～1。
    /// </summary>
    private static bool TryPercent(string text, out double fraction)
    {
        fraction = 0;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
            || percent < 0
            || percent > 100)
        {
            return false;
        }

        fraction = percent / 100.0;
        return true;
    }

    /// <summary>
    /// 拒绝非有限时长。
    /// </summary>
    private static bool AcceptSeconds(double value, out double seconds)
    {
        seconds = 0;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            return false;
        }

        seconds = value;
        return true;
    }
}
