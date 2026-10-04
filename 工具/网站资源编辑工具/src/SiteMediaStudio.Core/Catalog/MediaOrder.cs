namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 内容层 media 的显示顺序：按对象键重排，未出现的条目保持相对序并接到末尾。
/// </summary>
public static class MediaOrder
{
    /// <summary>
    /// 按给定对象键序重排；空序则原样复制。
    /// </summary>
    public static List<WorkMediaItem> Apply(
        IReadOnlyList<WorkMediaItem> mediaList,
        IReadOnlyList<string>? objectList)
    {
        if (mediaList == null)
        {
            return new List<WorkMediaItem>();
        }

        if (objectList == null || objectList.Count == 0)
        {
            return mediaList.ToList();
        }

        var remainingList = mediaList.ToList();
        var resultList = new List<WorkMediaItem>(mediaList.Count);
        foreach (var raw in objectList)
        {
            var key = JsonUtil.ToRel(raw ?? "");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var index = remainingList.FindIndex(item =>
                string.Equals(JsonUtil.ToRel(item.Src ?? ""), key, StringComparison.Ordinal));
            if (index < 0)
            {
                continue;
            }

            resultList.Add(remainingList[index]);
            remainingList.RemoveAt(index);
        }

        resultList.AddRange(remainingList);
        return resultList;
    }

    /// <summary>
    /// 带 <c>src</c> 的对象键按显示顺序。
    /// </summary>
    public static List<string> SrcList(IEnumerable<WorkMediaItem>? mediaList)
    {
        if (mediaList == null)
        {
            return new List<string>();
        }

        return mediaList
            .Where(item => !string.IsNullOrWhiteSpace(item.Src) && !VideoEncodeRules.IsPosterObject(item.Src))
            .Select(item => JsonUtil.ToRel(item.Src!))
            .ToList();
    }

    /// <summary>
    /// 两条对象键列表是否同序。
    /// </summary>
    public static bool SameOrder(IReadOnlyList<string>? leftList, IReadOnlyList<string>? rightList)
    {
        if (leftList == null || rightList == null || leftList.Count != rightList.Count)
        {
            return leftList == null && rightList == null;
        }

        for (var i = 0; i < leftList.Count; i++)
        {
            if (!string.Equals(
                    JsonUtil.ToRel(leftList[i] ?? ""),
                    JsonUtil.ToRel(rightList[i] ?? ""),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 把一组条目移到插入下标；插入下标按移动前的列表计算。
    /// </summary>
    public static List<T> MoveItems<T>(
        IReadOnlyList<T> sourceList,
        IReadOnlyList<T> movingList,
        int insertIndex)
        where T : class
    {
        if (sourceList == null || sourceList.Count == 0)
        {
            return new List<T>();
        }

        var movingSet = new HashSet<T>(
            (movingList ?? Array.Empty<T>()).Where(item => sourceList.Contains(item)));
        if (movingSet.Count == 0)
        {
            return sourceList.ToList();
        }

        var dest = Math.Clamp(insertIndex, 0, sourceList.Count);
        dest -= sourceList.Take(dest).Count(movingSet.Contains);
        var resultList = sourceList.Where(item => !movingSet.Contains(item)).ToList();
        dest = Math.Clamp(dest, 0, resultList.Count);
        resultList.InsertRange(dest, sourceList.Where(movingSet.Contains));
        return resultList;
    }

    /// <summary>
    /// 按对象键重排站点卡片；已隐藏条目始终排在末尾。
    /// </summary>
    public static List<SiteItem> ApplySiteItems(
        IReadOnlyList<SiteItem> siteList,
        IReadOnlyList<string>? objectList)
    {
        if (siteList == null || siteList.Count == 0)
        {
            return new List<SiteItem>();
        }

        var visibleList = siteList.Where(item => !item.IsHidden).ToList();
        var hiddenList = siteList.Where(item => item.IsHidden).ToList();
        if (objectList != null && objectList.Count > 0)
        {
            var remainingList = visibleList.ToList();
            var orderedList = new List<SiteItem>(visibleList.Count);
            foreach (var raw in objectList)
            {
                var key = JsonUtil.ToRel(raw ?? "");
                var index = remainingList.FindIndex(item =>
                    string.Equals(JsonUtil.ToRel(item.ObjectKey), key, StringComparison.Ordinal));
                if (index < 0)
                {
                    continue;
                }

                orderedList.Add(remainingList[index]);
                remainingList.RemoveAt(index);
            }

            orderedList.AddRange(remainingList);
            visibleList = orderedList;
        }

        var total = visibleList.Count + hiddenList.Count;
        var resultList = new List<SiteItem>(total);
        for (var i = 0; i < visibleList.Count; i++)
        {
            resultList.Add(visibleList[i].WithDisplay(i, total));
        }

        for (var i = 0; i < hiddenList.Count; i++)
        {
            resultList.Add(hiddenList[i].WithDisplay(visibleList.Count + i, total));
        }

        return resultList;
    }
}
