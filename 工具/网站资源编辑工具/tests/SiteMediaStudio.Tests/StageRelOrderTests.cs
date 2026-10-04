using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class StageRelOrderTests
{
    [Theory]
    [InlineData("LP-1.01.jpg", "LP-2.01.jpg", -1)]
    [InlineData("LP-2.01.jpg", "LP-10.01.jpg", -1)]
    [InlineData("LP-10.01.jpg", "LP-2.01.jpg", 1)]
    [InlineData("01 系统图/LP-1.01.jpg", "02 详图/L4.1.jpg", -1)]
    [InlineData("a/.site-ready/01.webp", "a/.site-ready/01.webp", 0)]
    public void Compare_UsesNumericRuns(string left, string right, int expectedSign)
    {
        var result = StageRelOrder.Compare(left, right);
        if (expectedSign == 0)
        {
            Assert.Equal(0, result);
            return;
        }

        Assert.Equal(expectedSign, Math.Sign(result));
    }

    [Fact]
    public void PreferSource_SkipsPreparedRel()
    {
        Assert.Equal(
            "景观施工图（landscape-cds）/项目/LP-2.01.jpg",
            StageRelOrder.PreferSource(
                "景观施工图（landscape-cds）/项目/LP-2.01.jpg",
                "景观施工图（landscape-cds）/项目/.site-ready/id/03.webp",
                "landscape-cds/id/03.webp"));
        Assert.Equal(
            "landscape-cds/id/03.webp",
            StageRelOrder.PreferSource(
                "a/.site-ready/id/03.webp",
                "a/.site-ready/id/03.webp",
                "landscape-cds/id/03.webp"));
    }

    [Fact]
    public void PlanMedia_AppendsIngestAtEndWithoutStageSort()
    {
        var existing = new[]
        {
            new WorkMediaItem { Kind = "image", Src = "landscape-cds/work/01.webp", Label = "施工图 01" },
            new WorkMediaItem { Kind = "image", Src = "landscape-cds/work/02.webp", Label = "施工图 02" }
        };
        var sortKeyDict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["landscape-cds/work/01.webp"] = "cds/01 系统图/LP-1.01.jpg",
            ["landscape-cds/work/02.webp"] = "cds/01 系统图/LP-10.01.jpg",
            ["landscape-cds/work/03.webp"] = "cds/01 系统图/LP-2.01.jpg"
        };

        var planned = ContentPatchPlanner.PlanMedia(
            existing,
            null,
            new[] { "landscape-cds/work/03.webp" },
            sortKeyDict: sortKeyDict);

        Assert.Equal(
            new[]
            {
                "landscape-cds/work/01.webp",
                "landscape-cds/work/02.webp",
                "landscape-cds/work/03.webp"
            },
            planned.AfterList.Select(item => item.Src));
        Assert.Equal("施工图 02", planned.AfterList[1].Label);
        Assert.Equal("施工图 03", planned.AfterList[2].Label);
    }

    [Fact]
    public void PlanMedia_AppliesExplicitSiteOrder()
    {
        var existing = new[]
        {
            new WorkMediaItem { Kind = "image", Src = "landscape-cds/work/01.webp", Label = "施工图 01" },
            new WorkMediaItem { Kind = "image", Src = "landscape-cds/work/02.webp", Label = "施工图 02" },
            new WorkMediaItem { Kind = "image", Src = "landscape-cds/work/03.webp", Label = "施工图 03" }
        };

        var planned = ContentPatchPlanner.PlanMedia(
            existing,
            null,
            null,
            orderList: new[]
            {
                "landscape-cds/work/03.webp",
                "landscape-cds/work/01.webp",
                "landscape-cds/work/02.webp"
            });

        Assert.Equal(
            new[]
            {
                "landscape-cds/work/03.webp",
                "landscape-cds/work/01.webp",
                "landscape-cds/work/02.webp"
            },
            planned.AfterList.Select(item => item.Src));
        Assert.Equal("施工图 01", planned.AfterList[0].Label);
        Assert.Equal("施工图 02", planned.AfterList[1].Label);
        Assert.Equal("施工图 03", planned.AfterList[2].Label);
    }
}
