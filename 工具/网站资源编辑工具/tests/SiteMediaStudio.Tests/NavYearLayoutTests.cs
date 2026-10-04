using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NavYearLayoutTests
{
    [Fact]
    public void Arrange_WithoutYearContainers_KeepsWorksOnTheChannel()
    {
        var channel = new ChannelProfile { Key = "landscape-rendering", Zh = "景观效果图" };
        var registered = new[] { Work("xiaowayao", "landscape-rendering") };
        var layout = NavYearLayout.Arrange(channel, registered, Array.Empty<WorkCatalogItem>(), Array.Empty<StageItem>());

        Assert.Empty(layout.YearRows);
        Assert.Equal("xiaowayao", Assert.Single(layout.LooseRegistered).Id);
    }

    [Fact]
    public void Arrange_GroupsByYearFolder_RegisteredAndUnregisteredTogether()
    {
        var channel = PhotoChannel();
        var registered = new[]
        {
            Work("20191125 新加坡", "real-world-photo", "real-world-photo/20191125 新加坡"),
            Work("loose", "real-world-photo", "real-world-photo/loose")
        };
        var unregistered = new[]
        {
            Work("20200101 上海", "real-world-photo", "real-world-photo/2020/20200101 上海", unregistered: true)
        };
        var stageItems = new[]
        {
            Stage("20191125 新加坡", "2019/20191125 新加坡")
        };

        var layout = NavYearLayout.Arrange(channel, registered, unregistered, stageItems);

        Assert.Equal(new[] { "2019", "2020" }, layout.YearRows.Select(row => row.Year));
        Assert.Equal("20191125 新加坡", Assert.Single(layout.YearRows[0].Works).Id);
        Assert.Equal("20200101 上海", Assert.Single(layout.YearRows[1].Works).Id);
        Assert.Equal("loose", Assert.Single(layout.LooseRegistered).Id);
        Assert.Empty(layout.LooseUnregistered);
    }

    [Fact]
    public void TryReadYearFolder_ReadsFourDigitSegment_AfterChannelKey()
    {
        var channel = PhotoChannel();
        Assert.True(StageWorkFolder.TryReadYearFolder(channel, "real-world-photo/2019/20191125 新加坡", out var year));
        Assert.Equal("2019", year);
        Assert.False(StageWorkFolder.TryReadYearFolder(channel, "real-world-photo/20191125 新加坡", out _));
        Assert.False(StageWorkFolder.TryReadYearFolder(
            new ChannelProfile { Key = "landscape-rendering" },
            "2019/shoot",
            out _));
    }

    private static ChannelProfile PhotoChannel()
    {
        return new ChannelProfile
        {
            Key = "real-world-photo",
            Zh = "现实摄影",
            YearContainers = true
        };
    }

    private static WorkCatalogItem Work(string id, string channel, string? stageFolder = null, bool unregistered = false)
    {
        return new WorkCatalogItem
        {
            Id = id,
            Channel = channel,
            Title = id,
            IsUnregistered = unregistered,
            StageFolder = stageFolder
        };
    }

    private static StageItem Stage(string workId, string stageFolderGuess)
    {
        return new StageItem
        {
            StageRel = stageFolderGuess + "/01.jpg",
            FullPath = "D:/tmp/" + stageFolderGuess + "/01.jpg",
            ChannelKey = "real-world-photo",
            WorkIdGuess = workId,
            StageFolderGuess = stageFolderGuess
        };
    }
}
