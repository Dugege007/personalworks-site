using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 多选摄影项目时网格应覆盖这些项目。
/// </summary>
public sealed class NavGridScopeTests
{
    [Fact]
    public void PhotoProjectsForGrid_UnionsTwoRegisteredPhotoWorks()
    {
        var selected = new NavGridScope.NavWorkPick[]
        {
            new("real-world-photo", "甲", true),
            new("real-world-photo", "乙", true)
        };
        var list = NavGridScope.PhotoProjectsForGrid(selected);
        Assert.Equal(new[] { "甲", "乙" }, list.Select(item => item.WorkId));
    }

    [Fact]
    public void PhotoProjectsForGrid_SingleOrNonPhoto_StaysEmpty()
    {
        var one = NavGridScope.PhotoProjectsForGrid(new[]
        {
            new NavGridScope.NavWorkPick("real-world-photo", "甲", true)
        });
        Assert.Empty(one);

        var mixed = NavGridScope.PhotoProjectsForGrid(new[]
        {
            new NavGridScope.NavWorkPick("real-world-photo", "甲", true),
            new NavGridScope.NavWorkPick("landscape-rendering", "park", true),
            new NavGridScope.NavWorkPick("real-world-photo", "乙", false)
        });
        Assert.Empty(mixed);
    }

    [Fact]
    public void YearsForGrid_UnionsTwoPhotoYears()
    {
        var list = NavGridScope.YearsForGrid(new[]
        {
            new NavGridScope.YearRef("real-world-photo", "2019"),
            new NavGridScope.YearRef("real-world-photo", "2025")
        });
        Assert.Equal(new[] { "2019", "2025" }, list.Select(item => item.Year));

        var one = NavGridScope.YearsForGrid(new[]
        {
            new NavGridScope.YearRef("real-world-photo", "2019")
        });
        Assert.Empty(one);
    }
}
