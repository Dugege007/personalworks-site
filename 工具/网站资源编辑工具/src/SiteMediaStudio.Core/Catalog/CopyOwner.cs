namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 由投放箱或站点卡片解析所属已入编作品。
/// </summary>
public static class CopyOwner
{
    /// <summary>
    /// 站点卡用 <c>WorkId</c>；投放箱卡按认领范围匹配，匹配不到则空，不回落到栏目第一项。
    /// </summary>
    public static string? ResolveWorkId(WorkspaceSession? session, StageItem? stage, SiteItem? site)
    {
        if (site != null && !string.IsNullOrWhiteSpace(site.WorkId))
        {
            return site.WorkId;
        }

        if (session == null || stage == null)
        {
            return null;
        }

        var candidateList = session.Works
            .Where(work => !work.IsUnregistered
                && string.Equals(work.Channel, stage.ChannelKey, StringComparison.Ordinal)
                && StageWorkScope.BelongsToWork(session, stage, work.Id))
            .ToList();
        if (candidateList.Count == 0)
        {
            return null;
        }

        var guessed = candidateList.FirstOrDefault(work =>
            string.Equals(work.Id, stage.WorkIdGuess, StringComparison.Ordinal));
        return guessed?.Id ?? (candidateList.Count == 1 ? candidateList[0].Id : null);
    }

    /// <summary>
    /// 形象栏目不出现名称栏与描述栏。
    /// </summary>
    public static bool HidesCopyName(string? channelKey)
    {
        return string.Equals(channelKey, "profile", StringComparison.Ordinal);
    }

    /// <summary>
    /// 形象与人像摄影作品不出现描述栏。
    /// </summary>
    public static bool HidesCopyDescription(string? channelKey)
    {
        return string.Equals(channelKey, "profile", StringComparison.Ordinal)
            || string.Equals(channelKey, "portrait-photo", StringComparison.Ordinal);
    }
}
