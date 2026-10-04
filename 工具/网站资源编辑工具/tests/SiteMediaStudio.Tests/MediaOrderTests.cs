using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class MediaOrderTests
{
    [Fact]
    public void Apply_LeavesUnknownKeysAndKeepsLeftovers()
    {
        var mediaList = new[]
        {
            new WorkMediaItem { Src = "a/01.webp" },
            new WorkMediaItem { Src = "a/02.webp" },
            new WorkMediaItem { Src = "a/03.webp" }
        };

        var ordered = MediaOrder.Apply(mediaList, new[] { "a/03.webp", "missing.webp", "a/01.webp" });

        Assert.Equal(new[] { "a/03.webp", "a/01.webp", "a/02.webp" }, ordered.Select(item => item.Src));
    }

    [Fact]
    public void ApplySiteItems_KeepsHiddenAtEnd()
    {
        var visibleA = NewSite("w", "ch", "a/01.webp", 0, 3, hidden: false);
        var visibleB = NewSite("w", "ch", "a/02.webp", 1, 3, hidden: false);
        var hidden = NewSite("w", "ch", "a/09.webp", 2, 3, hidden: true);

        var ordered = MediaOrder.ApplySiteItems(
            new[] { visibleA, visibleB, hidden },
            new[] { "a/02.webp", "a/01.webp" });

        Assert.Equal(new[] { "a/02.webp", "a/01.webp", "a/09.webp" }, ordered.Select(item => item.ObjectKey));
        Assert.Equal(0, ordered[0].Index);
        Assert.Equal(2, ordered[2].Index);
        Assert.True(ordered[2].IsHidden);
    }

    [Fact]
    public void MoveItems_KeepsRelativeOrderAndAdjustsInsert()
    {
        var a = new WorkMediaItem { Src = "a" };
        var b = new WorkMediaItem { Src = "b" };
        var c = new WorkMediaItem { Src = "c" };
        var d = new WorkMediaItem { Src = "d" };

        var moved = MediaOrder.MoveItems(new[] { a, b, c, d }, new[] { b, c }, 0);
        Assert.Equal(new[] { b, c, a, d }, moved);
    }

    [Fact]
    public void SameOrder_IgnoresSlashDirection()
    {
        Assert.True(MediaOrder.SameOrder(new[] { "a/01.webp" }, new[] { "a\\01.webp" }));
        Assert.False(MediaOrder.SameOrder(new[] { "a/01.webp" }, new[] { "a/02.webp" }));
    }

    private static SiteItem NewSite(string workId, string channel, string objectKey, int index, int total, bool hidden)
    {
        return new SiteItem
        {
            WorkId = workId,
            ChannelKey = channel,
            WorkTitle = workId,
            Label = objectKey,
            ObjectKey = objectKey,
            Index = index,
            Total = total,
            IsHidden = hidden
        };
    }
}
