namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 项目栏指针选区：Ctrl 切换，Shift 按可见顺序连选。
/// </summary>
public static class NavPointerSelection
{
    /// <summary>
    /// 一次指针选区的结果：选中项、连选锚点、网格焦点。
    /// </summary>
    public sealed class Result<T> where T : class
    {
        public Result(IReadOnlyList<T> selected, T? anchor, T? focus)
        {
            Selected = selected;
            Anchor = anchor;
            Focus = focus;
        }

        public IReadOnlyList<T> Selected { get; }

        public T? Anchor { get; }

        public T? Focus { get; }
    }

    /// <summary>
    /// 由当前选区与修饰键算出下一选区。连选只覆盖可见序列；收起节点上的既有选中在切换时保留。
    /// </summary>
    public static Result<T> Apply<T>(
        IReadOnlyList<T> visible,
        IReadOnlyCollection<T> current,
        T? anchor,
        T clicked,
        bool toggle,
        bool extend) where T : class
    {
        var anchorIndex = IndexOf(visible, anchor);
        if (extend && anchorIndex >= 0)
        {
            var clickedIndex = IndexOf(visible, clicked);
            if (clickedIndex >= 0)
            {
                var next = NewSet(toggle ? current : Array.Empty<T>());
                var lo = Math.Min(anchorIndex, clickedIndex);
                var hi = Math.Max(anchorIndex, clickedIndex);
                for (var i = lo; i <= hi; i++)
                {
                    next.Add(visible[i]);
                }

                return new Result<T>(Order(visible, current, next), anchor, clicked);
            }
        }

        if (toggle)
        {
            var next = NewSet(current);
            if (!next.Remove(clicked))
            {
                next.Add(clicked);
            }

            var focus = next.Contains(clicked) ? clicked : Nearest(visible, next, clicked);
            return new Result<T>(Order(visible, current, next), clicked, focus);
        }

        return new Result<T>(new[] { clicked }, clicked, clicked);
    }

    /// <summary>
    /// 按引用在可见序列中查找下标。
    /// </summary>
    private static int IndexOf<T>(IReadOnlyList<T> visible, T? item) where T : class
    {
        if (item == null)
        {
            return -1;
        }

        for (var i = 0; i < visible.Count; i++)
        {
            if (ReferenceEquals(visible[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 去掉焦点后，取可见序列里离它最近的仍选中项。
    /// </summary>
    private static T? Nearest<T>(IReadOnlyList<T> visible, HashSet<T> selected, T clicked) where T : class
    {
        var origin = IndexOf(visible, clicked);
        T? best = null;
        var bestDist = int.MaxValue;
        var bestIndex = int.MaxValue;
        for (var i = 0; i < visible.Count; i++)
        {
            if (!selected.Contains(visible[i]))
            {
                continue;
            }

            var dist = origin < 0 ? 0 : Math.Abs(i - origin);
            if (dist < bestDist || (dist == bestDist && i < bestIndex))
            {
                best = visible[i];
                bestDist = dist;
                bestIndex = i;
            }
        }

        if (best != null)
        {
            return best;
        }

        foreach (var item in selected)
        {
            return item;
        }

        return null;
    }

    /// <summary>
    /// 先按可见顺序，再补上不在可见序列里的既有选中。
    /// </summary>
    private static List<T> Order<T>(
        IReadOnlyList<T> visible,
        IReadOnlyCollection<T> current,
        HashSet<T> selected) where T : class
    {
        var ordered = new List<T>(selected.Count);
        foreach (var item in visible)
        {
            if (selected.Remove(item))
            {
                ordered.Add(item);
            }
        }

        foreach (var item in current)
        {
            if (selected.Remove(item))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }

    /// <summary>
    /// 以引用相等建立选区集合。
    /// </summary>
    private static HashSet<T> NewSet<T>(IEnumerable<T> items) where T : class
    {
        return new HashSet<T>(items, ReferenceEqualityComparer.Instance);
    }
}
