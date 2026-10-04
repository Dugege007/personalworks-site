namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 台账路径重挂：只改 stageRel，不改对象键。
/// </summary>
public static class StageRelocateRules
{
    /// <summary>
    /// 投放箱相对路径的目录前缀，含末尾斜杠。
    /// </summary>
    public static string DirectoryPrefix(string? stageRel)
    {
        var normalized = JsonUtil.ToRel(stageRel ?? "");
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? "" : normalized[..(slash + 1)];
    }

    /// <summary>
    /// 新旧路径是否换了作品夹，可整夹重挂。
    /// </summary>
    public static bool IsFolderMove(string? stageRelBefore, string? stageRelAfter)
    {
        var before = DirectoryPrefix(stageRelBefore);
        var after = DirectoryPrefix(stageRelAfter);
        return !string.IsNullOrWhiteSpace(before)
            && !string.IsNullOrWhiteSpace(after)
            && !string.Equals(before, after, StringComparison.Ordinal);
    }

    /// <summary>
    /// 按旧夹前缀列出将改写的 published 条目。
    /// </summary>
    public static IReadOnlyList<IntentItem> PlanFolder(
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict,
        string stageRelBefore,
        string stageRelAfter)
    {
        var oldPrefix = DirectoryPrefix(stageRelBefore);
        var newPrefix = DirectoryPrefix(stageRelAfter);
        var oldName = Path.GetFileName(JsonUtil.ToRel(stageRelBefore));
        var newName = Path.GetFileName(JsonUtil.ToRel(stageRelAfter));
        var itemList = new List<IntentItem>();
        if (string.IsNullOrWhiteSpace(oldPrefix) || string.IsNullOrWhiteSpace(newPrefix))
        {
            return itemList;
        }

        foreach (var record in ledgerDict.Values.OrderBy(item => item.Object, StringComparer.Ordinal))
        {
            if (!string.Equals(record.Status, "published", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(record.StageRel)
                || !record.StageRel.StartsWith(oldPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var rest = record.StageRel[oldPrefix.Length..];
            var after = string.Equals(rest, oldName, StringComparison.Ordinal)
                ? newPrefix + newName
                : newPrefix + rest;
            itemList.Add(CreateItem(record.Object, record.StageRel, after));
        }

        return itemList;
    }

    /// <summary>
    /// 组装单条重挂意图。
    /// </summary>
    public static IntentItem CreateItem(string objectKey, string stageRelBefore, string stageRelAfter)
    {
        var before = JsonUtil.ToRel(stageRelBefore);
        var after = JsonUtil.ToRel(stageRelAfter);
        return new IntentItem
        {
            Intent = MediaIntentCodes.StageRelocate,
            Object = objectKey,
            Channel = ChannelOf(objectKey),
            StageRel = after,
            StageRelBefore = before,
            StageRelAfter = after
        };
    }

    /// <summary>
    /// 对象键第一段作为栏目。
    /// </summary>
    public static string ChannelOf(string? objectKey)
    {
        var normalized = JsonUtil.ToRel(objectKey ?? "");
        var slash = normalized.IndexOf('/');
        return slash < 0 ? normalized : normalized[..slash];
    }
}
