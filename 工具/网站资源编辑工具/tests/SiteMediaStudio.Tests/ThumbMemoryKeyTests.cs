using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ThumbMemoryKeyTests
{
    [Fact]
    public void Matches_SamePathAndDecodePx_IgnoresWriteTime()
    {
        var first = ThumbMemoryKey.Build(@"D:\a.jpg", 240, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var second = ThumbMemoryKey.Build(@"D:\a.jpg", 240, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(ThumbMemoryKey.Matches(first, @"D:\a.jpg", 240));
        Assert.True(ThumbMemoryKey.Matches(second, @"D:\a.jpg", 240));
    }

    [Fact]
    public void Matches_DifferentDecodePx_IsFalse()
    {
        var key = ThumbMemoryKey.Build(@"D:\a.jpg", 240, DateTime.UnixEpoch);
        Assert.False(ThumbMemoryKey.Matches(key, @"D:\a.jpg", 1600));
        Assert.False(ThumbMemoryKey.Matches(key, @"D:\a.jpg", 0));
    }
}
