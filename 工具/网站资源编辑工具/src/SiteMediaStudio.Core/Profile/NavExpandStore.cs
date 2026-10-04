namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 按工作区记住项目栏折叠键。
/// </summary>
public static class NavExpandStore
{
    /// <summary>
    /// 规范化配置路径，避免正反斜杠写成两条记录。
    /// </summary>
    public static string ProfileKey(string? profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(profilePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (ArgumentException)
        {
            return profilePath.Trim();
        }
        catch (NotSupportedException)
        {
            return profilePath.Trim();
        }
    }

    /// <summary>
    /// 折叠键写成「该键为收起」；未列入的节点保持默认展开。
    /// </summary>
    public static Dictionary<string, bool> ToExpandDict(IEnumerable<string>? collapsedKeys)
    {
        var expandDict = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (collapsedKeys == null)
        {
            return expandDict;
        }

        foreach (var key in collapsedKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                expandDict[key] = false;
            }
        }

        return expandDict;
    }

    /// <summary>
    /// 读取该工作区上次收起的节点键。
    /// </summary>
    public static IReadOnlyList<string> ReadCollapsed(AppSettings settings, string? profilePath)
    {
        var map = settings.CollapsedNavByProfile;
        if (map == null || map.Count == 0)
        {
            return Array.Empty<string>();
        }

        var key = ProfileKey(profilePath);
        if (map.TryGetValue(key, out var list) && list != null)
        {
            return list;
        }

        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && pair.Value != null)
            {
                return pair.Value;
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// 覆盖写入该工作区的折叠键。
    /// </summary>
    public static void WriteCollapsed(
        AppSettings settings,
        string? profilePath,
        IReadOnlyList<string> collapsedKeys)
    {
        var key = ProfileKey(profilePath);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        settings.CollapsedNavByProfile ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        settings.CollapsedNavByProfile[key] = collapsedKeys.ToList();
    }
}
