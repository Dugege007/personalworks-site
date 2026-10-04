using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ComparePairingTests
{
    [Fact]
    public void AutoPair_UsesObjectKey_NotFolderName()
    {
        var session = BuildSession(
            new StageItem
            {
                StageRel = "landscape-rendering/huaian-fukang/01.jpg",
                FullPath = "x",
                ChannelKey = "landscape-rendering",
                WorkIdGuess = "huaian-fukang",
                MatchedObject = "landscape-rendering/huaian-fukang/01.jpg"
            },
            new SiteItem
            {
                WorkId = "huaian-fukang-15",
                ChannelKey = "landscape-rendering",
                WorkTitle = "淮安富康城15#",
                Label = "效果图 01",
                ObjectKey = "landscape-rendering/huaian-fukang/01.jpg",
                StageRel = "landscape-rendering/huaian-fukang/01.jpg"
            });

        var rows = ComparePairing.Build(session, "landscape-rendering", "huaian-fukang-15", Array.Empty<ManualPin>());
        Assert.Single(rows);
        Assert.Equal(CompareRowKind.Paired, rows[0].Kind);
        Assert.Equal("huaian-fukang-15", rows[0].Site!.WorkId);
        Assert.Equal("landscape-rendering/huaian-fukang/01.jpg", rows[0].Stage!.StageRel);
    }

    [Fact]
    public void ManualPin_WinsOverEmptyAutoMatch()
    {
        var stage = new StageItem
        {
            StageRel = "demo-render/leftover/unused.png",
            FullPath = "x",
            ChannelKey = "demo-render",
            WorkIdGuess = "leftover"
        };
        var site = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "演示公园",
            Label = "效果图 01",
            ObjectKey = "demo-render/demo-park/01.png"
        };
        var session = BuildSession(stage, site);
        var pins = new[]
        {
            new ManualPin
            {
                StageRel = stage.StageRel,
                Object = site.ObjectKey,
                WorkId = site.WorkId
            }
        };

        var rows = ComparePairing.Build(session, "demo-render", "demo-park", pins);
        Assert.Contains(rows, row => row.Kind == CompareRowKind.ManualPin && row.Stage == stage && row.Site == site);
    }

    [Fact]
    public void AutoPair_SameStemDifferentExtensionWithoutLedger_DoesNotPair()
    {
        var stage = new StageItem
        {
            StageRel = "形象（profile）/portrait.jpg",
            FullPath = "x",
            ChannelKey = "profile",
            MatchedObject = null
        };
        var site = new SiteItem
        {
            WorkId = "portrait",
            ChannelKey = "profile",
            WorkTitle = "形象照",
            Label = "portrait",
            ObjectKey = "profile/portrait.webp",
            ReferenceCount = 2
        };

        Assert.False(ComparePairing.IsAutoPair(stage, site));
        var rows = ComparePairing.Build(BuildSession(stage, site), "profile", null, Array.Empty<ManualPin>());
        Assert.Contains(rows, row => row.Kind == CompareRowKind.SiteOnly);
        Assert.Contains(rows, row => row.Kind == CompareRowKind.StageOnly);
    }

    /// <summary>
    /// 组装最小对照会话。
    /// </summary>
    private static WorkspaceSession BuildSession(StageItem stage, SiteItem site)
    {
        return new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                Name = "测试",
                ResolvedRoot = ".",
                ProfilePath = "profile.json"
            },
            StageItems = new[] { stage },
            SiteItems = new[] { site }
        };
    }
}
