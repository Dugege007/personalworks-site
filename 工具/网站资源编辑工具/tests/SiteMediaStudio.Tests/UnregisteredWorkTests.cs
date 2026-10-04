using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class UnregisteredWorkTests
{
    [Fact]
    public void Fixture_ListsNewFolderAndLeftoverAsUnregistered()
    {
        var session = LoadFixture();
        Assert.Contains(
            session.Works,
            work => work.Id == "new-folder" && work.Channel == "demo-render" && work.IsUnregistered);
        Assert.Contains(
            session.Works,
            work => work.Id == "leftover" && work.Channel == "demo-render" && work.IsUnregistered);
        Assert.DoesNotContain(
            session.Works,
            work => work.Id == "stock" && work.IsUnregistered);
        Assert.Contains(
            session.Works,
            work => work.Id == "nested-new"
                && work.Channel == "demo-render"
                && work.IsUnregistered
                && work.StageFolder == "demo-render/studio-a/nested-new");
        Assert.DoesNotContain(session.Works, work => work.Id == "studio-a" && work.IsUnregistered);
        Assert.False(session.Works.First(work => work.Id == "demo-park").IsUnregistered);
        Assert.DoesNotContain(session.SiteItems, item => item.WorkId == "new-folder");
    }

    [Fact]
    public void Fixture_NestedFolderUnderContainer_GuessesSecondLevel()
    {
        var session = LoadFixture();
        var stage = session.StageItems.First(item =>
            item.StageRel.Replace('\\', '/').EndsWith("studio-a/nested-new/01.png", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("nested-new", stage.WorkIdGuess);
        Assert.Equal("studio-a/nested-new", stage.StageFolderGuess);
    }

    [Fact]
    public void Fixture_ExistingWorkFolderIsNotUnregistered()
    {
        var session = LoadFixture();
        var registeredList = UnregisteredWorkDiscovery.RegisteredInChannel(session.Works, "demo-render");
        var unregisteredList = UnregisteredWorkDiscovery.UnregisteredInChannel(session.Works, "demo-render");
        Assert.Contains(registeredList, work => work.Id == "demo-park");
        Assert.DoesNotContain(unregisteredList, work => work.Id == "demo-park");
        Assert.DoesNotContain(unregisteredList, work => work.Id == "demo-yard");
    }

    [Fact]
    public void Ingest_UnregisteredTarget_IsRejected()
    {
        var session = LoadFixture();
        var stage = session.StageItems.First(item =>
            item.StageRel.Replace('\\', '/').EndsWith("new-folder/01.png", StringComparison.OrdinalIgnoreCase));
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageIngest,
            stage,
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                WorkList = session.Works,
                TargetWorkId = "new-folder"
            });

        Assert.False(decision.Allowed);
        Assert.Contains("须先登记或改选已有作品", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ingest_UnregisteredGuessWithoutTarget_IsRejected()
    {
        var session = LoadFixture();
        var stage = session.StageItems.First(item =>
            item.StageRel.Replace('\\', '/').EndsWith("leftover/unused.png", StringComparison.OrdinalIgnoreCase));
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageIngest,
            stage,
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                WorkList = session.Works,
                TargetWorkId = null
            });

        Assert.False(decision.Allowed);
        Assert.Contains("须先登记或改选已有作品", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ingest_UnregisteredFileIntoRegisteredWork_IsAllowed()
    {
        var session = LoadFixture();
        var stage = session.StageItems.First(item =>
            item.StageRel.Replace('\\', '/').EndsWith("leftover/unused.png", StringComparison.OrdinalIgnoreCase));
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageIngest,
            stage,
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                WorkList = session.Works,
                TargetWorkId = "demo-park"
            });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Compare_UnregisteredFolder_DoesNotPairByFolderName()
    {
        var session = LoadFixture();
        var rows = ComparePairing.Build(session, "demo-render", "new-folder", Array.Empty<ManualPin>());
        Assert.DoesNotContain(rows, row => row.Kind == CompareRowKind.Paired);
        Assert.Contains(rows, row =>
            row.Kind == CompareRowKind.StageOnly
            && row.Stage != null
            && row.Stage.WorkIdGuess == "new-folder");
        Assert.DoesNotContain(rows, row => row.Site?.WorkId == "demo-park");
    }

    [Fact]
    public void Discover_ClaimsDatedFolderFromLedgerStageRel()
    {
        var registeredList = new[]
        {
            new WorkCatalogItem
            {
                Id = "ningbo-siweier",
                Channel = "digital-twin",
                Title = "宁波四维尔"
            }
        };
        var stageList = new[]
        {
            new StageItem
            {
                StageRel = "数字孪生（digital-twin）/20240530_宁波四维尔/微信截图.png",
                FullPath = @"D:\stage\微信截图.png",
                ChannelKey = "digital-twin",
                WorkIdGuess = "20240530_宁波四维尔",
                StageFolderGuess = "20240530_宁波四维尔"
            }
        };
        var ledgerDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            ["digital-twin/ningbo-siweier/01.webp"] = new LedgerRecord
            {
                Object = "digital-twin/ningbo-siweier/01.webp",
                StageRel = "数字孪生（digital-twin）/20240530_宁波四维尔/.site-ready/ningbo-siweier/01.webp",
                Status = "published"
            }
        };

        var found = UnregisteredWorkDiscovery.Discover(registeredList, stageList, ledgerDict);
        Assert.DoesNotContain(found, work => work.Id == "20240530_宁波四维尔");
        Assert.DoesNotContain(
            UnregisteredWorkDiscovery.Discover(registeredList, stageList),
            work => work.Id == "20240530_宁波四维尔");
    }

    [Fact]
    public void Discover_ClaimsWorkFolder_WhenLedgerPointsAtBatchSubfolder()
    {
        var profile = new WorkspaceProfile
        {
            Channels =
            [
                new ChannelProfile
                {
                    Key = "landscape-rendering",
                    StageContainerFolders = ["上海道田景观工程咨询有限公司"]
                }
            ]
        };
        var registeredList = new[]
        {
            new WorkCatalogItem
            {
                Id = "harbin-jiangyufu",
                Channel = "landscape-rendering",
                Title = "哈尔滨江御府"
            }
        };
        var stageList = new[]
        {
            new StageItem
            {
                StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/202008 哈尔滨江御府/20200813_HEBZSQ_DU/备选.jpg",
                FullPath = @"D:\stage\备选.jpg",
                ChannelKey = "landscape-rendering",
                WorkIdGuess = "202008 哈尔滨江御府",
                StageFolderGuess = "上海道田景观工程咨询有限公司/202008 哈尔滨江御府"
            }
        };
        var ledgerDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal)
        {
            ["landscape-rendering/harbin-jiangyufu/07.webp"] = new LedgerRecord
            {
                Object = "landscape-rendering/harbin-jiangyufu/07.webp",
                StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/202008 哈尔滨江御府/20200813_HEBZSQ_DU/.site-ready/harbin-jiangyufu/07.webp",
                Status = "published"
            }
        };

        var found = UnregisteredWorkDiscovery.Discover(registeredList, stageList, ledgerDict, profile);
        Assert.DoesNotContain(found, work => work.Id == "202008 哈尔滨江御府");
        Assert.DoesNotContain(found, work => work.Id == "20200813_HEBZSQ_DU");
        Assert.DoesNotContain(
            UnregisteredWorkDiscovery.Discover(registeredList, stageList, profile: profile),
            work => work.Id == "202008 哈尔滨江御府");
    }

    [Fact]
    public void Discover_ClaimsFolder_WhenTitleMatchesAfterStrippingDate()
    {
        var registeredList = new[]
        {
            new WorkCatalogItem
            {
                Id = "xiaowayao",
                Channel = "landscape-rendering",
                Title = "北京丰台小瓦窑"
            }
        };
        var stageList = new[]
        {
            new StageItem
            {
                StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑/备选.jpg",
                FullPath = @"D:\stage\备选.jpg",
                ChannelKey = "landscape-rendering",
                WorkIdGuess = "201804 北京丰台小瓦窑",
                StageFolderGuess = "上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑"
            }
        };

        var found = UnregisteredWorkDiscovery.Discover(registeredList, stageList);
        Assert.DoesNotContain(found, work => work.Id == "201804 北京丰台小瓦窑");
        Assert.Equal("北京丰台小瓦窑", StageFolderClaim.StripLeadingDate("201804 北京丰台小瓦窑"));
        Assert.Equal("宁波四维尔", StageFolderClaim.StripLeadingDate("20240530_宁波四维尔"));
    }

    [Fact]
    public void TryClaim_SameTitleDifferentDate_PicksDatedWork()
    {
        var works = new[]
        {
            new WorkCatalogItem
            {
                Id = "hebi-stars-20220912",
                Channel = "landscape-photo",
                Title = "鹤壁 星空"
            },
            new WorkCatalogItem
            {
                Id = "hebi-stars-20241002",
                Channel = "landscape-photo",
                Title = "鹤壁 星空"
            }
        };

        Assert.True(StageFolderClaim.TryClaim(
            works,
            "landscape-photo",
            "20241002 鹤壁 星空",
            null,
            out var october));
        Assert.Equal("hebi-stars-20241002", october!.Id);
        Assert.True(StageFolderClaim.TryClaim(
            works,
            "landscape-photo",
            "20220912 鹤壁 星空",
            null,
            out var september));
        Assert.Equal("hebi-stars-20220912", september!.Id);
    }

    [Fact]
    public void Discover_FlatFileChannel_DoesNotListDatedFolderAsUnregistered()
    {
        var profile = new WorkspaceProfile
        {
            Channels =
            [
                new ChannelProfile
                {
                    Key = "profile",
                    ObjectKeyPattern = "flat-file"
                }
            ]
        };
        var registeredList = new[]
        {
            new WorkCatalogItem
            {
                Id = "仪摄影写真",
                Channel = "profile",
                Title = "仪摄影写真"
            }
        };
        var stageList = new[]
        {
            new StageItem
            {
                StageRel = "形象（profile）/20240824 仪摄影写真/462A2628.jpg",
                FullPath = @"D:\stage\462A2628.jpg",
                ChannelKey = "profile",
                WorkIdGuess = "20240824 仪摄影写真",
                StageFolderGuess = "20240824 仪摄影写真"
            }
        };

        var found = UnregisteredWorkDiscovery.Discover(registeredList, stageList, profile: profile);
        Assert.DoesNotContain(found, work => work.Id == "20240824 仪摄影写真");
    }

    [Fact]
    public void Discover_ProfileFolder_IsNotUnregistered_WhenTitleDiffers()
    {
        var registeredList = new[]
        {
            new WorkCatalogItem
            {
                Id = "仪摄影写真",
                Channel = "profile",
                Title = "仪摄影写真"
            }
        };
        var stageList = new[]
        {
            new StageItem
            {
                StageRel = "形象照（profile）/20250901 杭州写真/01.jpg",
                FullPath = @"D:\stage\01.jpg",
                ChannelKey = "profile",
                WorkIdGuess = "20250901 杭州写真",
                StageFolderGuess = "20250901 杭州写真"
            }
        };

        var found = UnregisteredWorkDiscovery.Discover(registeredList, stageList);
        Assert.DoesNotContain(found, work => work.Channel == "profile");
        var merged = ProfileStageSessions.Merge(registeredList, stageList);
        Assert.Contains(merged, work => work.Id == "仪摄影写真" && !work.IsUnregistered);
        Assert.Contains(merged, work => work.Id == "杭州写真" && !work.IsUnregistered && work.StageFolder == "profile/20250901 杭州写真");
    }

    [Fact]
    public void Discover_ListsNewDatedFolder_WhenTitleDoesNotMatchExistingShoot()
    {
        var registeredList = new[]
        {
            new WorkCatalogItem
            {
                Id = "鹤壁 星空",
                Channel = "landscape-photo",
                Title = "鹤壁 星空"
            }
        };
        var stageList = new[]
        {
            new StageItem
            {
                StageRel = "风光摄影（landscape-photo）/20250901 杭州 夜景/01.jpg",
                FullPath = @"D:\stage\01.jpg",
                ChannelKey = "landscape-photo",
                WorkIdGuess = "20250901 杭州 夜景",
                StageFolderGuess = "20250901 杭州 夜景"
            }
        };

        var found = UnregisteredWorkDiscovery.Discover(registeredList, stageList);
        var leftover = Assert.Single(found, work => work.Id == "20250901 杭州 夜景");
        Assert.Equal("杭州 夜景", leftover.Title);
        Assert.True(leftover.IsUnregistered);
    }

    [Fact]
    public void WatchStageEnabled_MissingSetting_DefaultsOn()
    {
        Assert.True(new AppSettings().WatchStageEnabled);
        Assert.True(new AppSettings { WatchStageChanges = null }.WatchStageEnabled);
        Assert.False(new AppSettings { WatchStageChanges = false }.WatchStageEnabled);
        Assert.Equal("direct", new AppSettings().ExecutionMode);
        Assert.Null(new AppSettings().LastWorkId);
        Assert.False(new AppSettings().LastUnregisteredGroup);
    }

    /// <summary>
    /// 加载模拟站正本。
    /// </summary>
    private static WorkspaceSession LoadFixture()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        return WorkspaceSession.Load(profilePath!);
    }
}
