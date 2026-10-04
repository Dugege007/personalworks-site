namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一份选区快照：卡片键顺序与焦点键。
/// </summary>
public sealed class CardSelectionSnapshot : IEquatable<CardSelectionSnapshot>
{
    public CardSelectionSnapshot(IReadOnlyList<string> keyList, string? focusKey)
    {
        KeyList = keyList.Where(key => !string.IsNullOrWhiteSpace(key)).ToList();
        FocusKey = string.IsNullOrWhiteSpace(focusKey) ? null : focusKey;
    }

    public IReadOnlyList<string> KeyList { get; }

    public string? FocusKey { get; }

    public bool IsEmpty => KeyList.Count == 0;

    public bool Equals(CardSelectionSnapshot? other)
    {
        if (other is null
            || KeyList.Count != other.KeyList.Count
            || !string.Equals(FocusKey, other.FocusKey, StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < KeyList.Count; i++)
        {
            if (!string.Equals(KeyList[i], other.KeyList[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CardSelectionSnapshot);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FocusKey, StringComparer.Ordinal);
        foreach (var key in KeyList)
        {
            hash.Add(key, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// 记录最近若干次选区，供「重新选择」逐条回退。
/// </summary>
public sealed class CardSelectionHistory
{
    public const int MaxCount = 5;

    private readonly List<CardSelectionSnapshot> mSnapshotList = new();

    public int Count => mSnapshotList.Count;

    public bool CanRestore => mSnapshotList.Count > 0;

    /// <summary>
    /// 旧选区非空且与新选区不同时入栈；超出上限丢掉最早一条。空选区不记。
    /// </summary>
    public void Remember(CardSelectionSnapshot previous, CardSelectionSnapshot next)
    {
        if (previous.IsEmpty || previous.Equals(next))
        {
            return;
        }

        mSnapshotList.Add(previous);
        if (mSnapshotList.Count > MaxCount)
        {
            mSnapshotList.RemoveAt(0);
        }
    }

    /// <summary>
    /// 弹出最近一条选区；无记录则返回空。
    /// </summary>
    public CardSelectionSnapshot? Restore()
    {
        if (mSnapshotList.Count == 0)
        {
            return null;
        }

        var last = mSnapshotList[^1];
        mSnapshotList.RemoveAt(mSnapshotList.Count - 1);
        return last;
    }

    /// <summary>
    /// 丢掉全部记录，例如切栏目或换工作区。
    /// </summary>
    public void Clear()
    {
        mSnapshotList.Clear();
    }
}
