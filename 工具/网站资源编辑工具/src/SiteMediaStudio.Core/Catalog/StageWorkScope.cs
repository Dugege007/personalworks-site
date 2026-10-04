namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 作品节点应收下哪些投放箱条目：id 猜测、对象键，以及该作品已认领的文件夹。
/// </summary>
public static class StageWorkScope
{
    /// <summary>
    /// 未指定作品时收入栏目全部；指定后只收该作品自己的对象与批次，邻作共用父夹也不串。
    /// </summary>
    public static bool BelongsToWork(WorkspaceSession? session, StageItem item, string? workId)
    {
        if (string.IsNullOrWhiteSpace(workId))
        {
            return true;
        }

        if (session != null && ItemOwnedByOtherWork(session, item, workId))
        {
            return false;
        }

        if (session != null && string.Equals(item.ChannelKey, "profile", StringComparison.Ordinal))
        {
            return ProfileSessionOwnsItem(session, item, workId);
        }

        if (string.Equals(item.WorkIdGuess, workId, StringComparison.Ordinal))
        {
            return true;
        }

        if (session == null)
        {
            return false;
        }

        if (HasMatchedObjectForWork(session, item, workId))
        {
            return true;
        }

        if (ChannelRootClaimedByWork(session, item, workId))
        {
            return true;
        }

        return FolderClaimedByWork(session, item, workId);
    }

    /// <summary>
    /// 当前栏目里可能属于该作品的投放箱条目。仍须再经 <see cref="BelongsToWork"/> 裁定，只是不再对邻作做平方扫描。
    /// </summary>
    public static IEnumerable<StageItem> CandidatesForWork(
        WorkspaceSession session,
        string? channelKey,
        string? workId)
    {
        var channelItems = session.StageItems.Where(item =>
            string.IsNullOrWhiteSpace(channelKey)
            || string.Equals(item.ChannelKey, channelKey, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(workId))
        {
            return channelItems;
        }

        var folderKeys = BuildFolderClaimKeys(session, channelKey, workId);
        var objectKeys = new HashSet<string>(
            session.SiteItems
                .Where(site =>
                    site.WorkId == workId
                    && (string.IsNullOrWhiteSpace(channelKey)
                        || string.Equals(site.ChannelKey, channelKey, StringComparison.Ordinal))
                    && !string.IsNullOrWhiteSpace(site.ObjectKey))
                .Select(site => site.ObjectKey!),
            StringComparer.Ordinal);
        var work = session.Works.FirstOrDefault(candidate =>
            candidate.Id == workId
            && (string.IsNullOrWhiteSpace(channelKey) || candidate.Channel == channelKey));
        return channelItems.Where(item => CouldBelongToWork(item, workId, work, folderKeys, objectKeys));
    }

    /// <summary>
    /// 作品 id、题名、登记夹与已入编路径合成的夹名键，供粗筛。
    /// </summary>
    public static HashSet<string> BuildFolderClaimKeys(
        WorkspaceSession session,
        string? channelKey,
        string workId)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        AddFolderKey(keys, workId);
        var work = session.Works.FirstOrDefault(candidate =>
            candidate.Id == workId
            && (string.IsNullOrWhiteSpace(channelKey) || candidate.Channel == channelKey));
        if (work != null)
        {
            AddFolderKey(keys, work.Id);
            AddFolderKey(keys, work.Title);
            if (!string.IsNullOrWhiteSpace(work.StageFolder))
            {
                AddFolderKey(keys, WorkRegisterRules.FolderIdFromStageFolder(work.StageFolder));
                var channel = StageWorkFolder.FindChannel(session.Profile, work.Channel);
                if (TryReadFolderFromStageRel(work.StageFolder, channel, out var folderId, out var folderGuess))
                {
                    AddFolderKey(keys, folderId);
                    AddFolderKey(keys, folderGuess);
                }
            }
        }

        var itemChannel = StageWorkFolder.FindChannel(session.Profile, channelKey ?? work?.Channel);
        foreach (var site in session.SiteItems)
        {
            if (site.WorkId != workId
                || (!string.IsNullOrWhiteSpace(channelKey)
                    && !string.Equals(site.ChannelKey, channelKey, StringComparison.Ordinal)))
            {
                continue;
            }

            if (TryReadFolderFromStageRel(site.StageRel, itemChannel, out var folderId, out var folderGuess))
            {
                AddFolderKey(keys, folderId);
                AddFolderKey(keys, folderGuess);
            }
        }

        foreach (var record in session.LedgerDict.Values)
        {
            if (string.IsNullOrWhiteSpace(record.StageRel) || string.IsNullOrWhiteSpace(record.Object))
            {
                continue;
            }

            var owner = session.SiteItems.FirstOrDefault(site =>
                site.WorkId == workId
                && string.Equals(site.ObjectKey, record.Object, StringComparison.Ordinal));
            if (owner == null)
            {
                continue;
            }

            if (TryReadFolderFromStageRel(record.StageRel, itemChannel, out var folderId, out var folderGuess))
            {
                AddFolderKey(keys, folderId);
                AddFolderKey(keys, folderGuess);
            }

            if (TryReadFolderFromStageRel(record.SourceStageRel, itemChannel, out folderId, out folderGuess))
            {
                AddFolderKey(keys, folderId);
                AddFolderKey(keys, folderGuess);
            }
        }

        return keys;
    }

    /// <summary>
    /// 粗筛：夹名、对象键或空猜测可能落入该作品。不能代替 <see cref="BelongsToWork"/>。
    /// </summary>
    public static bool CouldBelongToWork(
        StageItem item,
        string workId,
        WorkCatalogItem? work,
        ISet<string> folderKeys,
        ISet<string> objectKeys)
    {
        if (string.Equals(item.WorkIdGuess, workId, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(item.WorkIdGuess) && string.IsNullOrWhiteSpace(item.StageFolderGuess))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item.MatchedObject) && objectKeys.Contains(item.MatchedObject))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item.WorkIdGuess) && folderKeys.Contains(item.WorkIdGuess))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item.StageFolderGuess) && folderKeys.Contains(item.StageFolderGuess))
        {
            return true;
        }

        var folderId = StageFolderClaim.FolderIdOf(item);
        if (!string.IsNullOrWhiteSpace(folderId) && folderKeys.Contains(folderId))
        {
            return true;
        }

        return work != null
            && (StageFolderClaim.TitleMatchesFolder(work.Title, item.WorkIdGuess)
                || StageFolderClaim.TitleMatchesFolder(work.Id, item.WorkIdGuess)
                || StageFolderClaim.TitleMatchesFolder(work.Title, folderId)
                || StageFolderClaim.TitleMatchesFolder(work.Id, folderId));
    }

    /// <summary>
    /// 收入夹名、去日期后的剩余段，以及路径最后一段。
    /// </summary>
    private static void AddFolderKey(HashSet<string> keys, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        keys.Add(value);
        var stripped = StageFolderClaim.StripLeadingDate(value);
        if (!string.IsNullOrWhiteSpace(stripped))
        {
            keys.Add(stripped);
        }

        var slash = value.LastIndexOf('/');
        if (slash < 0)
        {
            return;
        }

        AddFolderKey(keys, value[(slash + 1)..]);
    }

    /// <summary>
    /// 从台账或站点 <c>stageRel</c> 取出作品夹名与栏目内相对夹。
    /// </summary>
    public static bool TryReadFolderFromStageRel(string? stageRel, out string folderId, out string folderGuess)
    {
        return TryReadFolderFromStageRel(stageRel, null, out folderId, out folderGuess);
    }

    /// <summary>
    /// 按栏目容器规则从 <c>stageRel</c> 取作品夹；批次夹与成片目录不作为作品。
    /// </summary>
    public static bool TryReadFolderFromStageRel(
        string? stageRel,
        ChannelProfile? channel,
        out string folderId,
        out string folderGuess)
    {
        folderId = "";
        folderGuess = "";
        return StageWorkFolder.TryReadFromStageRel(
                stageRel,
                channel,
                out folderId,
                out folderGuess,
                out var channelRoot)
            && !channelRoot
            && !string.IsNullOrWhiteSpace(folderId);
    }

    /// <summary>
    /// 形象照只按场次夹认领，不把邻夹原片或扁平对象键串进当前场次。
    /// </summary>
    private static bool ProfileSessionOwnsItem(WorkspaceSession session, StageItem item, string workId)
    {
        var work = session.Works.FirstOrDefault(candidate =>
            candidate.Id == workId
            && candidate.Channel == "profile"
            && !candidate.IsUnregistered);
        if (work == null)
        {
            return false;
        }

        if (FolderMatchesWorkFolder(item, work)
            || StageFolderClaim.TitleMatchesFolder(work.Title, item.WorkIdGuess)
            || StageFolderClaim.TitleMatchesFolder(work.Id, item.WorkIdGuess)
            || string.Equals(item.WorkIdGuess, work.Id, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item.WorkIdGuess) || !string.IsNullOrWhiteSpace(item.StageFolderGuess))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(work.StageFolder)
            && session.Works.Count(candidate =>
                candidate.Channel == "profile" && !candidate.IsUnregistered) == 1;
    }

    /// <summary>
    /// 台账对象键已挂到别的作品时，不再收入当前作品。
    /// </summary>
    private static bool ItemOwnedByOtherWork(WorkspaceSession session, StageItem item, string workId)
    {
        if (string.IsNullOrWhiteSpace(item.MatchedObject))
        {
            return false;
        }

        var owner = session.SiteItems.FirstOrDefault(site =>
            string.Equals(site.ObjectKey, item.MatchedObject, StringComparison.Ordinal));
        return owner != null && owner.WorkId != workId;
    }

    /// <summary>
    /// 同一作品夹下是否已有两条以上已入编作品（如富康城 15# / 3#）。
    /// </summary>
    private static bool ParentFolderHasMultipleWorks(WorkspaceSession session, StageItem item)
    {
        var ownerSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var site in session.SiteItems)
        {
            if (site.ChannelKey != item.ChannelKey
                || !StageRelMatchesItemFolder(session, item, site.StageRel))
            {
                continue;
            }

            ownerSet.Add(site.WorkId);
        }

        foreach (var record in session.LedgerDict.Values)
        {
            if (string.IsNullOrWhiteSpace(record.StageRel)
                || string.IsNullOrWhiteSpace(record.Object)
                || !StageRelMatchesItemFolder(session, item, record.StageRel))
            {
                continue;
            }

            var owner = session.SiteItems.FirstOrDefault(site =>
                string.Equals(site.ObjectKey, record.Object, StringComparison.Ordinal));
            if (owner != null)
            {
                ownerSet.Add(owner.WorkId);
            }
        }

        return ownerSet.Count > 1;
    }

    /// <summary>
    /// 共用作品夹时按批次夹或已配对对象认领，不把邻批收入当前作品。
    /// </summary>
    private static bool BatchBelongsToWork(WorkspaceSession session, StageItem item, string workId)
    {
        if (HasMatchedObjectForWork(session, item, workId))
        {
            return true;
        }

        var channel = StageWorkFolder.FindChannel(session.Profile, item.ChannelKey);
        if (!StageWorkFolder.TryReadBatchFolder(item.StageRel, channel, out var itemBatch))
        {
            return false;
        }

        foreach (var site in session.SiteItems)
        {
            if (site.WorkId != workId
                || !StageWorkFolder.TryReadBatchFolder(site.StageRel, channel, out var siteBatch)
                || !string.Equals(siteBatch, itemBatch, StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        foreach (var record in session.LedgerDict.Values)
        {
            var owner = session.SiteItems.FirstOrDefault(site =>
                site.WorkId == workId
                && string.Equals(site.ObjectKey, record.Object, StringComparison.Ordinal));
            if (owner == null)
            {
                continue;
            }

            if (StageWorkFolder.TryReadBatchFolder(record.StageRel, channel, out var ledgerBatch)
                && string.Equals(ledgerBatch, itemBatch, StringComparison.Ordinal))
            {
                return true;
            }

            if (StageWorkFolder.TryReadBatchFolder(record.SourceStageRel, channel, out var sourceBatch)
                && string.Equals(sourceBatch, itemBatch, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (var sibling in session.StageItems)
        {
            if (!HasMatchedObjectForWork(session, sibling, workId)
                || !StageWorkFolder.TryReadBatchFolder(sibling.StageRel, channel, out var siblingBatch)
                || !string.Equals(siblingBatch, itemBatch, StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 该投放箱条目的对象键是否已挂到指定作品。
    /// </summary>
    private static bool HasMatchedObjectForWork(WorkspaceSession session, StageItem item, string workId)
    {
        return !string.IsNullOrWhiteSpace(item.MatchedObject)
            && session.SiteItems.Any(site =>
                site.WorkId == workId
                && string.Equals(site.ObjectKey, item.MatchedObject, StringComparison.Ordinal));
    }

    /// <summary>
    /// 作品 <c>stageFolder</c>、已配对同夹文件或台账路径是否已认领该夹。
    /// </summary>
    private static bool FolderClaimedByWork(WorkspaceSession session, StageItem item, string workId)
    {
        if (string.IsNullOrWhiteSpace(item.WorkIdGuess) && string.IsNullOrWhiteSpace(item.StageFolderGuess))
        {
            return false;
        }

        var work = session.Works.FirstOrDefault(candidate =>
            candidate.Id == workId
            && (string.IsNullOrWhiteSpace(item.ChannelKey) || candidate.Channel == item.ChannelKey));
        if (work != null && FolderMatchesWorkFolder(item, work))
        {
            return true;
        }

        var folderId = StageFolderClaim.FolderIdOf(item);
        if (work != null
            && StageFolderClaim.TryClaim(
                session.Works,
                item.ChannelKey,
                folderId,
                session.Profile,
                out var claimed)
            && claimed != null
            && claimed.Id == workId)
        {
            return true;
        }

        if (ParentFolderHasMultipleWorks(session, item))
        {
            return BatchBelongsToWork(session, item, workId);
        }

        foreach (var sibling in session.StageItems)
        {
            if (!string.Equals(sibling.ChannelKey, item.ChannelKey, StringComparison.Ordinal)
                || !HasMatchedObjectForWork(session, sibling, workId))
            {
                continue;
            }

            if (SameClaimedFolder(item, sibling.WorkIdGuess, sibling.StageFolderGuess))
            {
                return true;
            }
        }

        foreach (var site in session.SiteItems)
        {
            if (site.WorkId != workId || !StageRelMatchesItemFolder(session, item, site.StageRel))
            {
                continue;
            }

            return true;
        }

        if (work == null)
        {
            return false;
        }

        foreach (var record in session.LedgerDict.Values)
        {
            if (string.IsNullOrWhiteSpace(record.StageRel)
                || string.IsNullOrWhiteSpace(record.Object)
                || !ObjectBelongsToWork(record.Object, work, session)
                || !StageRelMatchesItemFolder(session, item, record.StageRel))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 投放箱猜测是否落在作品登记的 <c>stageFolder</c>。
    /// </summary>
    private static bool FolderMatchesWorkFolder(StageItem item, WorkCatalogItem work)
    {
        if (string.IsNullOrWhiteSpace(work.StageFolder))
        {
            return false;
        }

        var folderId = WorkRegisterRules.FolderIdFromStageFolder(work.StageFolder);
        if (!string.IsNullOrWhiteSpace(folderId)
            && string.Equals(item.WorkIdGuess, folderId, StringComparison.Ordinal))
        {
            return true;
        }

        var rest = RestAfterChannel(work.StageFolder, work.Channel);
        return !string.IsNullOrWhiteSpace(rest)
            && string.Equals(item.StageFolderGuess, rest, StringComparison.Ordinal);
    }

    /// <summary>
    /// 两条路径是否指向同一作品夹。
    /// </summary>
    private static bool SameClaimedFolder(StageItem item, string? folderId, string? stageFolderGuess)
    {
        if (!string.IsNullOrWhiteSpace(item.StageFolderGuess)
            && !string.IsNullOrWhiteSpace(stageFolderGuess)
            && string.Equals(item.StageFolderGuess, stageFolderGuess, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(item.WorkIdGuess)
            && !string.IsNullOrWhiteSpace(folderId)
            && string.Equals(item.WorkIdGuess, folderId, StringComparison.Ordinal);
    }

    /// <summary>
    /// 台账路径是否落在该投放箱条目的作品夹。
    /// </summary>
    private static bool StageRelMatchesItemFolder(WorkspaceSession session, StageItem item, string? stageRel)
    {
        var channel = StageWorkFolder.FindChannel(session.Profile, item.ChannelKey);
        return TryReadFolderFromStageRel(stageRel, channel, out var folderId, out var folderGuess)
            && SameClaimedFolder(item, folderId, folderGuess);
    }

    /// <summary>
    /// 栏目根文件：扁平栏目或该栏仅一条已入编作品时，收入该作品。
    /// </summary>
    private static bool ChannelRootClaimedByWork(WorkspaceSession session, StageItem item, string workId)
    {
        if (!string.IsNullOrWhiteSpace(item.WorkIdGuess) || !string.IsNullOrWhiteSpace(item.StageFolderGuess))
        {
            return false;
        }

        var work = session.Works.FirstOrDefault(candidate =>
            candidate.Id == workId
            && !candidate.IsUnregistered
            && (string.IsNullOrWhiteSpace(item.ChannelKey) || candidate.Channel == item.ChannelKey));
        if (work == null)
        {
            return false;
        }

        var channel = StageWorkFolder.FindChannel(session.Profile, work.Channel);
        if (StageWorkFolder.IsFlatFile(channel))
        {
            return true;
        }

        return session.Works.Count(candidate =>
            candidate.Channel == work.Channel && !candidate.IsUnregistered) == 1;
    }

    /// <summary>
    /// 去掉栏目段后的作品夹相对路径。
    /// </summary>
    private static string RestAfterChannel(string stageFolder, string channel)
    {
        var normalized = JsonUtil.ToRel(stageFolder);
        var prefix = channel + "/";
        if (normalized.StartsWith(prefix, StringComparison.Ordinal))
        {
            return normalized[prefix.Length..];
        }

        var slash = normalized.IndexOf('/');
        return slash < 0 ? normalized : normalized[(slash + 1)..];
    }

    /// <summary>
    /// 对象键是否按栏目 / 作品 id 前缀归属该作品；扁平对象键归入该栏唯一作品。
    /// </summary>
    private static bool ObjectBelongsToWork(
        string objectKey,
        WorkCatalogItem work,
        WorkspaceSession? session = null)
    {
        var normalized = JsonUtil.ToRel(objectKey);
        var prefix = work.Channel + "/" + work.Id + "/";
        if (normalized.StartsWith(prefix, StringComparison.Ordinal)
            || string.Equals(normalized, work.Channel + "/" + work.Id, StringComparison.Ordinal))
        {
            return true;
        }

        var channelPrefix = work.Channel + "/";
        if (!normalized.StartsWith(channelPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = normalized[channelPrefix.Length..];
        if (rest.Contains('/'))
        {
            return false;
        }

        var channel = StageWorkFolder.FindChannel(session?.Profile, work.Channel);
        if (StageWorkFolder.IsFlatFile(channel))
        {
            return true;
        }

        return session != null
            && session.Works.Count(candidate =>
                candidate.Channel == work.Channel && !candidate.IsUnregistered) == 1;
    }
}
