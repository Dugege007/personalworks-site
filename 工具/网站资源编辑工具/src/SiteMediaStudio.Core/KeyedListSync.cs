using System.Collections.ObjectModel;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 按稳定键对齐可观察列表：只移除、插入或挪动变化项。
/// </summary>
public static class KeyedListSync
{
    /// <summary>
    /// 把 <paramref name="target"/> 调整为 <paramref name="nextList"/> 的顺序与成员，复用同一引用。
    /// </summary>
    public static void Align<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> nextList,
        Func<T, string> keyOf)
        where T : class
    {
        var nextSet = new HashSet<string>(nextList.Select(keyOf), StringComparer.Ordinal);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!nextSet.Contains(keyOf(target[i])))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < nextList.Count; i++)
        {
            var item = nextList[i];
            if (i < target.Count && ReferenceEquals(target[i], item))
            {
                continue;
            }

            var from = IndexOf(target, item);
            if (from >= 0)
            {
                target.Move(from, i);
            }
            else
            {
                target.Insert(i, item);
            }
        }
    }

    /// <summary>
    /// 按引用查找下标。
    /// </summary>
    private static int IndexOf<T>(ObservableCollection<T> target, T item)
        where T : class
    {
        for (var i = 0; i < target.Count; i++)
        {
            if (ReferenceEquals(target[i], item))
            {
                return i;
            }
        }

        return -1;
    }
}
