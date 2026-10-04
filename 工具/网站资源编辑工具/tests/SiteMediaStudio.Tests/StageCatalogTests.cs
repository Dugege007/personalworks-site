using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class StageCatalogTests
{
    [Fact]
    public void Scan_Fixture_IncludesUnpublishedAndStock()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        Assert.Contains(session.StageItems, item => item.StageRel.EndsWith("demo-park/03.png", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(session.StageItems, item => item.IsStock);
        var published = session.StageItems.First(item => item.StageRel.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("published", published.LedgerStatus);
        var unused = session.StageItems.First(item => item.StageRel.Contains("unused", StringComparison.OrdinalIgnoreCase));
        Assert.True(string.IsNullOrEmpty(unused.LedgerStatus) || unused.LedgerStatus != "published");
        Assert.Contains(session.SiteItems, item => item.ObjectKey == "demo-render/demo-park/01.png" && item.FullPath != null);
    }

    [Fact]
    public void Scan_ObjectKeyDoesNotClaimNeighborWhenLedgerNamesAnotherPath()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var source = session.StageItems.First(item =>
            item.StageRel.EndsWith("demo-park/04.png", StringComparison.OrdinalIgnoreCase));
        var neighbor = session.StageItems.First(item =>
            item.StageRel.EndsWith("demo-park/03.png", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("demo-render/demo-park/03.png", source.MatchedObject);
        Assert.Equal("published", source.LedgerStatus);
        Assert.True(string.IsNullOrEmpty(neighbor.MatchedObject));
        Assert.True(string.IsNullOrEmpty(neighbor.LedgerStatus));
    }

    [Theory]
    [InlineData("数字孪生（digital-twin）/foo/.site-ready/id/01.webp", true)]
    [InlineData("foo/.site-ready/01.webp", true)]
    [InlineData(".site-ready/id/01.webp", true)]
    [InlineData("数字孪生（digital-twin）/foo/01.png", false)]
    [InlineData("clip.mp4", false)]
    public void PathRules_SkipSiteReadyDerived(string rel, bool derived)
    {
        Assert.Equal(derived, MediaPathRules.IsDerivedStageRel(rel));
        if (derived)
        {
            Assert.False(MediaPathRules.IsCatalogFile(rel));
        }
    }

    [Fact]
    public void Scan_InheritsLedgerFromSiteReadySibling()
    {
        var ledgerByStage = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            ["数字孪生（digital-twin）/20240408 上海曼盛包装/.site-ready/shanghai-mansheng-packaging/01.webp"]
                = new LedgerRecord
                {
                    Object = "digital-twin/shanghai-mansheng-packaging/01.webp",
                    StageRel = "数字孪生（digital-twin）/20240408 上海曼盛包装/.site-ready/shanghai-mansheng-packaging/01.webp",
                    Status = "published"
                }
        };

        var hit = StageCatalogScanner.FindDerivedLedger(
            "数字孪生（digital-twin）/20240408 上海曼盛包装/01.png",
            ledgerByStage);
        Assert.NotNull(hit);
        Assert.Equal("digital-twin/shanghai-mansheng-packaging/01.webp", hit!.Object);

        Assert.DoesNotContain(
            ledgerByStage.Keys,
            key => MediaPathRules.IsCatalogFile(key) && MediaPathRules.IsDerivedStageRel(key));
    }

    [Fact]
    public void Scan_InheritsLedgerFromLeadingIndex_IgnoresVideoAndSuffix()
    {
        var ledgerByStage = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            ["数字孪生（digital-twin）/20240926 潍柴火炬/.site-ready/weichai-spark-plug/01.webp"]
                = new LedgerRecord
                {
                    Object = "digital-twin/weichai-spark-plug/01.webp",
                    StageRel = "数字孪生（digital-twin）/20240926 潍柴火炬/.site-ready/weichai-spark-plug/01.webp",
                    Status = "published"
                },
            ["数字孪生（digital-twin）/20240408 上海曼盛包装/.site-ready/shanghai-mansheng-packaging/04.webp"]
                = new LedgerRecord
                {
                    Object = "digital-twin/shanghai-mansheng-packaging/04.webp",
                    StageRel = "数字孪生（digital-twin）/20240408 上海曼盛包装/.site-ready/shanghai-mansheng-packaging/04.webp",
                    Status = "published"
                }
        };

        var named = StageCatalogScanner.FindDerivedLedger(
            "数字孪生（digital-twin）/20240926 潍柴火炬/01 车间总览.png",
            ledgerByStage);
        Assert.Equal("digital-twin/weichai-spark-plug/01.webp", named!.Object);

        Assert.Null(StageCatalogScanner.FindDerivedLedger(
            "数字孪生（digital-twin）/20240926 潍柴火炬/01 整体场景_0.7.0.mp4",
            ledgerByStage));
        Assert.Null(StageCatalogScanner.FindDerivedLedger(
            "数字孪生（digital-twin）/20240408 上海曼盛包装/04_1.png",
            ledgerByStage));
    }

    [Fact]
    public void BuildStageIndex_UsesSourceStageRel_WhenFileNameDoesNotMatchSlot()
    {
        var ledgerDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            ["landscape-photo/singapore-20191125/01.webp"] = new LedgerRecord
            {
                Object = "landscape-photo/singapore-20191125/01.webp",
                StageRel = "风光摄影（landscape-photo）/20191125 新加坡/.site-ready/singapore-20191125/01.webp",
                SourceStageRel = "风光摄影（landscape-photo）/20191125 新加坡/P1330899-3.jpg",
                Status = "published"
            },
            ["landscape-rendering/henan-tech-landscape-graduation/01.webp"] = new LedgerRecord
            {
                Object = "landscape-rendering/henan-tech-landscape-graduation/01.webp",
                StageRel = "景观效果图（landscape-rendering）/毕业设计/.site-ready/henan-tech-landscape-graduation/01.webp",
                SourceStageRel = "景观效果图（landscape-rendering）/毕业设计/1.jpg",
                Status = "published"
            }
        };

        var index = StageCatalogScanner.BuildStageIndex(ledgerDict);
        Assert.Equal(
            "landscape-photo/singapore-20191125/01.webp",
            index["风光摄影（landscape-photo）/20191125 新加坡/P1330899-3.jpg"].Object);
        Assert.Equal(
            "landscape-rendering/henan-tech-landscape-graduation/01.webp",
            index["景观效果图（landscape-rendering）/毕业设计/1.jpg"].Object);

        Assert.Null(StageCatalogScanner.FindDerivedLedger(
            "风光摄影（landscape-photo）/20191125 新加坡/P1330899-3.jpg",
            index));
        Assert.Null(StageCatalogScanner.FindDerivedLedger(
            "景观效果图（landscape-rendering）/毕业设计/1.jpg",
            index));
    }

    [Fact]
    public void BuildStageIndex_VideoSourceStageRel_PairsNamedClipToSlot()
    {
        var ledgerDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            ["digital-twin/shanghai-mansheng-packaging/08.mp4"] = new LedgerRecord
            {
                Object = "digital-twin/shanghai-mansheng-packaging/08.mp4",
                StageRel = "数字孪生（digital-twin）/20240408 上海曼盛包装/.site-ready/shanghai-mansheng-packaging/08.mp4",
                SourceStageRel = "数字孪生（digital-twin）/20240408 上海曼盛包装/01 曼盛AGV演示_0.2.mp4",
                Status = "published"
            }
        };

        var index = StageCatalogScanner.BuildStageIndex(ledgerDict);
        var source = "数字孪生（digital-twin）/20240408 上海曼盛包装/01 曼盛AGV演示_0.2.mp4";
        Assert.Equal("digital-twin/shanghai-mansheng-packaging/08.mp4", index[source].Object);
        Assert.True(ComparePairing.IsAutoPair(
            new StageItem
            {
                StageRel = source,
                FullPath = "x",
                ChannelKey = "digital-twin",
                MatchedObject = index[source].Object
            },
            new SiteItem
            {
                WorkId = "shanghai-mansheng-packaging",
                ChannelKey = "digital-twin",
                WorkTitle = "上海曼盛包装",
                Label = "视频",
                ObjectKey = "digital-twin/shanghai-mansheng-packaging/08.mp4",
                StageRel = ledgerDict["digital-twin/shanghai-mansheng-packaging/08.mp4"].StageRel
            }));
        Assert.Null(StageCatalogScanner.FindDerivedLedger(source, index));
    }

    [Fact]
    public void BuildStageIndex_StageRelWinsOverSourceStageRel()
    {
        var occupied = new LedgerRecord
        {
            Object = "demo-render/demo-park/01.png",
            StageRel = "demo-render/demo-park/01.png",
            Status = "published"
        };
        var derived = new LedgerRecord
        {
            Object = "demo-render/demo-park/99.webp",
            StageRel = "demo-render/demo-park/.site-ready/demo-park/99.webp",
            SourceStageRel = "demo-render/demo-park/01.png",
            Status = "published"
        };
        var ledgerDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            [occupied.Object] = occupied,
            [derived.Object] = derived
        };

        var index = StageCatalogScanner.BuildStageIndex(ledgerDict);
        Assert.Equal(occupied.Object, index["demo-render/demo-park/01.png"].Object);
        Assert.Equal(derived.Object, index["demo-render/demo-park/.site-ready/demo-park/99.webp"].Object);
    }

    [Fact]
    public void BuildStageIndex_WithdrawnStageRelYieldsToPublishedSource()
    {
        var source = "景观施工图（landscape-cds）/上海道田景观工程咨询有限公司/201709 世茂北京一渡/01.desense.jpg";
        var withdrawn = new LedgerRecord
        {
            Object = "landscape-cds/shimao-beijing-yidu/01.jpg",
            StageRel = source,
            Status = "withdrawn"
        };
        var published = new LedgerRecord
        {
            Object = "landscape-cds/shimao-beijing-yidu/01.webp",
            StageRel = "景观施工图（landscape-cds）/上海道田景观工程咨询有限公司/201709 世茂北京一渡/.site-ready/shimao-beijing-yidu/01.webp",
            SourceStageRel = source,
            Status = "published"
        };
        var ledgerDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            [withdrawn.Object] = withdrawn,
            [published.Object] = published
        };

        var index = StageCatalogScanner.BuildStageIndex(ledgerDict);
        Assert.Equal(published.Object, index[source].Object);
        Assert.Equal("published", index[source].Status);
        Assert.Equal(published.Object, index[published.StageRel].Object);
    }
}
