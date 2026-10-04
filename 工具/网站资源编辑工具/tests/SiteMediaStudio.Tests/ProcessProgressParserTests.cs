using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ProcessProgressParserTests
{
    [Fact]
    public void TryParsePrepareLine_ReadsPercentAndDetail()
    {
        Assert.True(ProcessProgressParser.TryParsePrepareLine("进度 45% 质量 80 1920x1080", out var fraction, out var detail));
        Assert.Equal(0.45, fraction, 3);
        Assert.Equal("质量 80 1920x1080", detail);
        Assert.False(ProcessProgressParser.TryParsePrepareLine("完成：单张", out _, out _));
    }

    [Theory]
    [InlineData("out_time_us=5120000", 5.12)]
    [InlineData("out_time_ms=5120000", 5.12)]
    [InlineData("out_time=00:00:05.12", 5.12)]
    [InlineData("time=00:01:03.50", 63.5)]
    [InlineData("frame=  12 fps= 25 q=28.0 size=  100kB time=00:00:08.00 bitrate=100kbits/s", 8)]
    public void TryParseMediaTimeSec_ReadsFfmpegKeys(string line, double expected)
    {
        Assert.True(ProcessProgressParser.TryParseMediaTimeSec(line, out var seconds));
        Assert.Equal(expected, seconds, 2);
    }

    [Fact]
    public void TryParseMediaTimeSec_RejectsUnavailable()
    {
        Assert.False(ProcessProgressParser.TryParseMediaTimeSec("out_time_us=N/A", out _));
        Assert.False(ProcessProgressParser.TryParseMediaTimeSec("speed=1.2x", out _));
    }

    [Fact]
    public void TryParsePercentLine_ReadsProgressLabel()
    {
        Assert.True(ProcessProgressParser.TryParsePercentLine("Progress: 37%", out var fraction));
        Assert.Equal(0.37, fraction, 2);
        Assert.False(ProcessProgressParser.TryParsePercentLine("encoded 37 frames", out _));
    }

    [Fact]
    public void FractionOfDuration_ClampsToUnit()
    {
        Assert.Equal(0.25, ProcessProgressParser.FractionOfDuration(10, 40), 3);
        Assert.Equal(1, ProcessProgressParser.FractionOfDuration(50, 40), 3);
        Assert.Equal(0, ProcessProgressParser.FractionOfDuration(5, 0), 3);
    }

    [Fact]
    public void TryParseEncodeLine_WritesPercentNotClock()
    {
        Assert.True(ProcessProgressParser.TryParseEncodeLine(
            "out_time_us=30000000",
            120,
            out var fraction,
            out var detail));
        Assert.Equal(0.25, fraction, 3);
        Assert.Equal("压码 25%", detail);
    }

    [Fact]
    public void FormatClock_OmitsHoursUnderOneHour()
    {
        Assert.Equal("00:05", ProcessProgressParser.FormatClock(5));
        Assert.Equal("1:01:01", ProcessProgressParser.FormatClock(3661));
    }
}
