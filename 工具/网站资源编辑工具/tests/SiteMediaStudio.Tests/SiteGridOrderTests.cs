using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 站点卡片按投放箱路径序，而不是内容层数组序。
/// </summary>
public sealed class SiteGridOrderTests
{
    [Fact]
    public void SortLikeStage_FollowsSourcePathNotContentOrder()
    {
        var later = Item(
            "photo/real-world-photo/20260926 南京 牛首山/01.webp",
            "摄影（photo）/现实摄影（real-world-photo）/2026/20260926 南京 牛首山/DSC07841.jpg");
        var earlierSecond = Item(
            "photo/real-world-photo/20180323 武汉/02.webp",
            "摄影（photo）/现实摄影（real-world-photo）/2018/20180323 武汉/P1210046.jpg");
        var earlierFirst = Item(
            "photo/real-world-photo/20180323 武汉/01.webp",
            "摄影（photo）/现实摄影（real-world-photo）/2018/20180323 武汉/P1210021.jpg");

        var ordered = SiteGridOrder.SortLikeStage(new[] { later, earlierSecond, earlierFirst });

        Assert.Equal(
            new[] { earlierFirst.ObjectKey, earlierSecond.ObjectKey, later.ObjectKey },
            ordered.Select(item => item.ObjectKey));
    }

    /// <summary>
    /// 组装一条只带排序所需字段的站点卡片。
    /// </summary>
    private static SiteItem Item(string objectKey, string sourceStageRel)
    {
        return new SiteItem
        {
            WorkId = "work",
            ChannelKey = "real-world-photo",
            WorkTitle = "作品",
            Label = objectKey,
            ObjectKey = objectKey,
            SourceStageRel = sourceStageRel
        };
    }
}
