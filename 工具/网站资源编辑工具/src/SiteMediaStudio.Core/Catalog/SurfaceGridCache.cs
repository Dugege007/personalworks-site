namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 按栏目节点与视图记住已物化的网格，切走后再回来复用同一批卡片。
/// </summary>
public sealed class SurfaceGridCache<TCard, TRow>
{
    public const int DefaultCapacity = 16;

    private readonly int mCapacity;
    private readonly Dictionary<string, LinkedListNode<Entry>> mNodeDict = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> mOrderList = new();

    public SurfaceGridCache(int capacity = DefaultCapacity)
    {
        mCapacity = Math.Max(1, capacity);
    }

    /// <summary>
    /// 视图与项目栏节点合成稳定键。
    /// </summary>
    public static string BuildKey(string pane, string navKey)
    {
        return pane + "|" + navKey;
    }

    public int Count => mNodeDict.Count;

    /// <summary>
    /// 写入或覆盖该表面的卡片与选区，并标为最近使用。
    /// </summary>
    public void Park(
        string key,
        IReadOnlyList<TCard> cardList,
        IReadOnlyList<TRow> rowList,
        IReadOnlyList<string> selectedKeyList,
        string? focusKey)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var snapshot = new SurfaceGridSnapshot<TCard, TRow>(
            cardList.ToList(),
            rowList.ToList(),
            selectedKeyList.ToList(),
            focusKey);
        if (mNodeDict.TryGetValue(key, out var existing))
        {
            existing.Value = new Entry(key, snapshot);
            mOrderList.Remove(existing);
            mOrderList.AddFirst(existing);
            return;
        }

        var node = mOrderList.AddFirst(new Entry(key, snapshot));
        mNodeDict[key] = node;
        while (mNodeDict.Count > mCapacity)
        {
            var last = mOrderList.Last;
            if (last == null)
            {
                break;
            }

            mOrderList.RemoveLast();
            mNodeDict.Remove(last.Value.Key);
        }
    }

    /// <summary>
    /// 取出已记住的网格；命中则标为最近使用。
    /// </summary>
    public bool TryGet(string key, out SurfaceGridSnapshot<TCard, TRow>? snapshot)
    {
        if (!mNodeDict.TryGetValue(key, out var node))
        {
            snapshot = null;
            return false;
        }

        mOrderList.Remove(node);
        mOrderList.AddFirst(node);
        snapshot = node.Value.Snapshot;
        return true;
    }

    /// <summary>
    /// 丢掉全部表面。换工作区后调用。
    /// </summary>
    public void Clear()
    {
        mNodeDict.Clear();
        mOrderList.Clear();
    }

    /// <summary>
    /// 遍历所有已记住表面的卡片，例如缩放后清掉旧边长缩略图。
    /// </summary>
    public void ForEachCard(Action<TCard> action)
    {
        foreach (var entry in mOrderList)
        {
            foreach (var card in entry.Snapshot.CardList)
            {
                action(card);
            }
        }
    }

    private readonly record struct Entry(string Key, SurfaceGridSnapshot<TCard, TRow> Snapshot);
}

/// <summary>
/// 某一栏目节点与视图下的卡片、对照行与选区。
/// </summary>
public sealed class SurfaceGridSnapshot<TCard, TRow>
{
    public SurfaceGridSnapshot(
        IReadOnlyList<TCard> cardList,
        IReadOnlyList<TRow> rowList,
        IReadOnlyList<string> selectedKeyList,
        string? focusKey)
    {
        CardList = cardList;
        RowList = rowList;
        SelectedKeyList = selectedKeyList;
        FocusKey = focusKey;
    }

    public IReadOnlyList<TCard> CardList { get; }

    public IReadOnlyList<TRow> RowList { get; }

    public IReadOnlyList<string> SelectedKeyList { get; }

    public string? FocusKey { get; }
}
