using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class SiteCardCaptionTests
{
    [Fact]
    public void Name_UsesDisplayName_WhenFilled()
    {
        var site = CreateSite(
            displayName: "公园入口",
            label: "P1370776",
            sourceStageRel: "风光摄影（landscape-photo）/20231115 南昌 滕王阁/P1370776.jpg",
            objectKey: "landscape-photo/nanchang-tengwang-pavilion/01.webp");

        Assert.Equal("公园入口", SiteCardCaption.Name(site));
        Assert.Equal("(1/3) 公园入口", SiteCardCaption.Format(site));
    }

    [Fact]
    public void Name_UsesSourceStem_WithoutExtension()
    {
        var site = CreateSite(
            displayName: null,
            label: "P1370776",
            sourceStageRel: "风光摄影（landscape-photo）/20231115 南昌 滕王阁/P1370776.jpg",
            objectKey: "landscape-photo/nanchang-tengwang-pavilion/01.webp");

        Assert.Equal("P1370776", SiteCardCaption.Name(site));
        Assert.Equal("(1/3) P1370776", SiteCardCaption.Format(site));
    }

    [Fact]
    public void Name_KeepsLongSourceStem_WithoutExtension()
    {
        var site = CreateSite(
            displayName: "  ",
            label: "DSC09551-已增强-降噪",
            sourceStageRel: "风光摄影（landscape-photo）/20251008 重庆 渝中区/DSC09551-已增强-降噪.jpg",
            objectKey: "landscape-photo/chongqing-yuzhong/01.webp",
            index: 0,
            total: 5);

        Assert.Equal("DSC09551-已增强-降噪", SiteCardCaption.Name(site));
        Assert.Equal("(1/5) DSC09551-已增强-降噪", SiteCardCaption.Format(site));
    }

    [Fact]
    public void Name_IgnoresPreparedStageRel_FallsBackToLabel()
    {
        var site = CreateSite(
            displayName: null,
            label: "DSC04068",
            sourceStageRel: null,
            objectKey: "landscape-photo/shanghai-jingan-temple/01.webp",
            stageRel: "风光摄影（landscape-photo）/20250413 上海 静安寺/.site-ready/shanghai-jingan-temple/01.webp");

        Assert.Equal("DSC04068", SiteCardCaption.Name(site));
    }

    [Fact]
    public void Name_UsesStageStem_WhenSourceMissingAndStageIsOriginal()
    {
        var site = CreateSite(
            displayName: null,
            label: "效果图 02",
            sourceStageRel: null,
            objectKey: "demo-render/demo-park/02.png",
            stageRel: "demo-render/demo-park/02.png");

        Assert.Equal("02", SiteCardCaption.Name(site));
    }

    [Fact]
    public void Name_TreatsPlaceholderDisplayNameAsEmpty()
    {
        var site = CreateSite(
            displayName: CopyText.Placeholder,
            label: "P1370776",
            sourceStageRel: "风光摄影（landscape-photo）/20231115 南昌 滕王阁/P1370776.jpg",
            objectKey: "landscape-photo/nanchang-tengwang-pavilion/01.webp");

        Assert.Equal("P1370776", SiteCardCaption.Name(site));
    }

    [Fact]
    public void PersonalWorks_UnfilledPhotoUsesSourceStem()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var site = Assert.Single(
            session.SiteItems,
            item => item.ObjectKey == "landscape-photo/nanchang-tengwang-pavilion/01.webp");
        Assert.EndsWith(
            "P1370776.jpg",
            (site.SourceStageRel ?? "").Replace('\\', '/'),
            StringComparison.Ordinal);
        Assert.False(CopyText.IsFilled(site.DisplayName));
        Assert.Equal("P1370776", SiteCardCaption.Name(site));
    }

    [Fact]
    public void PersonalWorks_FilledMediaNameUsesDisplayName()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var site = Assert.Single(
            session.SiteItems,
            item => item.ObjectKey == "digital-twin/shanghai-mansheng-packaging/09.mp4");
        Assert.Equal("立库动画", SiteCardCaption.Name(site));
    }

    [Fact]
    public void SplitStickyExtension_DoesNotPinAnyExt_OnSiteCaption()
    {
        var caption = SiteCardCaption.Format(CreateSite(
            displayName: null,
            label: "DSC09551-已增强-降噪",
            sourceStageRel: "风光摄影（landscape-photo）/20251008 重庆 渝中区/DSC09551-已增强-降噪.jpg",
            objectKey: "landscape-photo/chongqing-yuzhong/01.webp"));
        MediaPathRules.SplitStickyExtension(caption, null, out var stem, out var extension);

        Assert.Equal("(1/3) DSC09551-已增强-降噪", stem);
        Assert.Equal("", extension);
    }

    /// <summary>
    /// 组装一条仅含题名所需字段的站点条目。
    /// </summary>
    private static SiteItem CreateSite(
        string? displayName,
        string label,
        string? sourceStageRel,
        string objectKey,
        string? stageRel = null,
        int index = 0,
        int total = 3)
    {
        return new SiteItem
        {
            WorkId = "nanchang-tengwang-pavilion",
            ChannelKey = "landscape-photo",
            WorkTitle = "南昌 滕王阁",
            Label = label,
            DisplayName = displayName,
            ObjectKey = objectKey,
            Index = index,
            Total = total,
            StageRel = stageRel,
            SourceStageRel = sourceStageRel
        };
    }
}
