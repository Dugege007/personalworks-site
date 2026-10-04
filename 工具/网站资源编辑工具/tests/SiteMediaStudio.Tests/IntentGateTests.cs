using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class IntentGateTests
{
    [Fact]
    public void Recycle_PublishedStageFile_IsRejected()
    {
        var temp = CreateTempPng();
        var item = new StageItem
        {
            StageRel = "demo-render/demo-park/01.png",
            FullPath = temp,
            ChannelKey = "demo-render",
            MatchedObject = "demo-render/demo-park/01.png",
            LedgerStatus = "published",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(MediaIntent.StageRecycle, item, PublishedContext());
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Recycle_UnpublishedStageFile_IsAllowed()
    {
        var temp = CreateTempPng();
        var item = new StageItem
        {
            StageRel = "demo-render/demo-park/03.png",
            FullPath = temp,
            ChannelKey = "demo-render",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageRecycle,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int>()
            });
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Recycle_Stock_IsRejected()
    {
        var temp = CreateTempPng();
        var item = new StageItem
        {
            StageRel = "demo-render/stock/keep.png",
            FullPath = temp,
            ChannelKey = "demo-render",
            IsStock = true
        };
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageRecycle,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int>()
            });
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Withdraw_WhenOtherWorkStillReferences_IsRejected()
    {
        var item = new SiteItem
        {
            WorkId = "a",
            ChannelKey = "demo-render",
            WorkTitle = "A",
            Label = "效果图 01",
            ObjectKey = "demo-render/shared/01.png",
            ReferenceCount = 2
        };
        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteWithdraw,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int> { ["demo-render/shared/01.png"] = 2 },
                BatchRemoveCountDict = new Dictionary<string, int> { ["demo-render/shared/01.png"] = 1 }
            });
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Hide_WithObjectKey_IsAllowed()
    {
        var item = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "演示公园",
            Label = "效果图 01",
            ObjectKey = "demo-render/demo-park/01.png"
        };
        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteHide,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int> { ["demo-render/demo-park/01.png"] = 1 }
            });
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Hide_AlreadyHidden_IsSkippedNotRejected()
    {
        var item = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "演示公园",
            Label = "效果图 02",
            ObjectKey = "demo-render/demo-park/02.png",
            IsHidden = true
        };
        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteHide,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int>()
            });
        Assert.True(decision.Allowed);
        Assert.True(decision.IsWarn);
        Assert.False(decision.IsHardError);
        Assert.True(IntentGate.IsRedundantHideDecision(decision));
        Assert.Contains("已隐藏", decision.Message, StringComparison.Ordinal);
        Assert.Contains("跳过", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_Hidden_IsAllowedWithoutNewKey()
    {
        var item = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "演示公园",
            Label = "效果图 02",
            ObjectKey = "demo-render/demo-park/02.png",
            IsHidden = true
        };
        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteRestore,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int>()
            });
        Assert.True(decision.Allowed);
        Assert.False(decision.IsHardError);
        Assert.Contains("不新建键", decision.Message, StringComparison.Ordinal);
        Assert.Contains("不重新入库", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ingest_AlreadyPublished_IsSkippedNotRejected()
    {
        var temp = CreateTempPng();
        var item = new StageItem
        {
            StageRel = "demo-render/demo-park/01.png",
            FullPath = temp,
            ChannelKey = "demo-render",
            MatchedObject = "demo-render/demo-park/01.png",
            LedgerStatus = "published",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(MediaIntent.StageIngest, item, PublishedContext());
        Assert.True(decision.Allowed);
        Assert.True(decision.IsWarn);
        Assert.False(decision.IsHardError);
        Assert.True(IntentGate.IsRedundantIngestDecision(decision));
        Assert.Contains("已上页", decision.Message, StringComparison.Ordinal);
        Assert.Contains("跳过", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ingest_WithdrawnSourcePath_AllowsNewTarget()
    {
        var item = new StageItem
        {
            StageRel = "风光摄影（landscape-photo）/20250413 上海 静安寺/01.jpg",
            FullPath = CreateTempPng(),
            ChannelKey = "landscape-photo",
            MatchedObject = "landscape-photo/old-id/01.webp",
            LedgerStatus = "withdrawn",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageIngest,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>
                {
                    ["landscape-photo/old-id/01.webp"] = new LedgerRecord
                    {
                        Object = "landscape-photo/old-id/01.webp",
                        StageRel = "风光摄影（landscape-photo）/20250413 上海 静安寺/.site-ready/old-id/01.webp",
                        SourceStageRel = "风光摄影（landscape-photo）/20250413 上海 静安寺/01.jpg",
                        Status = "withdrawn"
                    }
                },
                ContentRefCountDict = new Dictionary<string, int>(),
                TargetWorkId = "20250413 上海 静安寺"
            });
        Assert.True(decision.Allowed);
        Assert.False(decision.IsHardError);
    }

    [Fact]
    public void Ingest_PublishedSourcePath_RejectsOtherObject()
    {
        var item = new StageItem
        {
            StageRel = "风光摄影（landscape-photo）/20250413 上海 静安寺/01.jpg",
            FullPath = CreateTempPng(),
            ChannelKey = "landscape-photo",
            IsStock = false
        };
        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageIngest,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>
                {
                    ["landscape-photo/old-id/01.webp"] = new LedgerRecord
                    {
                        Object = "landscape-photo/old-id/01.webp",
                        StageRel = "风光摄影（landscape-photo）/20250413 上海 静安寺/.site-ready/old-id/01.webp",
                        SourceStageRel = "风光摄影（landscape-photo）/20250413 上海 静安寺/01.jpg",
                        Status = "published"
                    }
                },
                ContentRefCountDict = new Dictionary<string, int>(),
                TargetWorkId = "20250413 上海 静安寺"
            });
        Assert.False(decision.Allowed);
        Assert.Contains("published", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_AlreadyOnPage_IsSkippedNotRejected()
    {
        var item = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "演示公园",
            Label = "效果图 01",
            ObjectKey = "demo-render/demo-park/01.png",
            IsHidden = false
        };
        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteRestore,
            item,
            new IntentContext
            {
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int> { ["demo-render/demo-park/01.png"] = 1 }
            });
        Assert.True(decision.Allowed);
        Assert.True(decision.IsWarn);
        Assert.True(IntentGate.IsRedundantRestoreDecision(decision));
        Assert.Contains("已在页上", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileHide_WithRemainingImage_IsAllowed()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var item = session.SiteItems.First(site => site.ObjectKey == "profile/portrait.webp");

        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteHide,
            item,
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                WorkList = session.Works
            });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void ProfileWithdraw_CountsPrimaryAndGalleryReferencesAsOnePatch()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var item = session.SiteItems.First(site => site.ObjectKey == "profile/portrait.webp");

        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteWithdraw,
            item,
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                WorkList = session.Works,
                BatchRemoveCountDict = new Dictionary<string, int>
                {
                    [item.ObjectKey] = item.ReferenceCount
                }
            });

        Assert.True(decision.Allowed);
        Assert.Equal(2, item.ReferenceCount);
    }

    [Fact]
    public void GameSingleCoverHide_IsRejectedBeforeDanglingReference()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var item = session.SiteItems.First(site =>
            site.ChannelKey == "game-dev" && site.WorkId == "line");

        var decision = IntentGate.EvaluateSite(
            MediaIntent.SiteWithdraw,
            item,
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                WorkList = session.Works,
                BatchRemoveCountDict = new Dictionary<string, int> { [item.ObjectKey] = 1 }
            });

        Assert.False(decision.Allowed);
        Assert.Contains("断引用", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PersonalWorksIngest_OriginalFormat_AllowsPrepareThenIngest()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var item = new StageItem
        {
            StageRel = "风光摄影（landscape-photo）/原图.jpg",
            FullPath = CreateTempPng(),
            ChannelKey = "landscape-photo",
            IsStock = false
        };

        var decision = IntentGate.EvaluateStage(
            MediaIntent.StageIngest,
            item,
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = new Dictionary<string, LedgerRecord>(),
                ContentRefCountDict = new Dictionary<string, int>(),
                WorkList = Array.Empty<WorkCatalogItem>(),
                TargetWorkId = "unit-test"
            });

        Assert.True(decision.Allowed);
        Assert.Contains("压成网页 WebP", decision.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 写入最小 PNG 供存在性检查。
    /// </summary>
    private static string CreateTempPng()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, MinimalPng);
        return path;
    }

    /// <summary>
    /// 已上页上下文。
    /// </summary>
    private static IntentContext PublishedContext()
    {
        return new IntentContext
        {
            LedgerDict = new Dictionary<string, LedgerRecord>
            {
                ["demo-render/demo-park/01.png"] = new LedgerRecord
                {
                    Object = "demo-render/demo-park/01.png",
                    StageRel = "demo-render/demo-park/01.png",
                    Status = "published"
                }
            },
            ContentRefCountDict = new Dictionary<string, int> { ["demo-render/demo-park/01.png"] = 1 }
        };
    }

    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
        0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0xD4, 0xEF, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };
}
