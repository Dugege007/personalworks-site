using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class CardSelectionHistoryTests
{
    [Fact]
    public void Remember_PushesPreviousAndRestoreReturnsIt()
    {
        var history = new CardSelectionHistory();
        var first = Snapshot("a");
        var second = Snapshot("b");

        history.Remember(first, second);

        Assert.True(history.CanRestore);
        Assert.Equal(first, history.Restore());
        Assert.False(history.CanRestore);
    }

    [Fact]
    public void Remember_IgnoresEmptyPrevious()
    {
        var history = new CardSelectionHistory();

        history.Remember(Snapshot(), Snapshot("a"));

        Assert.False(history.CanRestore);
        Assert.Null(history.Restore());
    }

    [Fact]
    public void Remember_IgnoresUnchangedSelection()
    {
        var history = new CardSelectionHistory();
        var same = Snapshot("a", "b");

        history.Remember(same, Snapshot("a", "b"));

        Assert.False(history.CanRestore);
    }

    [Fact]
    public void Restore_WalksBackIncludingMultiSelect()
    {
        var history = new CardSelectionHistory();
        var first = Snapshot("a");
        var second = Snapshot("b", "c");
        var third = Snapshot("d");

        history.Remember(first, second);
        history.Remember(second, third);

        var restored = history.Restore();
        Assert.Equal(new[] { "b", "c" }, restored?.KeyList);
        Assert.Equal("c", restored?.FocusKey);
        Assert.Equal(first, history.Restore());
        Assert.Null(history.Restore());
    }

    [Fact]
    public void Remember_DropsOldestWhenExceedingFive()
    {
        var history = new CardSelectionHistory();
        history.Remember(Snapshot("1"), Snapshot("2"));
        history.Remember(Snapshot("2"), Snapshot("3"));
        history.Remember(Snapshot("3"), Snapshot("4"));
        history.Remember(Snapshot("4"), Snapshot("5"));
        history.Remember(Snapshot("5"), Snapshot("6"));
        history.Remember(Snapshot("6"), Snapshot("7"));

        Assert.Equal(CardSelectionHistory.MaxCount, history.Count);
        Assert.Equal(Snapshot("6"), history.Restore());
        Assert.Equal(Snapshot("5"), history.Restore());
        Assert.Equal(Snapshot("4"), history.Restore());
        Assert.Equal(Snapshot("3"), history.Restore());
        Assert.Equal(Snapshot("2"), history.Restore());
        Assert.Null(history.Restore());
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var history = new CardSelectionHistory();
        history.Remember(Snapshot("a"), Snapshot("b"));

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.False(history.CanRestore);
    }

    private static CardSelectionSnapshot Snapshot(params string[] keys)
    {
        return new CardSelectionSnapshot(keys, keys.Length == 0 ? null : keys[^1]);
    }
}
