namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 投放箱路径的逻辑序：数字按数值比，其余按序。工具网格与内容层 media 共用。
/// </summary>
public static class StageRelOrder
{
    /// <summary>
    /// 比较两条相对路径；斜杠统一后按逻辑序。
    /// </summary>
    public static int Compare(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        var i = 0;
        var j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var (valueA, nextI) = ReadNumber(a, i);
                var (valueB, nextJ) = ReadNumber(b, j);
                if (valueA != valueB)
                {
                    return valueA < valueB ? -1 : 1;
                }

                i = nextI;
                j = nextJ;
                continue;
            }

            var ca = char.ToLowerInvariant(a[i]);
            var cb = char.ToLowerInvariant(b[j]);
            if (ca != cb)
            {
                return ca.CompareTo(cb);
            }

            i++;
            j++;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    /// <summary>
    /// 对象键对应的排序键：优先未压成片的原片路径。
    /// </summary>
    public static string PreferSource(string? sourceStageRel, string? stageRel, string? objectKey)
    {
        if (!string.IsNullOrWhiteSpace(sourceStageRel) && !MediaPathRules.IsDerivedStageRel(sourceStageRel))
        {
            return JsonUtil.ToRel(sourceStageRel);
        }

        if (!string.IsNullOrWhiteSpace(stageRel) && !MediaPathRules.IsDerivedStageRel(stageRel))
        {
            return JsonUtil.ToRel(stageRel);
        }

        return JsonUtil.ToRel(objectKey ?? "");
    }

    /// <summary>
    /// 由台账与意图条组装对象键到投放箱路径的对照。意图条覆盖台账。
    /// </summary>
    public static Dictionary<string, string> BuildSortKeyDict(
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict,
        IEnumerable<IntentItem>? itemList = null)
    {
        var resultDict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ledgerDict != null)
        {
            foreach (var record in ledgerDict.Values)
            {
                Put(resultDict, record.Object, record.SourceStageRel, record.StageRel);
            }
        }

        if (itemList == null)
        {
            return resultDict;
        }

        foreach (var item in itemList)
        {
            Put(resultDict, item.Object, item.SourceStageRel, item.StageRel);
        }

        return resultDict;
    }

    /// <summary>
    /// 按投放箱路径重排媒体；没有原片路径时保持原序，避免对象键字典序打乱已有图组。
    /// </summary>
    public static List<WorkMediaItem> SortMedia(
        IReadOnlyList<WorkMediaItem> mediaList,
        IReadOnlyDictionary<string, string>? sortKeyDict)
    {
        var keyedList = mediaList
            .Select((item, index) => (item, index, key: ResolveSortKey(item.Src, sortKeyDict)))
            .ToList();
        if (!keyedList.Any(row => HasDistinctStagePath(row.item.Src, row.key)))
        {
            return mediaList.ToList();
        }

        return keyedList
            .OrderBy(row => row.key, Comparer<string>.Create(Compare))
            .ThenBy(row => row.index)
            .Select(row => row.item)
            .ToList();
    }

    /// <summary>
    /// 按投放箱路径重排对象键列表；没有原片路径时保持原序。
    /// </summary>
    public static List<string> SortObjectKeys(
        IReadOnlyList<string> objectList,
        IReadOnlyDictionary<string, string>? sortKeyDict)
    {
        var keyedList = objectList
            .Select((objectKey, index) => (objectKey, index, key: ResolveSortKey(objectKey, sortKeyDict)))
            .ToList();
        if (!keyedList.Any(row => HasDistinctStagePath(row.objectKey, row.key)))
        {
            return objectList.ToList();
        }

        return keyedList
            .OrderBy(row => row.key, Comparer<string>.Create(Compare))
            .ThenBy(row => row.index)
            .Select(row => row.objectKey)
            .ToList();
    }

    /// <summary>
    /// 排序键是否来自投放箱路径，而不是对象键本身。
    /// </summary>
    private static bool HasDistinctStagePath(string? objectKey, string sortKey)
    {
        var obj = JsonUtil.ToRel(objectKey ?? "");
        return !string.IsNullOrWhiteSpace(sortKey)
            && !string.Equals(sortKey, obj, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 写入或覆盖一条对象键的排序路径。
    /// </summary>
    private static void Put(
        Dictionary<string, string> dict,
        string? objectKey,
        string? sourceStageRel,
        string? stageRel)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        var key = PreferSource(sourceStageRel, stageRel, objectKey);
        if (!string.IsNullOrWhiteSpace(key))
        {
            dict[JsonUtil.ToRel(objectKey)] = key;
        }
    }

    /// <summary>
    /// 取出对象键对应的排序路径；未登记则用对象键本身。
    /// </summary>
    private static string ResolveSortKey(string? objectKey, IReadOnlyDictionary<string, string>? sortKeyDict)
    {
        var obj = JsonUtil.ToRel(objectKey ?? "");
        if (sortKeyDict != null
            && sortKeyDict.TryGetValue(obj, out var mapped)
            && !string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        return obj;
    }

    /// <summary>
    /// 斜杠统一、去首尾分隔。
    /// </summary>
    private static string Normalize(string? value)
    {
        return JsonUtil.ToRel(value ?? "");
    }

    /// <summary>
    /// 从当前位置读一段连续数字。
    /// </summary>
    private static (long Value, int Next) ReadNumber(string text, int start)
    {
        var next = start;
        while (next < text.Length && char.IsDigit(text[next]))
        {
            next++;
        }

        var span = text.AsSpan(start, next - start);
        if (long.TryParse(span, out var value))
        {
            return (value, next);
        }

        return (long.MaxValue, next);
    }
}
