using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class SurfaceGridCacheTests
{
    [Fact]
    public void BuildKey_JoinsPaneAndNav()
    {
        Assert.Equal("stage|work:landscape-cds:xinghe", SurfaceGridCache<string, string>.BuildKey(
            "stage",
            "work:landscape-cds:xinghe"));
    }

    [Fact]
    public void Park_ThenTryGet_ReturnsSameCardInstances()
    {
        var cache = new SurfaceGridCache<Card, string>();
        var first = new Card("a");
        var second = new Card("b");
        cache.Park("stage|work:demo", new[] { first, second }, Array.Empty<string>(), new[] { "a" }, "b");

        Assert.True(cache.TryGet("stage|work:demo", out var snapshot));
        Assert.Equal(2, snapshot!.CardList.Count);
        Assert.Same(first, snapshot.CardList[0]);
        Assert.Same(second, snapshot.CardList[1]);
        Assert.Equal(new[] { "a" }, snapshot.SelectedKeyList);
        Assert.Equal("b", snapshot.FocusKey);
    }

    [Fact]
    public void Park_CopiesListsSoLaterMutationDoesNotLeak()
    {
        var cache = new SurfaceGridCache<Card, string>();
        var cardList = new List<Card> { new("a") };
        cache.Park("site|channel:demo", cardList, Array.Empty<string>(), Array.Empty<string>(), null);
        cardList.Add(new Card("b"));

        Assert.True(cache.TryGet("site|channel:demo", out var snapshot));
        Assert.Single(snapshot!.CardList);
    }

    [Fact]
    public void Park_OverwritesSameKey()
    {
        var cache = new SurfaceGridCache<Card, string>();
        cache.Park("stage|work:demo", new[] { new Card("old") }, Array.Empty<string>(), Array.Empty<string>(), null);
        var next = new Card("new");
        cache.Park("stage|work:demo", new[] { next }, Array.Empty<string>(), Array.Empty<string>(), "new");

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("stage|work:demo", out var snapshot));
        Assert.Same(next, snapshot!.CardList[0]);
        Assert.Equal("new", snapshot.FocusKey);
    }

    [Fact]
    public void Park_EvictsLeastRecentlyUsedWhenOverCapacity()
    {
        var cache = new SurfaceGridCache<Card, string>(2);
        cache.Park("a", new[] { new Card("a") }, Array.Empty<string>(), Array.Empty<string>(), null);
        cache.Park("b", new[] { new Card("b") }, Array.Empty<string>(), Array.Empty<string>(), null);
        cache.Park("c", new[] { new Card("c") }, Array.Empty<string>(), Array.Empty<string>(), null);

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void TryGet_TouchesKeySoItIsNotEvictedNext()
    {
        var cache = new SurfaceGridCache<Card, string>(2);
        cache.Park("a", new[] { new Card("a") }, Array.Empty<string>(), Array.Empty<string>(), null);
        cache.Park("b", new[] { new Card("b") }, Array.Empty<string>(), Array.Empty<string>(), null);
        Assert.True(cache.TryGet("a", out _));
        cache.Park("c", new[] { new Card("c") }, Array.Empty<string>(), Array.Empty<string>(), null);

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
    }

    [Fact]
    public void Clear_DropsAllSurfaces()
    {
        var cache = new SurfaceGridCache<Card, string>();
        cache.Park("a", new[] { new Card("a") }, Array.Empty<string>(), Array.Empty<string>(), null);
        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("a", out _));
    }

    [Fact]
    public void ForEachCard_VisitsEveryParkedCard()
    {
        var cache = new SurfaceGridCache<Card, string>();
        cache.Park("a", new[] { new Card("a1"), new Card("a2") }, Array.Empty<string>(), Array.Empty<string>(), null);
        cache.Park("b", new[] { new Card("b1") }, Array.Empty<string>(), Array.Empty<string>(), null);
        var seenList = new List<string>();

        cache.ForEachCard(card => seenList.Add(card.Key));

        Assert.Equal(new[] { "b1", "a1", "a2" }, seenList);
    }

    [Fact]
    public void Park_IgnoresBlankKey()
    {
        var cache = new SurfaceGridCache<Card, string>();
        cache.Park(" ", new[] { new Card("a") }, Array.Empty<string>(), Array.Empty<string>(), null);

        Assert.Equal(0, cache.Count);
    }

    private sealed class Card
    {
        public Card(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }
}
