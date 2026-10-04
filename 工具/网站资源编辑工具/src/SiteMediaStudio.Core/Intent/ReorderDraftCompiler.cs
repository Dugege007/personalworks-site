namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把本机排序草稿编成 <c>media.reorder</c> 意图条。
/// </summary>
public static class ReorderDraftCompiler
{
    /// <summary>
    /// 工作区匹配且顺序与内容层不同的草稿转意图。
    /// </summary>
    public static IReadOnlyList<IntentItem> ToItems(WorkspaceSession session, ReorderDraftFile? file)
    {
        if (session == null || file == null || !ReorderDraftStore.MatchesWorkspace(file, session.Profile))
        {
            return Array.Empty<IntentItem>();
        }

        var itemList = new List<IntentItem>();
        foreach (var entry in file.Entries)
        {
            if (!TryParseWorkKey(entry.Key, out var channel, out var workId))
            {
                continue;
            }

            var work = session.Works.FirstOrDefault(item =>
                !item.IsUnregistered
                && string.Equals(item.Id, workId, StringComparison.Ordinal)
                && string.Equals(item.Channel, channel, StringComparison.Ordinal));
            if (work == null)
            {
                continue;
            }

            var publishedList = MediaOrder.SrcList(work.Media);
            var draftList = (entry.ObjectList ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => JsonUtil.ToRel(item))
                .ToList();
            if (draftList.Count == 0 || MediaOrder.SameOrder(draftList, publishedList))
            {
                continue;
            }

            itemList.Add(new IntentItem
            {
                Intent = MediaIntentCodes.MediaReorder,
                Channel = channel,
                WorkId = workId,
                ObjectList = draftList
            });
        }

        return itemList;
    }

    /// <summary>
    /// 由意图条还原草稿键。
    /// </summary>
    public static string? KeyOf(IntentItem item)
    {
        if (item == null
            || !string.Equals(item.Intent, MediaIntentCodes.MediaReorder, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(item.Channel)
            || string.IsNullOrWhiteSpace(item.WorkId))
        {
            return null;
        }

        return ReorderDraftStore.WorkKey(item.Channel, item.WorkId);
    }

    /// <summary>
    /// 解析 <c>work:channel:id</c>。
    /// </summary>
    public static bool TryParseWorkKey(string? key, out string channel, out string workId)
    {
        channel = "";
        workId = "";
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("work:", StringComparison.Ordinal))
        {
            return false;
        }

        var body = key["work:".Length..];
        var split = body.IndexOf(':');
        if (split <= 0 || split >= body.Length - 1)
        {
            return false;
        }

        channel = body[..split];
        workId = body[(split + 1)..];
        return !string.IsNullOrWhiteSpace(channel) && !string.IsNullOrWhiteSpace(workId);
    }
}
