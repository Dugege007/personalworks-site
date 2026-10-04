using System.Collections.ObjectModel;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class KeyedListSyncTests
{
    [Fact]
    public void Align_InsertsAndRemovesWithoutReplacingKeptItems()
    {
        var keep = new Item("b");
        var drop = new Item("a");
        var target = new ObservableCollection<Item> { drop, keep };
        var add = new Item("c");
        var nextList = new List<Item> { keep, add };

        KeyedListSync.Align(target, nextList, item => item.Key);

        Assert.Equal(2, target.Count);
        Assert.Same(keep, target[0]);
        Assert.Same(add, target[1]);
        Assert.DoesNotContain(drop, target);
    }

    [Fact]
    public void Align_MovesExistingItemToNewIndex()
    {
        var first = new Item("a");
        var second = new Item("b");
        var target = new ObservableCollection<Item> { first, second };

        KeyedListSync.Align(target, new[] { second, first }, item => item.Key);

        Assert.Same(second, target[0]);
        Assert.Same(first, target[1]);
    }

    [Fact]
    public void Align_NoOpWhenOrderMatches()
    {
        var first = new Item("a");
        var second = new Item("b");
        var target = new ObservableCollection<Item> { first, second };

        KeyedListSync.Align(target, new[] { first, second }, item => item.Key);

        Assert.Same(first, target[0]);
        Assert.Same(second, target[1]);
    }

    private sealed class Item
    {
        public Item(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }
}
