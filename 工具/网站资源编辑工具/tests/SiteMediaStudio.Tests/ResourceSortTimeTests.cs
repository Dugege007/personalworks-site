using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ResourceSortTimeTests
{
    [Fact]
    public void Choose_PrefersCaptureThenCreation()
    {
        var captured = new DateTime(2020, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(captured, ResourceSortTime.Choose(captured, created));
        Assert.Equal(created, ResourceSortTime.Choose(null, created));
        Assert.Null(ResourceSortTime.Choose(null, null));
    }

    [Fact]
    public void Exif_ParsesColonDate()
    {
        Assert.True(ExifDateTime.TryParseLocal("2021:08:03 14:05:06", out var local));
        Assert.Equal(2021, local.Year);
        Assert.Equal(8, local.Month);
        Assert.Equal(3, local.Day);
        Assert.Equal(14, local.Hour);
        Assert.Equal(DateTimeKind.Local, local.Kind);
    }

    [Fact]
    public void Exif_RejectsBlank()
    {
        Assert.False(ExifDateTime.TryParseLocal("  ", out _));
        Assert.False(ExifDateTime.TryParseLocal(null, out _));
    }
}
