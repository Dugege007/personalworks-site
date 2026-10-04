namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 工具网格查看序的键。不对应内容层数组序。
/// </summary>
public enum GridSortKey
{
    Name,
    Time,
    Stars,
    Size
}

/// <summary>
/// 单个查看键的方向。从低到高为升序。
/// </summary>
public enum GridSortDir
{
    Asc,
    Desc
}

/// <summary>
/// 一条已入列的查看键。列表顺序即优先级。
/// </summary>
public readonly record struct GridSortRule(GridSortKey Key, GridSortDir Dir);

/// <summary>
/// 一条资源参与比较的取值。缺时间表示正式位没有文件。
/// </summary>
public readonly record struct GridSortFact(
    int CatalogIndex,
    string Name,
    DateTime? TimeUtc,
    int Stars,
    long Size);

/// <summary>
/// 多键查看比较。全相等时按编目下标，不改源集合。
/// </summary>
public static class GridSort
{
    /// <summary>
    /// 按优先级比较两条。无入列键时只比编目下标。
    /// </summary>
    public static int Compare(GridSortFact left, GridSortFact right, IReadOnlyList<GridSortRule> ruleList)
    {
        foreach (var rule in ruleList)
        {
            var compared = CompareKey(left, right, rule.Key);
            if (compared == 0)
            {
                continue;
            }

            // 星级固定从高到低，不读该键方向。
            var highToLow = rule.Key == GridSortKey.Stars || rule.Dir == GridSortDir.Desc;
            return highToLow ? -compared : compared;
        }

        return left.CatalogIndex.CompareTo(right.CatalogIndex);
    }

    /// <summary>
    /// 单键的从低到高比较。时间缺文件大于任何实有时间，方向反转时一并反过来。
    /// </summary>
    private static int CompareKey(GridSortFact left, GridSortFact right, GridSortKey key)
    {
        return key switch
        {
            GridSortKey.Name => StageRelOrder.Compare(left.Name, right.Name),
            GridSortKey.Time => CompareTime(left.TimeUtc, right.TimeUtc),
            GridSortKey.Stars => left.Stars.CompareTo(right.Stars),
            GridSortKey.Size => left.Size.CompareTo(right.Size),
            _ => 0
        };
    }

    /// <summary>
    /// 旧在前。两侧都缺文件则相等；只有一侧缺文件时缺文件在后。
    /// </summary>
    private static int CompareTime(DateTime? left, DateTime? right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return left.Value.CompareTo(right.Value);
    }
}

/// <summary>
/// 一条已写入设置的查看键。未知键由读取方跳过。
/// </summary>
public sealed class GridSortPreference
{
    public string Key { get; set; } = "";

    public string Dir { get; set; } = "asc";
}

/// <summary>
/// 查看序入列与方向。全工具一份，调用方写入设置文件。
/// </summary>
public sealed class GridSortState
{
    private readonly List<GridSortKey> mKeyList = new();
    private readonly Dictionary<GridSortKey, GridSortDir> mDirDict = new()
    {
        [GridSortKey.Name] = GridSortDir.Asc,
        [GridSortKey.Time] = GridSortDir.Asc,
        [GridSortKey.Stars] = GridSortDir.Desc,
        [GridSortKey.Size] = GridSortDir.Asc
    };

    private GridSortState()
    {
    }

    /// <summary>
    /// 缺省不入列任何键，网格保持编目原序。
    /// </summary>
    public static GridSortState CreateDefault()
    {
        return new GridSortState();
    }

    /// <summary>
    /// 按设置数组恢复。缺省、空数组与未知键都保持原序；星级固定从高到低。
    /// </summary>
    public static GridSortState FromPreferences(IReadOnlyList<GridSortPreference>? list)
    {
        var state = CreateDefault();
        if (list == null)
        {
            return state;
        }

        foreach (var item in list)
        {
            if (!TryParseKey(item.Key, out var key) || state.Contains(key))
            {
                continue;
            }

            state.Toggle(key);
            if (key != GridSortKey.Stars
                && string.Equals(item.Dir, "desc", StringComparison.OrdinalIgnoreCase))
            {
                state.Flip(key);
            }
        }

        return state;
    }

    /// <summary>
    /// 导出当前入列。数组顺序即优先级。星级只写从高到低。
    /// </summary>
    public List<GridSortPreference> ToPreferences()
    {
        var list = new List<GridSortPreference>(mKeyList.Count);
        foreach (var key in mKeyList)
        {
            var desc = key == GridSortKey.Stars || mDirDict[key] == GridSortDir.Desc;
            list.Add(new GridSortPreference
            {
                Key = KeyName(key),
                Dir = desc ? "desc" : "asc"
            });
        }

        return list;
    }

    /// <summary>
    /// 设置里的键名。无法识别则跳过。
    /// </summary>
    private static bool TryParseKey(string? text, out GridSortKey key)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "name":
                key = GridSortKey.Name;
                return true;
            case "time":
                key = GridSortKey.Time;
                return true;
            case "stars":
                key = GridSortKey.Stars;
                return true;
            case "size":
                key = GridSortKey.Size;
                return true;
            default:
                key = default;
                return false;
        }
    }

    /// <summary>
    /// 写入设置用的短键。
    /// </summary>
    private static string KeyName(GridSortKey key)
    {
        return key switch
        {
            GridSortKey.Name => "name",
            GridSortKey.Time => "time",
            GridSortKey.Stars => "stars",
            GridSortKey.Size => "size",
            _ => ""
        };
    }

    public bool HasActive => mKeyList.Count > 0;

    /// <summary>
    /// 当前入列规则。调用时复制，之后改状态不影响已取出的列表。
    /// </summary>
    public IReadOnlyList<GridSortRule> Rules =>
        mKeyList.Select(key => new GridSortRule(key, mDirDict[key])).ToArray();

    /// <summary>
    /// 收起态文案。无入列为原序；一键用选项名，降序附方向；多键写优先级与短名。
    /// </summary>
    public string Caption
    {
        get
        {
            if (mKeyList.Count == 0)
            {
                return "原序";
            }

            if (mKeyList.Count == 1)
            {
                var key = mKeyList[0];
                var label = Label(key);
                if (key == GridSortKey.Stars)
                {
                    return label;
                }

                return mDirDict[key] == GridSortDir.Desc ? label + " ▼" : label;
            }

            return string.Join(" · ", mKeyList.Select((key, index) => $"{index + 1} {ShortLabel(key)}"));
        }
    }

    /// <summary>
    /// 该键是否已入列。
    /// </summary>
    public bool Contains(GridSortKey key)
    {
        return mKeyList.Contains(key);
    }

    /// <summary>
    /// 从 1 起的优先级。未入列返回 0。
    /// </summary>
    public int Priority(GridSortKey key)
    {
        var index = mKeyList.IndexOf(key);
        return index < 0 ? 0 : index + 1;
    }

    /// <summary>
    /// 该键当前方向。未入列也保留，再次点名称时沿用。
    /// </summary>
    public GridSortDir Dir(GridSortKey key)
    {
        return mDirDict[key];
    }

    /// <summary>
    /// 点名称或左端数字：未入列则追加到末尾，已入列则移除并让其余键从 1 重排。
    /// </summary>
    public void Toggle(GridSortKey key)
    {
        var index = mKeyList.IndexOf(key);
        if (index >= 0)
        {
            mKeyList.RemoveAt(index);
            return;
        }

        mKeyList.Add(key);
    }

    /// <summary>
    /// 点箭头：只反向。不改变是否入列，也不改变优先级。星级固定从高到低。
    /// </summary>
    public void Flip(GridSortKey key)
    {
        if (key == GridSortKey.Stars)
        {
            return;
        }

        mDirDict[key] = mDirDict[key] == GridSortDir.Asc ? GridSortDir.Desc : GridSortDir.Asc;
    }

    /// <summary>
    /// 下拉与单键收起态用的全称。
    /// </summary>
    public static string Label(GridSortKey key)
    {
        return key switch
        {
            GridSortKey.Name => "按名称",
            GridSortKey.Time => "按时间",
            GridSortKey.Stars => "按星级",
            GridSortKey.Size => "按大小",
            _ => ""
        };
    }

    /// <summary>
    /// 多键收起态用的短名，不含「按」。
    /// </summary>
    public static string ShortLabel(GridSortKey key)
    {
        return key switch
        {
            GridSortKey.Name => "名称",
            GridSortKey.Time => "时间",
            GridSortKey.Stars => "星级",
            GridSortKey.Size => "大小",
            _ => ""
        };
    }
}
