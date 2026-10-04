using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class CopyCatalogTests
{
    [Fact]
    public void CopyText_TreatsPlaceholderAsEmpty()
    {
        Assert.False(CopyText.IsFilled(null));
        Assert.False(CopyText.IsFilled(""));
        Assert.False(CopyText.IsFilled("  "));
        Assert.False(CopyText.IsFilled(CopyText.Placeholder));
        Assert.True(CopyText.IsFilled("公园项目说明。"));
        Assert.Equal("", CopyText.ForEditor(CopyText.Placeholder));
        Assert.Equal("公园项目说明。", CopyText.ForEditor("公园项目说明。"));
    }

    [Fact]
    public void JsonCatalog_ReadsChannelLeadWorkSummaryAndMediaCopy()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        Assert.Equal("", session.ChannelLeadDict["demo-render"]);
        Assert.Equal("演示风光栏目。", session.ChannelLeadDict["demo-photo"]);

        var park = Assert.Single(session.Works, item => item.Id == "demo-park");
        Assert.Equal("公园项目说明。", park.Summary);
        var first = Assert.Single(park.Media, item => item.Src != null && item.Src.EndsWith("/01.png", StringComparison.Ordinal));
        Assert.Equal("效果图 01", first.Label);
        Assert.Equal("公园入口", first.DisplayName);
        Assert.Equal("仅此张资源说明。", first.Description);
        Assert.Equal("仅此张资源说明。", CopyDisplayRules.ResolveMediaDescription(first.Description, park.Summary));
        var second = Assert.Single(park.Media, item => item.Src != null && item.Src.EndsWith("/02.png", StringComparison.Ordinal));
        Assert.Equal("效果图 02", second.Label);
        Assert.False(CopyText.IsFilled(second.DisplayName));
        Assert.False(CopyText.IsFilled(second.Description));
        Assert.Equal("公园项目说明。", CopyDisplayRules.ResolveMediaDescription(second.Description, park.Summary));
        var yard = Assert.Single(session.Works, item => item.Id == "demo-yard");
        Assert.Equal("", yard.Summary);
        var walk = Assert.Single(session.Works, item => item.Id == "demo-walk");
        Assert.Equal("步道项目说明。", walk.Summary);

        var site = Assert.Single(
            session.SiteItems,
            item => item.WorkId == "demo-park" && item.ObjectKey.EndsWith("/01.png", StringComparison.Ordinal));
        Assert.Equal("公园项目说明。", site.WorkSummary);
        Assert.Equal("公园入口", site.DisplayName);
        Assert.Equal("仅此张资源说明。", site.Description);
    }

    [Fact]
    public void CopyOwner_SiteCardUsesWorkId_StageCardDoesNotFallBackToFirst()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var yardSite = Assert.Single(session.SiteItems, item => item.WorkId == "demo-yard");
        Assert.Equal("demo-yard", CopyOwner.ResolveWorkId(session, null, yardSite));

        var stage = session.StageItems.FirstOrDefault(item =>
            item.ChannelKey == "demo-render" && item.WorkIdGuess == "demo-yard");
        if (stage != null)
        {
            Assert.Equal("demo-yard", CopyOwner.ResolveWorkId(session, stage, null));
        }

        Assert.Null(CopyOwner.ResolveWorkId(session, null, null));
        Assert.True(CopyOwner.HidesCopyName("profile"));
        Assert.True(CopyOwner.HidesCopyDescription("portrait-photo"));
        Assert.False(CopyOwner.HidesCopyName("demo-render"));
    }

    [Fact]
    public void PersonalWorks_ReadsWorkSummaryAndChannelLead()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var mansheng = session.Works.FirstOrDefault(item => item.Id == "shanghai-mansheng-packaging");
        Assert.NotNull(mansheng);
        Assert.True(CopyText.IsFilled(mansheng!.Summary));
        Assert.True(session.ChannelLeadDict.ContainsKey("digital-twin"));
        Assert.False(CopyText.IsFilled(session.ChannelLeadDict["digital-twin"]));
        Assert.True(CopyText.IsFilled(session.ChannelLeadDict["game-dev"]));
    }

    [Fact]
    public void PersonalWorks_ReadsObjectMediaCopyFieldsWithoutDroppingTuples()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var mansheng = Assert.Single(session.Works, item => item.Id == "shanghai-mansheng-packaging");
        Assert.Equal(9, mansheng.Media.Count);
        Assert.Contains(mansheng.Media, item =>
            item.Src == "digital-twin/shanghai-mansheng-packaging/01.webp");
        var video = Assert.Single(
            mansheng.Media,
            item => item.Src == "digital-twin/shanghai-mansheng-packaging/09.mp4");
        Assert.Equal("video", video.Kind);
        Assert.Equal("视频", video.Label);
        Assert.Equal("立库动画", video.DisplayName);
        Assert.Equal("立库动画测试", video.Description);

        var site = Assert.Single(
            session.SiteItems,
            item => item.ObjectKey == "digital-twin/shanghai-mansheng-packaging/09.mp4");
        Assert.Equal("立库动画", site.DisplayName);
        Assert.Equal("立库动画测试", site.Description);
    }
}
