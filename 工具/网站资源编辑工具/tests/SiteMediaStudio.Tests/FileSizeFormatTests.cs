using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class FileSizeFormatTests
{
    [Theory]
    [InlineData(0, "0 KB")]
    [InlineData(1, "1 KB")]
    [InlineData(1024, "1 KB")]
    [InlineData(1025, "2 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(1024 * 1024, "1024 KB")]
    [InlineData(1024 * 1024 + 1, "1 MB")]
    [InlineData(1024 * 1024 * 3 / 2, "1.5 MB")]
    [InlineData(1024 * 1024 + 1024 * 1024 / 100, "1.01 MB")]
    [InlineData(1024L * 1024 * 1024, "1024 MB")]
    [InlineData(1024L * 1024 * 1024 + 1, "1 GB")]
    [InlineData(1024L * 1024 * 1024 * 5 / 2, "2.5 GB")]
    public void Format_UsesKbMbGbThresholds(long bytes, string expected)
    {
        Assert.Equal(expected, FileSizeFormat.Format(bytes));
    }
}
