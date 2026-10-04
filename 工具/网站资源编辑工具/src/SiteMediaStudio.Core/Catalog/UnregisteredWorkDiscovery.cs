namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 从投放箱作品夹归纳尚未写入内容层的作品节点。
/// </summary>
public static class UnregisteredWorkDiscovery
{
    /// <summary>
    /// 栏目下存在图像、且未被已入编作品认领的作品夹视为未登记。容器夹、stock、无文件夹的文件与形象照场次夹排除。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Discover(
        IReadOnlyList<WorkCatalogItem> registeredList,
        IReadOnlyList<StageItem> stageList,
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict = null,
        WorkspaceProfile? profile = null)
    {
        var knownSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in registeredList)
        {
            knownSet.Add(WorkKey(work.Channel, work.Id));
            var folderId = WorkRegisterRules.FolderIdFromStageFolder(work.StageFolder);
            if (!string.IsNullOrWhiteSpace(folderId))
            {
                knownSet.Add(WorkKey(work.Channel, folderId));
            }
        }

        foreach (var item in stageList)
        {
            if (string.IsNullOrWhiteSpace(item.WorkIdGuess)
                || string.IsNullOrWhiteSpace(item.MatchedObject)
                || !RegisteredWorkClaimsObject(registeredList, item.ChannelKey, item.MatchedObject, profile))
            {
                continue;
            }

            knownSet.Add(WorkKey(item.ChannelKey, item.WorkIdGuess));
        }

        ClaimFoldersFromLedger(registeredList, ledgerDict, knownSet, profile);
        ClaimFoldersByTitleOrSoleWork(registeredList, stageList, knownSet, profile);

        var folderDict = new SortedDictionary<string, (string Channel, string Id, string StageFolder)>(
            StringComparer.Ordinal);
        foreach (var item in stageList)
        {
            if (!TryReadFolder(item, out var channel, out var folderId))
            {
                continue;
            }

            var key = WorkKey(channel, folderId);
            if (knownSet.Contains(key) || folderDict.ContainsKey(key))
            {
                continue;
            }

            var stageFolder = string.IsNullOrWhiteSpace(item.StageFolderGuess)
                ? WorkRegisterRules.StageFolder(channel, folderId)
                : JsonUtil.ToRel(channel + "/" + item.StageFolderGuess);
            folderDict[key] = (channel, folderId, stageFolder);
        }

        return folderDict.Values
            .Select(item => new WorkCatalogItem
            {
                Id = item.Id,
                Channel = item.Channel,
                Title = WorkRegisterRules.SeedTitle(item.Id),
                SourceKind = "unregistered",
                IsUnregistered = true,
                SupportsAppend = false,
                StageFolder = item.StageFolder,
                Media = Array.Empty<WorkMediaItem>()
            })
            .ToList();
    }

    /// <summary>
    /// 已入编作品，按栏目过滤。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> RegisteredInChannel(
        IEnumerable<WorkCatalogItem> workList,
        string channelKey)
    {
        return workList
            .Where(item => item.Channel == channelKey && !item.IsUnregistered)
            .ToList();
    }

    /// <summary>
    /// 未登记作品，按栏目过滤。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> UnregisteredInChannel(
        IEnumerable<WorkCatalogItem> workList,
        string channelKey)
    {
        return workList
            .Where(item => item.Channel == channelKey && item.IsUnregistered)
            .ToList();
    }

    /// <summary>
    /// 目标作品是否为未登记空壳。
    /// </summary>
    public static bool IsUnregisteredTarget(string? workId, string? channelKey, IEnumerable<WorkCatalogItem>? workList)
    {
        if (string.IsNullOrWhiteSpace(workId) || workList == null)
        {
            return false;
        }

        return workList.Any(item =>
            item.IsUnregistered
            && item.Id == workId
            && (string.IsNullOrWhiteSpace(channelKey) || item.Channel == channelKey));
    }

    /// <summary>
    /// 投放箱文件夹猜测是否对应未登记节点。
    /// </summary>
    public static bool IsUnregisteredGuess(StageItem item, IEnumerable<WorkCatalogItem>? workList)
    {
        return !string.IsNullOrWhiteSpace(item.WorkIdGuess)
            && IsUnregisteredTarget(item.WorkIdGuess, item.ChannelKey, workList);
    }

    /// <summary>
    /// 栏目与作品 id 组成去重键。
    /// </summary>
    private static string WorkKey(string channel, string workId)
    {
        return channel + "\n" + workId;
    }

    /// <summary>
    /// 读取可作为未登记作品的作品夹名。
    /// </summary>
    private static bool TryReadFolder(StageItem item, out string channel, out string folderId)
    {
        channel = item.ChannelKey;
        folderId = item.WorkIdGuess ?? "";
        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(folderId))
        {
            return false;
        }

        if (item.IsStock || MediaPathRules.IsStock(folderId))
        {
            return false;
        }

        if (folderId.StartsWith('.'))
        {
            return false;
        }

        // 形象照子夹只是中转站手分，一律属栏目，不进未登记。
        if (string.Equals(channel, "profile", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 台账或同名对象键已指向某已入编作品时，该夹视为已认领。
    /// </summary>
    private static bool RegisteredWorkClaimsObject(
        IReadOnlyList<WorkCatalogItem> registeredList,
        string channel,
        string objectKey,
        WorkspaceProfile? profile = null)
    {
        var normalized = JsonUtil.ToRel(objectKey);
        var channelProfile = StageWorkFolder.FindChannel(profile, channel);
        var soleWork = registeredList.Count(item => item.Channel == channel) == 1;
        foreach (var work in registeredList)
        {
            if (work.Channel != channel)
            {
                continue;
            }

            var prefix = work.Channel + "/" + work.Id + "/";
            if (normalized.StartsWith(prefix, StringComparison.Ordinal)
                || string.Equals(normalized, work.Channel + "/" + work.Id, StringComparison.Ordinal))
            {
                return true;
            }

            var rest = normalized.StartsWith(channel + "/", StringComparison.Ordinal)
                ? normalized[(channel.Length + 1)..]
                : "";
            if (!rest.Contains('/')
                && (StageWorkFolder.IsFlatFile(channelProfile) || soleWork))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 成片不进编目时，仍按台账 <c>stageRel</c> 认领其作品夹。
    /// </summary>
    private static void ClaimFoldersFromLedger(
        IReadOnlyList<WorkCatalogItem> registeredList,
        IReadOnlyDictionary<string, LedgerRecord>? ledgerDict,
        ISet<string> knownSet,
        WorkspaceProfile? profile)
    {
        if (ledgerDict == null)
        {
            return;
        }

        foreach (var record in ledgerDict.Values)
        {
            if (string.IsNullOrWhiteSpace(record.Object)
                || string.IsNullOrWhiteSpace(record.StageRel)
                || !TryReadChannel(record.Object, out var channel)
                || !RegisteredWorkClaimsObject(registeredList, channel, record.Object, profile)
                || !StageWorkScope.TryReadFolderFromStageRel(
                    record.StageRel,
                    StageWorkFolder.FindChannel(profile, channel),
                    out var folderId,
                    out _))
            {
                continue;
            }

            knownSet.Add(WorkKey(channel, folderId));
        }
    }

    /// <summary>
    /// 夹名去掉日期后等于题名，或扁平栏目仅一条作品时，该夹不再标未登记。
    /// </summary>
    private static void ClaimFoldersByTitleOrSoleWork(
        IReadOnlyList<WorkCatalogItem> registeredList,
        IReadOnlyList<StageItem> stageList,
        ISet<string> knownSet,
        WorkspaceProfile? profile)
    {
        foreach (var item in stageList)
        {
            if (!TryReadFolder(item, out var channel, out var folderId))
            {
                continue;
            }

            var key = WorkKey(channel, folderId);
            if (knownSet.Contains(key))
            {
                continue;
            }

            if (StageFolderClaim.TryClaim(registeredList, channel, folderId, profile, out _))
            {
                knownSet.Add(key);
            }
        }
    }

    /// <summary>
    /// 对象键第一段为栏目键。
    /// </summary>
    private static bool TryReadChannel(string objectKey, out string channel)
    {
        var normalized = JsonUtil.ToRel(objectKey);
        var slash = normalized.IndexOf('/');
        channel = slash > 0 ? normalized[..slash] : "";
        return !string.IsNullOrWhiteSpace(channel);
    }
}
