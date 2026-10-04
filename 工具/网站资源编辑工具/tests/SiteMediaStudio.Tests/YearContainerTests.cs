using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class YearContainerTests
{
    [Fact]
    public void YearFolder_IsContainer_DatedShootIsNot()
    {
        var channel = PhotoChannel();
        Assert.True(StageWorkFolder.IsYearFolder(channel, "2024"));
        Assert.True(StageWorkFolder.IsContainer(channel, "2024"));
        Assert.False(StageWorkFolder.IsYearFolder(channel, "20241119 上海"));
        Assert.False(StageWorkFolder.IsContainer(channel, "20241119 上海"));
        Assert.False(StageWorkFolder.IsYearFolder(new ChannelProfile { Key = "landscape-rendering" }, "2024"));
    }

    [Fact]
    public void TryRead_YearNestAndChannelRoot()
    {
        var channel = PhotoChannel();
        Assert.True(StageWorkFolder.TryRead(
            "2024/nested-shoot/01.jpg",
            channel,
            out var nestedId,
            out var nestedFolder));
        Assert.Equal("nested-shoot", nestedId);
        Assert.Equal("2024/nested-shoot", nestedFolder);

        Assert.True(StageWorkFolder.TryRead(
            "loose-shoot/01.jpg",
            channel,
            out var looseId,
            out var looseFolder));
        Assert.Equal("loose-shoot", looseId);
        Assert.Equal("loose-shoot", looseFolder);

        Assert.False(StageWorkFolder.TryRead("2024/01.jpg", channel, out _, out _));
        Assert.False(StageWorkFolder.TryRead("2024", channel, out _, out _));
    }

    [Fact]
    public void Discover_YearFolderIsNotWork_ShootsAre()
    {
        var profile = new WorkspaceProfile { Channels = [PhotoChannel()] };
        var stageList = new[]
        {
            Stage("real-world-photo/2024/nested-shoot/01.jpg", "nested-shoot", "2024/nested-shoot"),
            Stage("real-world-photo/loose-shoot/01.jpg", "loose-shoot", "loose-shoot")
        };

        var found = UnregisteredWorkDiscovery.Discover([], stageList, profile: profile);
        Assert.DoesNotContain(found, work => work.Id == "2024");
        Assert.Contains(found, work =>
            work.Id == "nested-shoot"
            && work.StageFolder == "real-world-photo/2024/nested-shoot");
        Assert.Contains(found, work => work.Id == "loose-shoot");
    }

    [Fact]
    public void Discover_MovedShootUnderYear_StaysClaimedByFolderName()
    {
        var profile = new WorkspaceProfile { Channels = [PhotoChannel("landscape-photo")] };
        var registeredList = new[]
        {
            new WorkCatalogItem
            {
                Id = "hebi-stars-20241002",
                Channel = "landscape-photo",
                Title = "鹤壁 星空"
            },
            new WorkCatalogItem
            {
                Id = "hebi-stars-20220912",
                Channel = "landscape-photo",
                Title = "鹤壁 星空"
            }
        };
        var stageList = new[]
        {
            Stage(
                "风光摄影（landscape-photo）/2024/20241002 鹤壁 星空/DSC00768.jpg",
                "20241002 鹤壁 星空",
                "2024/20241002 鹤壁 星空",
                "landscape-photo")
        };

        var found = UnregisteredWorkDiscovery.Discover(registeredList, stageList, profile: profile);
        Assert.DoesNotContain(found, work => work.Id == "2024");
        Assert.DoesNotContain(found, work => work.Id == "20241002 鹤壁 星空");
    }

    [Fact]
    public void Register_RejectsYearFolder_AllowsShootUnderIt()
    {
        var channel = PhotoChannel();
        Assert.False(WorkRegisterRules.IsAllowedStageFolder(
            "real-world-photo",
            "real-world-photo/2024",
            channel));
        Assert.True(WorkRegisterRules.IsAllowedStageFolder(
            "real-world-photo",
            "real-world-photo/2024/nested-shoot",
            channel));
        Assert.True(WorkRegisterRules.IsAllowedStageFolder(
            "real-world-photo",
            "real-world-photo/20260921 上海外滩",
            channel));
        Assert.True(WorkRegisterRules.IsAllowedStageFolder(
            "real-world-photo",
            "real-world-photo/20241119 上海",
            channel));
    }

    [Fact]
    public void PersonalWorksProfile_PhotoChannelsUseYearContainers()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var profile = WorkspaceProfileLoader.Load(profilePath!);
        var real = Assert.Single(profile.Channels, item => item.Key == "real-world-photo");
        Assert.True(real.YearContainers);
        Assert.DoesNotContain(profile.Channels, item =>
            item.Key is "landscape-photo" or "humanist-photo" or "portrait-photo");

        var render = Assert.Single(profile.Channels, item => item.Key == "landscape-rendering");
        Assert.False(render.YearContainers);
        var gamePhoto = Assert.Single(profile.Channels, item => item.Key == "game-photo");
        Assert.False(gamePhoto.YearContainers);
    }

    private static ChannelProfile PhotoChannel(string key = "real-world-photo")
    {
        return new ChannelProfile
        {
            Key = key,
            YearContainers = true
        };
    }

    private static StageItem Stage(
        string stageRel,
        string workIdGuess,
        string stageFolderGuess,
        string channel = "real-world-photo")
    {
        return new StageItem
        {
            StageRel = stageRel,
            FullPath = @"D:\stage\01.jpg",
            ChannelKey = channel,
            WorkIdGuess = workIdGuess,
            StageFolderGuess = stageFolderGuess
        };
    }
}
