namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把本机脏草稿编成 <c>copy.update</c> 意图条。
/// </summary>
public static class CopyDraftCompiler
{
    //TODO: 对话 CLI 导出 copy.update
    /// <summary>
    /// 工作区匹配的草稿条转意图；不匹配或空则无条。
    /// </summary>
    public static IReadOnlyList<IntentItem> ToItems(WorkspaceSession session, CopyDraftFile? file)
    {
        if (session == null || file == null || !CopyDraftStore.MatchesWorkspace(file, session.Profile))
        {
            return Array.Empty<IntentItem>();
        }

        var itemList = new List<IntentItem>();
        foreach (var entry in file.Entries)
        {
            if (!TryParseKey(entry.Key, out var target, out var channel, out var workId, out var objectKey))
            {
                continue;
            }

            var work = FindWork(session, channel, workId);
            var media = FindMedia(work, objectKey);
            itemList.Add(new IntentItem
            {
                Intent = MediaIntentCodes.CopyUpdate,
                Target = target,
                Channel = channel,
                WorkId = target == "channel" ? null : workId,
                Object = target == "media" ? objectKey : null,
                Title = target == "media" ? entry.Title ?? "" : null,
                Description = entry.Description ?? "",
                LabelBefore = media?.Label
            });
        }

        return itemList;
    }

    /// <summary>
    /// 由意图条还原草稿键，供执行后删齐。
    /// </summary>
    public static string? KeyOf(IntentItem item)
    {
        var target = item.Target ?? "";
        var channel = item.Channel ?? "";
        if (string.Equals(target, "channel", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(channel))
        {
            return CopyDraftStore.ChannelKey(channel);
        }

        if (string.Equals(target, "work", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(channel)
            && !string.IsNullOrWhiteSpace(item.WorkId))
        {
            return CopyDraftStore.WorkKey(channel, item.WorkId);
        }

        if (string.Equals(target, "media", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(channel)
            && !string.IsNullOrWhiteSpace(item.WorkId)
            && !string.IsNullOrWhiteSpace(item.Object))
        {
            return CopyDraftStore.MediaKey(channel, item.WorkId, item.Object);
        }

        return null;
    }

    /// <summary>
    /// 解析 <c>channel:</c> / <c>work:</c> / <c>media:</c> 键。
    /// </summary>
    public static bool TryParseKey(
        string? key,
        out string target,
        out string channel,
        out string workId,
        out string objectKey)
    {
        target = "";
        channel = "";
        workId = "";
        objectKey = "";
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (key.StartsWith("channel:", StringComparison.Ordinal))
        {
            channel = key["channel:".Length..];
            if (string.IsNullOrWhiteSpace(channel))
            {
                return false;
            }

            target = "channel";
            return true;
        }

        if (key.StartsWith("work:", StringComparison.Ordinal))
        {
            var body = key["work:".Length..];
            var split = body.IndexOf(':');
            if (split <= 0 || split >= body.Length - 1)
            {
                return false;
            }

            target = "work";
            channel = body[..split];
            workId = body[(split + 1)..];
            return true;
        }

        if (key.StartsWith("media:", StringComparison.Ordinal))
        {
            var body = key["media:".Length..];
            var first = body.IndexOf(':');
            var second = first < 0 ? -1 : body.IndexOf(':', first + 1);
            if (first <= 0 || second <= first || second >= body.Length - 1)
            {
                return false;
            }

            target = "media";
            channel = body[..first];
            workId = body[(first + 1)..second];
            objectKey = JsonUtil.ToRel(body[(second + 1)..]);
            return true;
        }

        return false;
    }

    private static WorkCatalogItem? FindWork(WorkspaceSession session, string channel, string workId)
    {
        if (string.IsNullOrWhiteSpace(workId))
        {
            return null;
        }

        return session.Works.FirstOrDefault(item =>
            !item.IsUnregistered
            && string.Equals(item.Id, workId, StringComparison.Ordinal)
            && string.Equals(item.Channel, channel, StringComparison.Ordinal));
    }

    private static WorkMediaItem? FindMedia(WorkCatalogItem? work, string objectKey)
    {
        if (work == null || string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        return work.Media.FirstOrDefault(item =>
            string.Equals(item.Src, objectKey, StringComparison.Ordinal));
    }
}
