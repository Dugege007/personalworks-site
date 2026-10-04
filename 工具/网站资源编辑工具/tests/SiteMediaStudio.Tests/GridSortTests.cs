using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class GridSortTests
{
    [Fact]
    public void Default_IsCatalogOrder()
    {
        var state = GridSortState.CreateDefault();
        Assert.Equal("原序", state.Caption);
        Assert.False(state.HasActive);
        Assert.Equal(0, state.Priority(GridSortKey.Name));
        Assert.Equal(0, state.Priority(GridSortKey.Stars));
        var laterName = Fact(0, "img10.png");
        var earlierName = Fact(1, "img2.png");
        Assert.True(GridSort.Compare(laterName, earlierName, state.Rules) < 0);
    }

    [Fact]
    public void Toggle_AppendsThenRenumbers()
    {
        var state = GridSortState.CreateDefault();
        state.Toggle(GridSortKey.Name);
        state.Toggle(GridSortKey.Stars);
        Assert.Equal(1, state.Priority(GridSortKey.Name));
        Assert.Equal(2, state.Priority(GridSortKey.Stars));
        Assert.Equal("1 名称 · 2 星级", state.Caption);

        var low = Fact(0, "a", stars: 1);
        var high = Fact(1, "a", stars: 5);
        var later = Fact(2, "b", stars: 0);
        Assert.True(GridSort.Compare(low, high, state.Rules) > 0);
        Assert.True(GridSort.Compare(high, later, state.Rules) < 0);

        state.Toggle(GridSortKey.Name);
        Assert.Equal(0, state.Priority(GridSortKey.Name));
        Assert.Equal(1, state.Priority(GridSortKey.Stars));
        Assert.Equal("按星级", state.Caption);
        Assert.True(GridSort.Compare(low, later, state.Rules) < 0);
    }

    [Fact]
    public void Flip_DoesNotChangePriority()
    {
        var state = GridSortState.CreateDefault();
        state.Toggle(GridSortKey.Name);
        state.Toggle(GridSortKey.Stars);
        state.Flip(GridSortKey.Name);
        Assert.Equal(1, state.Priority(GridSortKey.Name));
        Assert.Equal(2, state.Priority(GridSortKey.Stars));
        Assert.Equal(GridSortDir.Desc, state.Dir(GridSortKey.Name));
        Assert.Equal(GridSortDir.Desc, state.Dir(GridSortKey.Stars));
        Assert.True(GridSort.Compare(Fact(1, "b"), Fact(0, "a"), state.Rules) < 0);
    }

    [Fact]
    public void Stars_StaysHighToLow()
    {
        var state = GridSortState.CreateDefault();
        state.Toggle(GridSortKey.Stars);
        state.Flip(GridSortKey.Stars);
        Assert.Equal(GridSortDir.Desc, state.Dir(GridSortKey.Stars));
        Assert.Equal("按星级", state.Caption);
        Assert.True(GridSort.Compare(Fact(1, stars: 5), Fact(0, stars: 1), state.Rules) < 0);
        Assert.True(GridSort.Compare(Fact(0, stars: 0), Fact(2, stars: 2), state.Rules) > 0);
    }

    [Fact]
    public void FlipUnselected_StaysOut()
    {
        var state = GridSortState.CreateDefault();
        state.Flip(GridSortKey.Time);
        Assert.Equal(0, state.Priority(GridSortKey.Time));
        Assert.Equal(GridSortDir.Desc, state.Dir(GridSortKey.Time));
        Assert.Equal("原序", state.Caption);
        Assert.False(state.HasActive);
    }

    [Fact]
    public void Clear_FallsBackToCatalogIndex()
    {
        var state = GridSortState.CreateDefault();
        state.Toggle(GridSortKey.Name);
        state.Toggle(GridSortKey.Name);
        Assert.False(state.HasActive);
        Assert.Equal("原序", state.Caption);
        var earlier = Fact(1, "z", stars: 5, size: 9);
        var later = Fact(4, "a", stars: 0, size: 1);
        Assert.True(GridSort.Compare(earlier, later, state.Rules) < 0);
    }

    [Fact]
    public void Tie_KeepsCatalogIndex()
    {
        var rules = GridSortState.CreateDefault().Rules;
        Assert.True(GridSort.Compare(Fact(0, "a"), Fact(3, "a"), rules) < 0);
        Assert.True(GridSort.Compare(Fact(3, "a"), Fact(0, "a"), rules) > 0);
    }

    [Fact]
    public void MissingTime_SortsLastWhenAscending()
    {
        var state = GridSortState.CreateDefault();
        state.Toggle(GridSortKey.Time);
        var old = Fact(1, time: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var missing = Fact(0);
        Assert.True(GridSort.Compare(old, missing, state.Rules) < 0);
        state.Flip(GridSortKey.Time);
        Assert.Equal(1, state.Priority(GridSortKey.Time));
        Assert.True(GridSort.Compare(missing, old, state.Rules) < 0);
    }

    [Fact]
    public void Size_TreatsZeroAsSmallest()
    {
        var state = GridSortState.CreateDefault();
        state.Toggle(GridSortKey.Size);
        Assert.True(GridSort.Compare(Fact(0, size: 0), Fact(1, size: 20), state.Rules) < 0);
    }

    [Fact]
    public void Preferences_RoundTrip_TimeDescending_AndUnknownKeySkipped()
    {
        var state = GridSortState.CreateDefault();
        state.Toggle(GridSortKey.Time);
        state.Flip(GridSortKey.Time);
        var settings = new AppSettings
        {
            GridSort = state.ToPreferences()
        };
        settings.GridSort.Insert(0, new GridSortPreference { Key = "render", Dir = "desc" });
        var json = JsonSerializer.Serialize(settings, JsonUtil.Options);
        Assert.Contains("\"key\": \"time\"", json, StringComparison.Ordinal);
        Assert.Contains("\"dir\": \"desc\"", json, StringComparison.Ordinal);

        var back = JsonSerializer.Deserialize<AppSettings>(json, JsonUtil.Options);
        var restored = GridSortState.FromPreferences(back!.GridSort);
        Assert.Equal(1, restored.Priority(GridSortKey.Time));
        Assert.Equal(GridSortDir.Desc, restored.Dir(GridSortKey.Time));
        Assert.Equal(0, restored.Priority(GridSortKey.Name));
        Assert.Equal("按时间 ▼", restored.Caption);
    }

    [Fact]
    public void Preferences_MissingField_StaysOriginalOrder()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}", JsonUtil.Options);
        var state = GridSortState.FromPreferences(settings!.GridSort);
        Assert.False(state.HasActive);
        Assert.Equal("原序", state.Caption);
    }

    private static GridSortFact Fact(
        int index,
        string name = "",
        DateTime? time = null,
        int stars = 0,
        long size = 0)
    {
        return new GridSortFact(index, name, time, stars, size);
    }
}
