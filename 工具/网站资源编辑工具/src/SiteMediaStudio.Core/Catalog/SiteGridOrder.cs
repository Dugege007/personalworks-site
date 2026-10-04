namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 站点网格与投放箱对齐时的路径序。不改内容层数组。
/// </summary>
public static class SiteGridOrder
{
    /// <summary>
    /// 按原片路径的逻辑序排列。没有原片路径时用对象键。同键保持原相对序。
    /// </summary>
    public static List<SiteItem> SortLikeStage(IReadOnlyList<SiteItem> siteList)
    {
        if (siteList == null || siteList.Count == 0)
        {
            return new List<SiteItem>();
        }

        return siteList
            .Select((item, index) => (item, index))
            .OrderBy(
                row => StageRelOrder.PreferSource(row.item.SourceStageRel, row.item.StageRel, row.item.ObjectKey),
                Comparer<string>.Create(StageRelOrder.Compare))
            .ThenBy(row => row.index)
            .Select(row => row.item)
            .ToList();
    }
}
