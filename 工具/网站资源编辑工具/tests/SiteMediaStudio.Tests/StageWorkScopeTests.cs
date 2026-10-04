using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class StageWorkScopeTests
{
    [Fact]
    public void BelongsToWork_ChannelOverview_IncludesUnpublished()
    {
        var unpublished = CreateUnpublished();
        Assert.True(StageWorkScope.BelongsToWork(BuildEmptySession(), unpublished, null));
    }

    [Fact]
    public void BelongsToWork_FolderNameDiffersFromId_UsesStageFolder()
    {
        var unpublished = CreateUnpublished();
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "ningbo-siweier",
                    Channel = "digital-twin",
                    Title = "宁波四维尔",
                    StageFolder = "digital-twin/20240530_宁波四维尔"
                }
            ],
            StageItems = [unpublished]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "ningbo-siweier"));
        Assert.False(StageWorkScope.BelongsToWork(session, unpublished, "other-work"));
    }

    [Fact]
    public void BelongsToWork_PairedSibling_IncludesUnpublishedInSameFolder()
    {
        var unpublished = CreateUnpublished();
        var paired = new StageItem
        {
            StageRel = "数字孪生（digital-twin）/20240530_宁波四维尔/01.png",
            FullPath = "x",
            ChannelKey = "digital-twin",
            WorkIdGuess = "20240530_宁波四维尔",
            StageFolderGuess = "20240530_宁波四维尔",
            MatchedObject = "digital-twin/ningbo-siweier/01.webp"
        };
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "ningbo-siweier",
                    Channel = "digital-twin",
                    Title = "宁波四维尔"
                }
            ],
            StageItems = [unpublished, paired],
            SiteItems =
            [
                new SiteItem
                {
                    WorkId = "ningbo-siweier",
                    ChannelKey = "digital-twin",
                    WorkTitle = "宁波四维尔",
                    Label = "01",
                    ObjectKey = "digital-twin/ningbo-siweier/01.webp"
                }
            ]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "ningbo-siweier"));
    }

    [Fact]
    public void BelongsToWork_SiteStageRel_IncludesUnpublishedInClaimedFolder()
    {
        var unpublished = CreateUnpublished();
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "ningbo-siweier",
                    Channel = "digital-twin",
                    Title = "宁波四维尔"
                }
            ],
            StageItems = [unpublished],
            SiteItems =
            [
                new SiteItem
                {
                    WorkId = "ningbo-siweier",
                    ChannelKey = "digital-twin",
                    WorkTitle = "宁波四维尔",
                    Label = "01",
                    ObjectKey = "digital-twin/ningbo-siweier/01.webp",
                    StageRel = "数字孪生（digital-twin）/20240530_宁波四维尔/.site-ready/ningbo-siweier/01.webp"
                }
            ]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "ningbo-siweier"));
    }

    [Fact]
    public void Compare_WorkScope_KeepsUnpublishedWhenFolderDiffersFromId()
    {
        var unpublished = CreateUnpublished();
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "ningbo-siweier",
                    Channel = "digital-twin",
                    Title = "宁波四维尔",
                    StageFolder = "digital-twin/20240530_宁波四维尔"
                }
            ],
            StageItems = [unpublished]
        };

        var rows = ComparePairing.Build(session, "digital-twin", "ningbo-siweier", Array.Empty<ManualPin>());
        Assert.Contains(rows, row =>
            row.Kind == CompareRowKind.StageOnly
            && row.Stage != null
            && row.Stage.StageRel == unpublished.StageRel);
    }

    [Fact]
    public void BelongsToWork_SameTitleDifferentDate_DoesNotIncludeOtherBatch()
    {
        var sept = new StageItem
        {
            StageRel = "风光摄影（landscape-photo）/20220912 鹤壁 星空/20220912-106.jpg",
            FullPath = "x",
            ChannelKey = "landscape-photo",
            WorkIdGuess = "20220912 鹤壁 星空",
            StageFolderGuess = "20220912 鹤壁 星空",
            MatchedObject = "landscape-photo/hebi-stars-20220912/01.webp"
        };
        var oct = new StageItem
        {
            StageRel = "风光摄影（landscape-photo）/20241002 鹤壁 星空/DSC00768.jpg",
            FullPath = "y",
            ChannelKey = "landscape-photo",
            WorkIdGuess = "20241002 鹤壁 星空",
            StageFolderGuess = "20241002 鹤壁 星空",
            MatchedObject = "landscape-photo/hebi-stars-20241002/01.webp"
        };
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
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
            ],
            StageItems = [sept, oct],
            SiteItems =
            [
                new SiteItem
                {
                    WorkId = "hebi-stars-20220912",
                    ChannelKey = "landscape-photo",
                    WorkTitle = "鹤壁 星空",
                    Label = "01",
                    ObjectKey = "landscape-photo/hebi-stars-20220912/01.webp",
                    StageRel = "风光摄影（landscape-photo）/20220912 鹤壁 星空/.site-ready/hebi-stars-20220912/01.webp"
                },
                new SiteItem
                {
                    WorkId = "hebi-stars-20241002",
                    ChannelKey = "landscape-photo",
                    WorkTitle = "鹤壁 星空",
                    Label = "01",
                    ObjectKey = "landscape-photo/hebi-stars-20241002/01.webp",
                    StageRel = "风光摄影（landscape-photo）/20241002 鹤壁 星空/.site-ready/hebi-stars-20241002/01.webp"
                }
            ]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, sept, "hebi-stars-20220912"));
        Assert.False(StageWorkScope.BelongsToWork(session, oct, "hebi-stars-20220912"));
        Assert.True(StageWorkScope.BelongsToWork(session, oct, "hebi-stars-20241002"));
        Assert.False(StageWorkScope.BelongsToWork(session, sept, "hebi-stars-20241002"));

        var septRows = ComparePairing.Build(session, "landscape-photo", "hebi-stars-20220912", []);
        Assert.DoesNotContain(septRows, row => row.Stage?.StageRel == oct.StageRel);
        Assert.Contains(septRows, row =>
            row.Kind == CompareRowKind.Paired && row.Stage?.StageRel == sept.StageRel);
    }

    [Fact]
    public void BelongsToWork_SharedParentDifferentBatch_DoesNotCrossClaim()
    {
        var batchFifteen = CreateFukangItem(
            "20200324-HAFKC-15#-DU",
            "a.jpg",
            "landscape-rendering/huaian-fukang-15/01.jpg");
        var batchThree = CreateFukangItem(
            "20200806_HAFK-3#_DU",
            "b.jpg",
            "landscape-rendering/huaian-fukang-3/01.jpg");
        var unpublishedFifteen = CreateFukangItem("20200324-HAFKC-15#-DU", "spare.jpg", null);
        var session = new WorkspaceSession
        {
            Profile = FukangProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "huaian-fukang-15",
                    Channel = "landscape-rendering",
                    Title = "淮安富康城15#"
                },
                new WorkCatalogItem
                {
                    Id = "huaian-fukang-3",
                    Channel = "landscape-rendering",
                    Title = "淮安富康城3#"
                }
            ],
            StageItems = [batchFifteen, batchThree, unpublishedFifteen],
            SiteItems =
            [
                new SiteItem
                {
                    WorkId = "huaian-fukang-15",
                    ChannelKey = "landscape-rendering",
                    WorkTitle = "淮安富康城15#",
                    Label = "01",
                    ObjectKey = "landscape-rendering/huaian-fukang-15/01.jpg",
                    StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/201902 淮安富康城/20200324-HAFKC-15#-DU/.site-ready/huaian-fukang-15/01.webp"
                },
                new SiteItem
                {
                    WorkId = "huaian-fukang-3",
                    ChannelKey = "landscape-rendering",
                    WorkTitle = "淮安富康城3#",
                    Label = "01",
                    ObjectKey = "landscape-rendering/huaian-fukang-3/01.jpg",
                    StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/201902 淮安富康城/20200806_HAFK-3#_DU/.site-ready/huaian-fukang-3/01.webp"
                }
            ]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, batchFifteen, "huaian-fukang-15"));
        Assert.False(StageWorkScope.BelongsToWork(session, batchThree, "huaian-fukang-15"));
        Assert.True(StageWorkScope.BelongsToWork(session, unpublishedFifteen, "huaian-fukang-15"));
        Assert.False(StageWorkScope.BelongsToWork(session, unpublishedFifteen, "huaian-fukang-3"));
        Assert.True(StageWorkScope.BelongsToWork(session, batchThree, "huaian-fukang-3"));
    }

    [Fact]
    public void BelongsToWork_OtherFolder_RemainsExcluded()
    {
        var leftover = new StageItem
        {
            StageRel = "数字孪生（digital-twin）/leftover/unused.png",
            FullPath = "x",
            ChannelKey = "digital-twin",
            WorkIdGuess = "leftover",
            StageFolderGuess = "leftover"
        };
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "ningbo-siweier",
                    Channel = "digital-twin",
                    Title = "宁波四维尔",
                    StageFolder = "digital-twin/20240530_宁波四维尔"
                }
            ],
            StageItems = [leftover]
        };

        Assert.False(StageWorkScope.BelongsToWork(session, leftover, "ningbo-siweier"));
        var candidates = StageWorkScope.CandidatesForWork(session, "digital-twin", "ningbo-siweier").ToList();
        Assert.DoesNotContain(candidates, item => item.WorkIdGuess == "leftover");
    }

    [Fact]
    public void CandidatesForWork_KeepsTitleMatchedFolder_SkipsNeighbor()
    {
        var unpublished = new StageItem
        {
            StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑/备选.jpg",
            FullPath = "x",
            ChannelKey = "landscape-rendering",
            WorkIdGuess = "201804 北京丰台小瓦窑",
            StageFolderGuess = "上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑"
        };
        var leftover = LeftoverForTitleTest();
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "xiaowayao",
                    Channel = "landscape-rendering",
                    Title = "北京丰台小瓦窑"
                }
            ],
            StageItems = [unpublished, leftover]
        };

        var candidates = StageWorkScope.CandidatesForWork(session, "landscape-rendering", "xiaowayao").ToList();
        Assert.Contains(candidates, item => item.WorkIdGuess == "201804 北京丰台小瓦窑");
        Assert.DoesNotContain(candidates, item => item.WorkIdGuess == leftover.WorkIdGuess);
        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "xiaowayao"));
    }

    [Fact]
    public void BelongsToWork_BatchSubfolderStageRel_IncludesUnpublishedInWorkFolder()
    {
        var unpublished = new StageItem
        {
            StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/202008 哈尔滨江御府/20200813_HEBZSQ_DU/备选.jpg",
            FullPath = "x",
            ChannelKey = "landscape-rendering",
            WorkIdGuess = "202008 哈尔滨江御府",
            StageFolderGuess = "上海道田景观工程咨询有限公司/202008 哈尔滨江御府"
        };
        var session = new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                Name = "测试",
                ResolvedRoot = ".",
                ProfilePath = "profile.json",
                Channels =
                [
                    new ChannelProfile
                    {
                        Key = "landscape-rendering",
                        StageContainerFolders = ["上海道田景观工程咨询有限公司"]
                    }
                ]
            },
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "harbin-jiangyufu",
                    Channel = "landscape-rendering",
                    Title = "哈尔滨江御府"
                }
            ],
            StageItems = [unpublished],
            SiteItems =
            [
                new SiteItem
                {
                    WorkId = "harbin-jiangyufu",
                    ChannelKey = "landscape-rendering",
                    WorkTitle = "哈尔滨江御府",
                    Label = "07",
                    ObjectKey = "landscape-rendering/harbin-jiangyufu/07.webp",
                    StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/202008 哈尔滨江御府/20200813_HEBZSQ_DU/.site-ready/harbin-jiangyufu/07.webp"
                }
            ]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "harbin-jiangyufu"));
    }

    [Fact]
    public void BelongsToWork_ChannelRootUnpublished_BelongsToFlatFileWork()
    {
        var unpublished = new StageItem
        {
            StageRel = "形象（profile）/462A2628.jpg",
            FullPath = "x",
            ChannelKey = "profile"
        };
        var session = new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                Name = "测试",
                ResolvedRoot = ".",
                ProfilePath = "profile.json",
                Channels =
                [
                    new ChannelProfile
                    {
                        Key = "profile",
                        ObjectKeyPattern = "flat-file"
                    }
                ]
            },
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "portrait",
                    Channel = "profile",
                    Title = "形象照",
                    SourceKind = "site-profile"
                }
            ],
            StageItems = [unpublished]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "portrait"));
        Assert.False(StageWorkScope.BelongsToWork(session, unpublished, "other-work"));
    }

    [Fact]
    public void BelongsToWork_TitleWithoutDate_IncludesUnpublished()
    {
        var unpublished = new StageItem
        {
            StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑/备选.jpg",
            FullPath = "x",
            ChannelKey = "landscape-rendering",
            WorkIdGuess = "201804 北京丰台小瓦窑",
            StageFolderGuess = "上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑"
        };
        var session = new WorkspaceSession
        {
            Profile = EmptyProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "xiaowayao",
                    Channel = "landscape-rendering",
                    Title = "北京丰台小瓦窑"
                }
            ],
            StageItems = [unpublished]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "xiaowayao"));
        Assert.False(StageWorkScope.BelongsToWork(session, LeftoverForTitleTest(), "xiaowayao"));
    }

    [Fact]
    public void BelongsToWork_ProfileDatedFolder_BelongsToPortrait()
    {
        var unpublished = new StageItem
        {
            StageRel = "形象（profile）/20240824 仪摄影写真/462A2628.jpg",
            FullPath = "x",
            ChannelKey = "profile",
            WorkIdGuess = "20240824 仪摄影写真",
            StageFolderGuess = "20240824 仪摄影写真"
        };
        var session = new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                Name = "测试",
                ResolvedRoot = ".",
                ProfilePath = "profile.json",
                Channels =
                [
                    new ChannelProfile
                    {
                        Key = "profile",
                        ObjectKeyPattern = "flat-file"
                    }
                ]
            },
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "仪摄影写真",
                    Channel = "profile",
                    Title = "仪摄影写真",
                    SourceKind = "site-profile"
                }
            ],
            StageItems = [unpublished]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, unpublished, "仪摄影写真"));
    }

    [Fact]
    public void BelongsToWork_ProfileSessions_DoNotShareNeighborFolder()
    {
        var yi = new StageItem
        {
            StageRel = "形象照（profile）/20240824 仪摄影写真/462A2628.jpg",
            FullPath = "x",
            ChannelKey = "profile",
            WorkIdGuess = "20240824 仪摄影写真",
            StageFolderGuess = "20240824 仪摄影写真",
            MatchedObject = ""
        };
        var xuhui = new StageItem
        {
            StageRel = "形象照（profile）/20260905 徐汇区/01.jpg",
            FullPath = "y",
            ChannelKey = "profile",
            WorkIdGuess = "20260905 徐汇区",
            StageFolderGuess = "20260905 徐汇区"
        };
        var session = new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                Name = "测试",
                ResolvedRoot = ".",
                ProfilePath = "profile.json",
                Channels =
                [
                    new ChannelProfile
                    {
                        Key = "profile",
                        ObjectKeyPattern = "flat-file"
                    }
                ]
            },
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "仪摄影写真",
                    Channel = "profile",
                    Title = "仪摄影写真",
                    SourceKind = "site-profile",
                    StageFolder = "profile/20240824 仪摄影写真",
                    Media =
                    [
                        new WorkMediaItem { Kind = "image", Src = "profile/462A2628.webp" }
                    ]
                },
                new WorkCatalogItem
                {
                    Id = "徐汇区",
                    Channel = "profile",
                    Title = "徐汇区",
                    SourceKind = "site-profile",
                    StageFolder = "profile/20260905 徐汇区"
                }
            ],
            StageItems = [yi, xuhui],
            LedgerDict = new Dictionary<string, LedgerRecord>
            {
                ["profile/462A2628.webp"] = new LedgerRecord
                {
                    Object = "profile/462A2628.webp",
                    StageRel = "形象照（profile）/20240824 仪摄影写真/462A2628.jpg",
                    Status = "published"
                }
            },
            SiteItems =
            [
                new SiteItem
                {
                    WorkId = "仪摄影写真",
                    ChannelKey = "profile",
                    WorkTitle = "仪摄影写真",
                    Label = "462A2628",
                    ObjectKey = "profile/462A2628.webp"
                }
            ]
        };

        Assert.True(StageWorkScope.BelongsToWork(session, yi, "仪摄影写真"));
        Assert.False(StageWorkScope.BelongsToWork(session, yi, "徐汇区"));
        Assert.True(StageWorkScope.BelongsToWork(session, xuhui, "徐汇区"));
        Assert.False(StageWorkScope.BelongsToWork(session, xuhui, "仪摄影写真"));
    }

    /// <summary>
    /// 与题名无关的对照夹。
    /// </summary>
    private static StageItem LeftoverForTitleTest()
    {
        return new StageItem
        {
            StageRel = "景观效果图（landscape-rendering）/leftover/unused.png",
            FullPath = "x",
            ChannelKey = "landscape-rendering",
            WorkIdGuess = "leftover",
            StageFolderGuess = "leftover"
        };
    }

    /// <summary>
    /// 组装无台账的未上页条目。
    /// </summary>
    private static StageItem CreateUnpublished()
    {
        return new StageItem
        {
            StageRel = "数字孪生（digital-twin）/20240530_宁波四维尔/微信截图.png",
            FullPath = "x",
            ChannelKey = "digital-twin",
            WorkIdGuess = "20240530_宁波四维尔",
            StageFolderGuess = "20240530_宁波四维尔"
        };
    }

    /// <summary>
    /// 组装空编目会话。
    /// </summary>
    private static WorkspaceSession BuildEmptySession()
    {
        return new WorkspaceSession
        {
            Profile = EmptyProfile(),
            StageItems = [CreateUnpublished()]
        };
    }

    /// <summary>
    /// 淮安富康城共用作品夹下的一条投放箱。
    /// </summary>
    private static StageItem CreateFukangItem(string batchFolder, string fileName, string? matchedObject)
    {
        return new StageItem
        {
            StageRel = "景观效果图（landscape-rendering）/上海道田景观工程咨询有限公司/201902 淮安富康城/"
                + batchFolder + "/" + fileName,
            FullPath = "x",
            ChannelKey = "landscape-rendering",
            WorkIdGuess = "201902 淮安富康城",
            StageFolderGuess = "上海道田景观工程咨询有限公司/201902 淮安富康城",
            MatchedObject = matchedObject
        };
    }

    /// <summary>
    /// 带施工图容器夹的配置。
    /// </summary>
    private static WorkspaceProfile FukangProfile()
    {
        return new WorkspaceProfile
        {
            Name = "测试",
            ResolvedRoot = ".",
            ProfilePath = "profile.json",
            Channels =
            [
                new ChannelProfile
                {
                    Key = "landscape-rendering",
                    StageContainerFolders = ["上海道田景观工程咨询有限公司"]
                }
            ]
        };
    }

    /// <summary>
    /// 最小工作区配置。
    /// </summary>
    private static WorkspaceProfile EmptyProfile()
    {
        return new WorkspaceProfile
        {
            Name = "测试",
            ResolvedRoot = ".",
            ProfilePath = "profile.json"
        };
    }
}
