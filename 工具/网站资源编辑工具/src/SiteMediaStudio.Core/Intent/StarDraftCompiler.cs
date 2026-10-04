namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把本机脏星级草稿编成 <c>stars.update</c> 意图条。
/// </summary>
public static class StarDraftCompiler
{
    /// <summary>
    /// 工作区匹配的草稿条转意图；不匹配或空则无条。
    /// </summary>
    public static IReadOnlyList<IntentItem> ToItems(WorkspaceSession session, StarDraftFile? file)
    {
        if (session == null || file == null || !StarDraftStore.MatchesWorkspace(file, session.Profile))
        {
            return Array.Empty<IntentItem>();
        }

        var itemList = new List<IntentItem>();
        foreach (var entry in file.Entries)
        {
            if (entry.Stars is < 0 or > 5)
            {
                continue;
            }

            if (entry.Key.StartsWith("object:", StringComparison.Ordinal))
            {
                var objectKey = entry.Key["object:".Length..];
                var work = FindWork(session, objectKey);
                var media = work?.Media.FirstOrDefault(item =>
                    string.Equals(JsonUtil.ToRel(item.Src ?? ""), objectKey, StringComparison.Ordinal));
                itemList.Add(new IntentItem
                {
                    Intent = MediaIntentCodes.StarsUpdate,
                    Channel = work?.Channel,
                    WorkId = work?.Id,
                    Object = objectKey,
                    Stars = entry.Stars,
                    LabelBefore = media?.Label
                });
                continue;
            }

            if (entry.Key.StartsWith("stage:", StringComparison.Ordinal))
            {
                itemList.Add(new IntentItem
                {
                    Intent = MediaIntentCodes.StarsUpdate,
                    StageRel = entry.Key["stage:".Length..],
                    Stars = entry.Stars
                });
            }
        }

        return itemList;
    }

    /// <summary>
    /// 本批会写入内容层的星级。未入编的对象键和只等上页的原片留在草稿，不单独占一次执行。
    /// </summary>
    public static bool IsContentWrite(IntentItem item)
    {
        return item.Intent == MediaIntentCodes.StarsUpdate
            && item.Stars is >= 0 and <= 5
            && !string.IsNullOrWhiteSpace(item.Object)
            && !string.IsNullOrWhiteSpace(item.Channel)
            && !string.IsNullOrWhiteSpace(item.WorkId);
    }

    /// <summary>
    /// 由意图条还原草稿键，供执行后删齐。
    /// </summary>
    public static string? KeyOf(IntentItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Object))
        {
            return StarDraftStore.ObjectKey(item.Object);
        }

        if (!string.IsNullOrWhiteSpace(item.StageRel))
        {
            return StarDraftStore.StageKey(item.StageRel);
        }

        return null;
    }

    /// <summary>
    /// 本批已经写进内容层的草稿键。未上页且本批没有对应上页的星级留下。
    /// </summary>
    public static IReadOnlyList<string> KeysWritten(IntentDocument document)
    {
        var ingestRelSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in document.Items)
        {
            if (item.Intent != MediaIntentCodes.StageIngest)
            {
                continue;
            }

            AddRel(ingestRelSet, item.SourceStageRel);
            AddRel(ingestRelSet, item.StageRel);
        }

        var keyList = new List<string>();
        foreach (var item in document.Items)
        {
            if (item.Intent != MediaIntentCodes.StarsUpdate || item.Stars is < 0 or > 5)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.Object))
            {
                if (string.IsNullOrWhiteSpace(item.Channel) || string.IsNullOrWhiteSpace(item.WorkId))
                {
                    continue;
                }

                AddKey(keyList, KeyOf(item));
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.StageRel))
            {
                continue;
            }

            if (item.Stars == 0 || ingestRelSet.Contains(JsonUtil.ToRel(item.StageRel)))
            {
                AddKey(keyList, KeyOf(item));
            }
        }

        return keyList;
    }

    private static void AddRel(HashSet<string> relSet, string? rel)
    {
        if (!string.IsNullOrWhiteSpace(rel))
        {
            relSet.Add(JsonUtil.ToRel(rel));
        }
    }

    private static void AddKey(List<string> keyList, string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            keyList.Add(key);
        }
    }

    private static WorkCatalogItem? FindWork(WorkspaceSession session, string objectKey)
    {
        return session.Works.FirstOrDefault(work =>
            !work.IsUnregistered
            && work.Media.Any(media =>
                string.Equals(JsonUtil.ToRel(media.Src ?? ""), objectKey, StringComparison.Ordinal)));
    }
}
