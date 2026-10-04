namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次加载后的只读工作区快照。
/// </summary>
public sealed class WorkspaceSession
{
    public required WorkspaceProfile Profile { get; init; }
    public IReadOnlyList<WorkCatalogItem> Works { get; init; } = Array.Empty<WorkCatalogItem>();
    public IReadOnlyList<StageItem> StageItems { get; init; } = Array.Empty<StageItem>();
    public IReadOnlyList<SiteItem> SiteItems { get; init; } = Array.Empty<SiteItem>();
    public IReadOnlyDictionary<string, LedgerRecord> LedgerDict { get; init; } =
        new Dictionary<string, LedgerRecord>();
    public IReadOnlyDictionary<string, int> ContentRefCountDict { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// 栏目键到已发布导语。
    /// </summary>
    public IReadOnlyDictionary<string, string> ChannelLeadDict { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 读取配置并编目投放箱与站点图像 / 视频。
    /// </summary>
    public static WorkspaceSession Load(string profilePath)
    {
        return Load(profilePath, PendingPublishStore.Load());
    }

    /// <summary>
    /// 读取配置并编目。pending 指定待发布队列；不属于该配置时不排除撤下键。
    /// </summary>
    public static WorkspaceSession Load(string profilePath, PendingPublish? pending)
    {
        var profile = WorkspaceProfileLoader.Load(profilePath);
        var ledgerDict = LedgerReader.LoadDict(profile);
        var withdrawSet = PendingWithdrawSet(profile, pending);
        var stageItems = StageCatalogScanner.Scan(profile, ledgerDict);
        MarkPendingWithdraw(stageItems, withdrawSet);
        var registeredList = ProfileStageSessions.Merge(LoadWorks(profile, ledgerDict), stageItems)
            .Concat(NoteCatalog.Discover(profile, stageItems))
            .ToList();
        var works = registeredList
            .Concat(UnregisteredWorkDiscovery.Discover(registeredList, stageItems, ledgerDict, profile))
            .ToList();
        var refCountDict = BuildRefCountDict(works);
        var placeholdersRoot = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.PlaceholdersRoot);
        var siteItems = BuildSiteItems(
            works,
            ledgerDict,
            refCountDict,
            placeholdersRoot,
            withdrawSet);
        return new WorkspaceSession
        {
            Profile = profile,
            Works = works,
            StageItems = stageItems,
            SiteItems = siteItems,
            LedgerDict = ledgerDict,
            ContentRefCountDict = refCountDict,
            ChannelLeadDict = LoadChannelLeads(profile)
        };
    }

    /// <summary>
    /// 待发布撤下的对象，投放箱不再标已发布。
    /// </summary>
    private static void MarkPendingWithdraw(IReadOnlyList<StageItem> stageItems, IReadOnlySet<string> withdrawSet)
    {
        if (withdrawSet.Count == 0)
        {
            return;
        }

        foreach (var item in stageItems)
        {
            item.IsPendingWithdraw = !string.IsNullOrWhiteSpace(item.MatchedObject)
                && withdrawSet.Contains(JsonUtil.ToRel(item.MatchedObject));
        }
    }

    /// <summary>
    /// 本机已执行撤下、尚未发布的对象键。这些键不进站点页，避免和已隐藏混在一起。
    /// </summary>
    private static HashSet<string> PendingWithdrawSet(WorkspaceProfile profile, PendingPublish? pending)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!PendingPublishStore.BelongsToWorkspace(profile, pending))
        {
            return set;
        }

        foreach (var key in pending!.WithdrawObjectList)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                set.Add(JsonUtil.ToRel(key));
            }
        }

        return set;
    }

    /// <summary>
    /// 按配置选择 JSON 或 PersonalWorks 适配器。
    /// </summary>
    private static IReadOnlyList<WorkCatalogItem> LoadWorks(
        WorkspaceProfile profile,
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict)
    {
        var kind = profile.SiteCatalog.Kind ?? "json";
        var fullPath = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.Path);
        if (string.Equals(kind, "personalworks-ts", StringComparison.OrdinalIgnoreCase))
        {
            var sitePath = string.IsNullOrWhiteSpace(profile.SiteCatalog.SitePath)
                ? null
                : WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.SitePath);
            var workDataPath = string.IsNullOrWhiteSpace(profile.SiteCatalog.WorkDataPath)
                ? null
                : WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.WorkDataPath);
            var gameDataPath = string.IsNullOrWhiteSpace(profile.SiteCatalog.GameDataPath)
                ? null
                : WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.GameDataPath);
            return PersonalWorksSiteCatalogReader.Load(
                fullPath,
                sitePath,
                workDataPath,
                gameDataPath,
                ledgerDict,
                profile);
        }

        return JsonSiteCatalogReader.Load(fullPath);
    }

    /// <summary>
    /// 按编目种类读取栏目导语。
    /// </summary>
    private static IReadOnlyDictionary<string, string> LoadChannelLeads(WorkspaceProfile profile)
    {
        var kind = profile.SiteCatalog.Kind ?? "json";
        if (string.Equals(kind, "personalworks-ts", StringComparison.OrdinalIgnoreCase))
        {
            var sitePath = string.IsNullOrWhiteSpace(profile.SiteCatalog.SitePath)
                ? null
                : WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.SitePath);
            var lexiconPath = Path.Combine(profile.ResolvedRoot, "PersonalSite", "src", "content", "lexicon.ts");
            return ChannelCopyReader.LoadFromSiteTs(lexiconPath, sitePath);
        }

        var catalogPath = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.Path);
        return ChannelCopyReader.LoadFromJsonCatalog(catalogPath);
    }

    /// <summary>
    /// 统计每个对象键被内容层引用的次数。
    /// </summary>
    private static Dictionary<string, int> BuildRefCountDict(IReadOnlyList<WorkCatalogItem> works)
    {
        var refCountDict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var work in works)
        {
            foreach (var media in work.Media)
            {
                if (!string.IsNullOrWhiteSpace(media.Src))
                {
                    refCountDict[media.Src] =
                        refCountDict.GetValueOrDefault(media.Src) + Math.Max(1, media.ReferenceCount);
                }

                if (!string.IsNullOrWhiteSpace(media.Poster))
                {
                    refCountDict[media.Poster] =
                        refCountDict.GetValueOrDefault(media.Poster) + Math.Max(1, media.ReferenceCount);
                }
            }
        }

        return refCountDict;
    }

    /// <summary>
    /// 把编目展开为可预览的站点条目。
    /// </summary>
    private static List<SiteItem> BuildSiteItems(
        IReadOnlyList<WorkCatalogItem> works,
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict,
        IReadOnlyDictionary<string, int> refCountDict,
        string placeholdersRoot,
        IReadOnlySet<string> pendingWithdrawSet)
    {
        var siteList = new List<SiteItem>();
        var onPageSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in works)
        {
            var withSrcList = work.Media.Where(item => !string.IsNullOrWhiteSpace(item.Src)).ToList();
            foreach (var media in withSrcList)
            {
                onPageSet.Add(media.Src!);
            }
        }

        foreach (var work in works)
        {
            var withSrcList = work.Media
                .Where(item => !string.IsNullOrWhiteSpace(item.Src) && !VideoEncodeRules.IsPosterObject(item.Src))
                .ToList();
            var hiddenRecordList = ledgerDict.Values
                .Where(record => !VideoEncodeRules.IsPosterObject(record.Object)
                    && !pendingWithdrawSet.Contains(JsonUtil.ToRel(record.Object))
                    && IsHiddenLeftover(record, work, works, onPageSet, refCountDict))
                .OrderBy(record => record.Object, StringComparer.Ordinal)
                .ToList();
            var total = withSrcList.Count + hiddenRecordList.Count;
            for (var i = 0; i < withSrcList.Count; i++)
            {
                var media = withSrcList[i];
                var objectKey = media.Src!;
                ledgerDict.TryGetValue(objectKey, out var ledger);
                siteList.Add(CreateSiteItem(
                    work,
                    objectKey,
                    string.IsNullOrWhiteSpace(media.Label) ? objectKey : media.Label,
                    i,
                    total,
                    ledger,
                    refCountDict.GetValueOrDefault(objectKey),
                    placeholdersRoot,
                    hidden: work.IsHidden,
                    media.DisplayName,
                    media.Description,
                    media.Stars ?? 0));
            }

            for (var i = 0; i < hiddenRecordList.Count; i++)
            {
                var record = hiddenRecordList[i];
                var objectKey = JsonUtil.ToRel(record.Object);
                work.HiddenObjectSet.Add(objectKey);
                var stars = work.ParkedStarDict.TryGetValue(objectKey, out var parked) ? parked : 0;
                siteList.Add(CreateSiteItem(
                    work,
                    record.Object,
                    Path.GetFileNameWithoutExtension(record.Object),
                    withSrcList.Count + i,
                    total,
                    record,
                    0,
                    placeholdersRoot,
                    hidden: true,
                    displayName: null,
                    description: null,
                    stars: stars));
            }
        }

        return siteList;
    }

    /// <summary>
    /// 台账仍为 published、内容层已无引用，并归属该作品。
    /// </summary>
    private static bool IsHiddenLeftover(
        LedgerRecord record,
        WorkCatalogItem work,
        IReadOnlyList<WorkCatalogItem> works,
        ISet<string> onPageSet,
        IReadOnlyDictionary<string, int> refCountDict)
    {
        if (!string.Equals(record.Status, "published", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.Object) || MediaPathRules.IsStock(record.Object))
        {
            return false;
        }

        if (onPageSet.Contains(record.Object) || refCountDict.GetValueOrDefault(record.Object) > 0)
        {
            return false;
        }

        return BelongsToWork(record.Object, work, works, record);
    }

    /// <summary>
    /// 对象键按作品目录前缀归属。摄影为 photo/{栏目}/{作品}/，与分配对象键同一规则。扁平栏目仅当该栏只有一条作品时归入。形象照按台账场次夹归入对应场次。
    /// </summary>
    private static bool BelongsToWork(
        string objectKey,
        WorkCatalogItem work,
        IReadOnlyList<WorkCatalogItem> works,
        LedgerRecord? record = null)
    {
        var numberedPrefix = ObjectKeyAllocator.WorkDirectoryPrefix(work.Channel, work.Id);
        if (objectKey.StartsWith(numberedPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        var channelPrefix = work.Channel + "/";
        if (!objectKey.StartsWith(channelPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = objectKey[channelPrefix.Length..];
        if (rest.Contains('/'))
        {
            return false;
        }

        if (work.Channel == "profile"
            && record != null
            && StageWorkScope.TryReadFolderFromStageRel(record.StageRel, out var folderId, out _))
        {
            return StageFolderClaim.TitleMatchesFolder(work.Title, folderId)
                || StageFolderClaim.TitleMatchesFolder(work.Id, folderId)
                || string.Equals(
                    WorkRegisterRules.FolderIdFromStageFolder(work.StageFolder),
                    folderId,
                    StringComparison.Ordinal);
        }

        return works.Count(item => item.Channel == work.Channel) == 1;
    }

    /// <summary>
    /// 组装一条站点卡片数据。
    /// </summary>
    private static SiteItem CreateSiteItem(
        WorkCatalogItem work,
        string objectKey,
        string label,
        int index,
        int total,
        LedgerRecord? ledger,
        int referenceCount,
        string placeholdersRoot,
        bool hidden,
        string? displayName,
        string? description,
        int stars)
    {
        var diskPath = Path.Combine(placeholdersRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        return new SiteItem
        {
            WorkId = work.Id,
            ChannelKey = work.Channel,
            WorkTitle = work.Title,
            WorkSummary = work.Summary,
            Label = label,
            DisplayName = displayName,
            Description = description,
            Stars = stars,
            ObjectKey = objectKey,
            Index = index,
            Total = total,
            StageRel = ledger?.StageRel,
            SourceStageRel = ledger?.SourceStageRel,
            FullPath = File.Exists(diskPath) ? diskPath : null,
            LedgerStatus = ledger?.Status,
            ReferenceCount = referenceCount,
            IsStock = MediaPathRules.IsStock(objectKey),
            IsHidden = hidden
        };
    }
}
