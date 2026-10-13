using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteImageThresholdTests
{
    [Fact]
    public void IsOverLimit_RequiresStrictlyGreaterThanThreshold()
    {
        var limit = NoteImageThreshold.DefaultImageMaxBytes;
        Assert.Equal(500 * 1024, limit);
        Assert.False(NoteImageThreshold.IsOverLimit(limit, limit));
        Assert.False(NoteImageThreshold.IsOverLimit(limit - 1, limit));
        Assert.True(NoteImageThreshold.IsOverLimit(limit + 1, limit));
        Assert.Equal("", NoteImageThreshold.FormatOverLimit(limit, limit));
    }

    [Fact]
    public void FormatOverLimit_ReusesInspectorSizeText()
    {
        var limit = 500 * 1024L;
        Assert.Equal("超出上限 1 KB（上限 500 KB）", NoteImageThreshold.FormatOverLimit(501 * 1024L, limit));
        Assert.Equal(
            "超出上限 " + FileSizeFormat.Format(1) + "（上限 " + FileSizeFormat.Format(limit) + "）",
            NoteImageThreshold.FormatOverLimit(limit + 1, limit));

        var gifLimit = NoteImageThreshold.DefaultGifMaxBytes;
        Assert.Equal(2L * 1024 * 1024, gifLimit);
        Assert.Equal("超出上限 1 KB（上限 2 MB）", NoteImageThreshold.FormatOverLimit(gifLimit + 1024, gifLimit));
    }

    [Fact]
    public void GifUsesItsOwnThreshold()
    {
        var imageLimit = NoteImageThreshold.DefaultImageMaxBytes;
        var gifLimit = NoteImageThreshold.DefaultGifMaxBytes;
        Assert.True(NoteImageThreshold.IsOverLimit(gifLimit, "图/a.png", imageLimit, gifLimit));
        Assert.False(NoteImageThreshold.IsOverLimit(gifLimit, "图/a.gif", imageLimit, gifLimit));
        Assert.True(NoteImageThreshold.IsOverLimit(gifLimit + 1, "sub/deep.GIF", imageLimit, gifLimit));
        Assert.False(NoteImageThreshold.IsOverLimit(imageLimit, ".webp", imageLimit, gifLimit));
        Assert.True(NoteImageThreshold.IsGif(".gif"));
        Assert.False(NoteImageThreshold.IsGif("a.jpg"));
    }

    [Theory]
    [InlineData("500", 500 * 1024L, NoteImageKilobyteParseFailure.None)]
    [InlineData("0", 0L, NoteImageKilobyteParseFailure.None)]
    [InlineData("2048", 2048 * 1024L, NoteImageKilobyteParseFailure.None)]
    [InlineData("", 0L, NoteImageKilobyteParseFailure.NotInteger)]
    [InlineData("500.5", 0L, NoteImageKilobyteParseFailure.NotInteger)]
    [InlineData("-1", 0L, NoteImageKilobyteParseFailure.NotInteger)]
    [InlineData("9223372036854775807", 0L, NoteImageKilobyteParseFailure.Overflow)]
    public void TryParseWholeKilobytes_AcceptsOnlyNonNegativeIntegers(string text, long bytes, NoteImageKilobyteParseFailure failure)
    {
        var ok = NoteImageThreshold.TryParseWholeKilobytes(text, out var parsed, out var actual);
        Assert.Equal(failure == NoteImageKilobyteParseFailure.None, ok);
        Assert.Equal(failure, actual);
        Assert.Equal(bytes, parsed);
    }

    [Fact]
    public void FormatWholeKilobytes_KeepsExactMultiplesAndDoesNotDropRemainder()
    {
        Assert.Equal("500", NoteImageThreshold.FormatWholeKilobytes(500 * 1024));
        Assert.Equal("2048", NoteImageThreshold.FormatWholeKilobytes(2 * 1024 * 1024));
        Assert.Equal("500.001", NoteImageThreshold.FormatWholeKilobytes(500 * 1024 + 1));
    }
}
