using System.Collections.ObjectModel;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 项目栏按稳定键就地增删，并恢复折叠 / 选中。
/// </summary>
internal static class NavTreeSync
{
    /// <summary>
    /// 记录各节点当前是否展开。
    /// </summary>
    public static Dictionary<string, bool> CaptureExpand(IEnumerable<NavNodeViewModel> nodes)
    {
        var expandDict = new Dictionary<string, bool>(StringComparer.Ordinal);
        CollectExpand(nodes, expandDict);
        return expandDict;
    }

    /// <summary>
    /// 只收集已收起的节点键，供写入本机设置。
    /// </summary>
    public static List<string> CaptureCollapsed(IEnumerable<NavNodeViewModel> nodes)
    {
        var collapsedList = new List<string>();
        CollectCollapsed(nodes, collapsedList);
        return collapsedList;
    }

    /// <summary>
    /// 按键复用已有节点，只插入、移除或挪动变化项。
    /// </summary>
    public static void Sync(
        ObservableCollection<NavNodeViewModel> target,
        IReadOnlyList<NavNodeViewModel> desired)
    {
        var existingDict = new Dictionary<string, NavNodeViewModel>(StringComparer.Ordinal);
        foreach (var node in target)
        {
            existingDict[node.NavKey] = node;
        }

        var nextList = new List<NavNodeViewModel>(desired.Count);
        foreach (var incoming in desired)
        {
            if (existingDict.TryGetValue(incoming.NavKey, out var existing))
            {
                existing.ApplyCatalog(incoming);
                Sync(existing.Children, incoming.Children.ToList());
                nextList.Add(existing);
            }
            else
            {
                nextList.Add(incoming);
            }
        }

        Align(target, nextList);
    }

    /// <summary>
    /// 把快照中的展开状态写回仍在树上的节点。
    /// </summary>
    public static void ApplyExpand(
        IEnumerable<NavNodeViewModel> nodes,
        IReadOnlyDictionary<string, bool> expandDict)
    {
        foreach (var node in nodes)
        {
            if (expandDict.TryGetValue(node.NavKey, out var expanded))
            {
                node.IsExpanded = expanded;
            }

            ApplyExpand(node.Children, expandDict);
        }
    }

    /// <summary>
    /// 按当前展开状态列出可见节点，供 Shift 连选。
    /// </summary>
    public static List<NavNodeViewModel> FlattenVisible(IEnumerable<NavNodeViewModel> nodes)
    {
        var flat = new List<NavNodeViewModel>();
        CollectVisible(nodes, flat);
        return flat;
    }

    /// <summary>
    /// 收集整棵子树里已选中的节点。
    /// </summary>
    public static void CollectSelected(IEnumerable<NavNodeViewModel> nodes, ICollection<NavNodeViewModel> selected)
    {
        foreach (var node in nodes)
        {
            if (node.IsSelected)
            {
                selected.Add(node);
            }

            CollectSelected(node.Children, selected);
        }
    }

    /// <summary>
    /// 按给定集合写下选中标记，其余节点清除。
    /// </summary>
    public static void ApplySelectionSet(
        IEnumerable<NavNodeViewModel> roots,
        IReadOnlyCollection<NavNodeViewModel> selected)
    {
        var selectedSet = new HashSet<NavNodeViewModel>(selected);
        WriteSelection(roots, selectedSet);
    }

    /// <summary>
    /// 只高亮目标节点，并展开其祖先以便容器生成。
    /// </summary>
    public static void ApplySelection(IEnumerable<NavNodeViewModel> roots, NavNodeViewModel? selected)
    {
        ApplySelectionSet(roots, selected == null ? Array.Empty<NavNodeViewModel>() : new[] { selected });
        if (selected != null)
        {
            ExpandAncestors(roots, selected);
        }
    }

    /// <summary>
    /// 按目标顺序对齐集合，尽量 Move / Insert，避免整表 Clear。
    /// </summary>
    private static void Align(
        ObservableCollection<NavNodeViewModel> target,
        IReadOnlyList<NavNodeViewModel> nextList)
    {
        var nextSet = new HashSet<string>(nextList.Select(node => node.NavKey), StringComparer.Ordinal);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!nextSet.Contains(target[i].NavKey))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < nextList.Count; i++)
        {
            var node = nextList[i];
            if (i < target.Count && ReferenceEquals(target[i], node))
            {
                continue;
            }

            var from = IndexOf(target, node);
            if (from >= 0)
            {
                target.Move(from, i);
            }
            else
            {
                target.Insert(i, node);
            }
        }
    }

    /// <summary>
    /// 收集整棵子树的展开状态。
    /// </summary>
    private static void CollectExpand(
        IEnumerable<NavNodeViewModel> nodes,
        IDictionary<string, bool> expandDict)
    {
        foreach (var node in nodes)
        {
            expandDict[node.NavKey] = node.IsExpanded;
            CollectExpand(node.Children, expandDict);
        }
    }

    /// <summary>
    /// 收集整棵子树中已收起的键。
    /// </summary>
    private static void CollectCollapsed(IEnumerable<NavNodeViewModel> nodes, ICollection<string> collapsedList)
    {
        foreach (var node in nodes)
        {
            if (!node.IsExpanded)
            {
                collapsedList.Add(node.NavKey);
            }

            CollectCollapsed(node.Children, collapsedList);
        }
    }

    /// <summary>
    /// 深度优先收集已展开路径上的节点。
    /// </summary>
    private static void CollectVisible(IEnumerable<NavNodeViewModel> nodes, ICollection<NavNodeViewModel> flat)
    {
        foreach (var node in nodes)
        {
            flat.Add(node);
            if (node.IsExpanded)
            {
                CollectVisible(node.Children, flat);
            }
        }
    }

    /// <summary>
    /// 把选中标记写到整棵子树。
    /// </summary>
    private static void WriteSelection(IEnumerable<NavNodeViewModel> nodes, HashSet<NavNodeViewModel> selectedSet)
    {
        foreach (var node in nodes)
        {
            var on = selectedSet.Contains(node);
            if (node.IsSelected != on)
            {
                node.IsSelected = on;
            }

            WriteSelection(node.Children, selectedSet);
        }
    }

    /// <summary>
    /// 若目标在某节点的子树中，则展开该节点。
    /// </summary>
    private static bool ExpandAncestors(IEnumerable<NavNodeViewModel> nodes, NavNodeViewModel target)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, target))
            {
                return true;
            }

            if (ExpandAncestors(node.Children, target))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 按引用查找节点下标。
    /// </summary>
    private static int IndexOf(ObservableCollection<NavNodeViewModel> target, NavNodeViewModel node)
    {
        for (var i = 0; i < target.Count; i++)
        {
            if (ReferenceEquals(target[i], node))
            {
                return i;
            }
        }

        return -1;
    }
}
