using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class WorkspaceProfileTests
{
    [Fact]
    public void Load_FixtureProfile_ResolvesStageAndJsonCatalog()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        Assert.Equal("模拟站点", session.Profile.Name);
        Assert.Equal("Key", session.Profile.StageFolderPattern);
        Assert.Contains(session.Works, work => work.Id == "demo-park");
        Assert.Equal(2, session.Works.First(work => work.Id == "demo-park").Media.Count);
        Assert.True(session.LedgerDict.ContainsKey("demo-render/demo-park/01.png"));
    }

    [Fact]
    public void ChannelFolder_ZhKey_UsesChineseAndKey()
    {
        var profile = new WorkspaceProfile { StageFolderPattern = "ZhKey" };
        var channel = new ChannelProfile { Key = "landscape-rendering", Zh = "景观效果图" };
        Assert.Equal("景观效果图（landscape-rendering）", WorkspaceProfileLoader.ChannelStageFolderName(profile, channel));
    }

    [Theory]
    [InlineData("profile", "形象照", "PROFILE", "形象照（profile）")]
    [InlineData("game-dev", "游戏开发", "GAME DEV", "游戏开发（game-dev）")]
    [InlineData("game-photo", "游戏摄影", "GAME PHOTO", "游戏摄影（game-photo）")]
    public void PersonalWorksProfile_IncludesFrozenExtraChannels(
        string key,
        string zh,
        string deco,
        string folder)
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var profile = WorkspaceProfileLoader.Load(profilePath!);
        var channel = Assert.Single(profile.Channels, item => item.Key == key);
        Assert.Equal(zh, channel.Zh);
        Assert.Equal(deco, channel.Deco);
        Assert.Equal(folder, WorkspaceProfileLoader.ChannelStageFolderName(profile, channel));
    }

    [Fact]
    public void PersonalWorksProfile_MarksUnavailableChannelCapabilities()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var profile = WorkspaceProfileLoader.Load(profilePath!);
        var gamePhoto = Assert.Single(profile.Channels, item => item.Key == "game-photo");

        Assert.False(gamePhoto.Capabilities.Catalog);
        Assert.False(gamePhoto.Capabilities.Ingest);
        Assert.False(gamePhoto.Capabilities.Hide);
        Assert.False(gamePhoto.Capabilities.Withdraw);
        Assert.DoesNotContain(profile.Channels, item =>
            item.Key is "home-page" or "notes" or "profile-resume" or "profile-skills");
    }

    [Fact]
    public void PersonalWorksProfile_ListsKnownStageContainers()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var profile = WorkspaceProfileLoader.Load(profilePath!);
        var gameDev = Assert.Single(profile.Channels, item => item.Key == "game-dev");
        Assert.Contains("参赛作品", gameDev.StageContainerFolders);
        Assert.Contains("技术文章", gameDev.StageContainerFolders);
        Assert.Contains("练习作品", gameDev.StageContainerFolders);
        var lineSim = Assert.Single(profile.Channels, item => item.Key == "line-sim");
        Assert.Contains("FlexSim", lineSim.StageContainerFolders);
        var render = Assert.Single(profile.Channels, item => item.Key == "landscape-rendering");
        Assert.Contains("上海道田景观工程咨询有限公司", render.StageContainerFolders);
        var cds = Assert.Single(profile.Channels, item => item.Key == "landscape-cds");
        Assert.Contains("上海日清景观设计有限公司", cds.StageContainerFolders);
    }
}
