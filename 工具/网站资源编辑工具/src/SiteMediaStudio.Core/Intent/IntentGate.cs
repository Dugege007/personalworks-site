namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 闸门判定所需的只读上下文。
/// </summary>
public sealed class IntentContext
{
    public required IReadOnlyDictionary<string, LedgerRecord> LedgerDict { get; init; }
    public required IReadOnlyDictionary<string, int> ContentRefCountDict { get; init; }
    public WorkspaceProfile? Profile { get; init; }
    public string? TargetWorkId { get; init; }
    public IReadOnlyList<WorkCatalogItem>? WorkList { get; init; }
    public IReadOnlyDictionary<string, int>? BatchRemoveCountDict { get; init; }
}

/// <summary>
/// 投放箱、站点与登记意图的允许 / 拒绝规则。
/// </summary>
public static class IntentGate
{
    /// <summary>
    /// 判定投放箱条目。
    /// </summary>
    public static GateDecision EvaluateStage(MediaIntent intent, StageItem item, IntentContext context)
    {
        if (intent is not (MediaIntent.StageIngest or MediaIntent.StageRecycle))
        {
            return GateDecision.Reject("投放箱条目不能使用站点意图。");
        }

        if (!StageExists(item))
        {
            return GateDecision.Reject(item.IsPack ? "投放箱包目录不存在。" : "投放箱文件不存在。");
        }

        if (item.IsStock)
        {
            return GateDecision.Reject("stock 常驻位不可上页标记以外的破坏性操作，且默认拒绝回收与撤下。");
        }

        if (intent == MediaIntent.StageIngest && NoteRules.IsNotesChannel(item.ChannelKey))
        {
            return NoteRules.IsBodyStageRel(item.StageRel)
                ? GateDecision.Allow("将上页整篇心得，与右键「上页心得」相同。")
                : GateDecision.Allow("配图是否上页，要看同批有没有标记正文.md。");
        }

        if (intent == MediaIntent.StageRecycle)
        {
            if (IsOnSite(item.MatchedObject, context) || IsPublished(item.MatchedObject, context) || IsPublished(item.LedgerStatus))
            {
                return GateDecision.Reject("已上页或台账仍为 published 的投放箱文件不可回收。");
            }

            if (item.IsStock)
            {
                return GateDecision.Reject("stock 不可回收。");
            }

            return GateDecision.Allow("将移入本机回收站，不经 sitemedia。");
        }

        if (IsRedundantIngest(item, context))
        {
            return GateDecision.Warn(RedundantIngestMessage);
        }

        var capabilities = FindCapabilities(item.ChannelKey, context);
        if (!capabilities.Ingest)
        {
            return GateDecision.Reject("该栏目未声明上页能力。");
        }

        if (CatalogNotice.IsUnsignedConstruction(item))
        {
            return GateDecision.Reject("施工图未脱敏（须 .desense.jpg 或 webp），禁止上页。");
        }

        if (MediaPathRules.IsVideoFile(item.FullPath ?? item.StageRel)
            && CatalogNotice.IsConstructionChannel(item.ChannelKey))
        {
            return GateDecision.Reject("施工图栏目拒绝视频上页。");
        }

        if (item.IsPack)
        {
            if (IsUnregisteredStage(item, context))
            {
                return GateDecision.Reject("须先登记或改选已有作品。");
            }

            return GateDecision.Reject("游戏包上页未开通。");
        }

        var needsPrepare = WebpPrepareRules.NeedsPrepare(item, context.Profile);

        if (IsUnregisteredStage(item, context))
        {
            return GateDecision.Reject("须先登记或改选已有作品。");
        }

        if (!CanAppendToTargetWork(item.ChannelKey, context, MediaPathRules.IsVideoFile(item.FullPath ?? item.StageRel)))
        {
            return GateDecision.Reject("目标作品图像 src 与占位槽混排，本轮不能上页改写。");
        }

        if (string.IsNullOrWhiteSpace(context.TargetWorkId))
        {
            return GateDecision.Reject("上页须先指定目标作品。");
        }

        var missingFacts = MissingPhotoFacts(item, context);
        if (missingFacts != null)
        {
            return GateDecision.Reject(missingFacts);
        }

        if (!string.IsNullOrWhiteSpace(item.StageRel))
        {
            foreach (var record in context.LedgerDict.Values)
            {
                if (!string.Equals(record.Status, "published", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var occupiesPath = string.Equals(record.StageRel, item.StageRel, StringComparison.Ordinal)
                    || string.Equals(record.SourceStageRel, item.StageRel, StringComparison.Ordinal);
                if (!occupiesPath)
                {
                    continue;
                }

                if (!string.Equals(record.Object, item.MatchedObject, StringComparison.Ordinal))
                {
                    return GateDecision.Reject("该投放路径已被其它 published 对象占用。");
                }
            }
        }

        var needsEncode = VideoEncodeRules.NeedsEncode(item, context.Profile);
        return GateDecision.Allow(needsEncode
            ? "将先压成网页 MP4 再上页，原片保留。"
            : needsPrepare
                ? "将先压成网页 WebP 再上页，原片保留。"
                : "将复制入库并写入内容层；中转站文件保留。");
    }

    /// <summary>
    /// 判定站点条目。
    /// </summary>
    public static GateDecision EvaluateSite(MediaIntent intent, SiteItem item, IntentContext context)
    {
        if (intent is not (MediaIntent.SiteHide or MediaIntent.SiteRestore or MediaIntent.SiteWithdraw))
        {
            return GateDecision.Reject("站点条目不能使用投放箱意图。");
        }

        if (intent == MediaIntent.SiteRestore)
        {
            if (string.IsNullOrWhiteSpace(item.ObjectKey))
            {
                return GateDecision.Reject("无对象键的占位槽不能上页。");
            }

            if (IsRedundantRestore(intent, item))
            {
                return GateDecision.Warn(RedundantRestoreMessage);
            }

            var restoreCapabilities = FindCapabilities(item.ChannelKey, context);
            if (!restoreCapabilities.Hide && !restoreCapabilities.Ingest)
            {
                return GateDecision.Reject("该栏目未声明上页或隐藏能力。");
            }

            if (!CanAppendToWork(
                    item.ChannelKey,
                    item.WorkId,
                    MediaPathRules.IsVideoFile(item.ObjectKey),
                    context))
            {
                return GateDecision.Reject("目标作品图像 src 与占位槽混排，本轮不能上页改写。");
            }

            return GateDecision.Allow(RestoreAllowMessage);
        }

        if (string.IsNullOrWhiteSpace(item.ObjectKey))
        {
            return GateDecision.Reject("无对象键的占位槽不能隐藏。");
        }

        if (intent == MediaIntent.SiteHide)
        {
            if (IsRedundantHide(intent, item))
            {
                return GateDecision.Warn(RedundantHideMessage);
            }

            var capabilities = FindCapabilities(item.ChannelKey, context);
            if (!capabilities.Hide)
            {
                return GateDecision.Reject("该栏目未声明隐藏能力。");
            }

            if (!CanRemoveFromSource(item, context))
            {
                return GateDecision.Reject("该站点级图库只剩当前主图，隐藏会留下断引用。");
            }

            return GateDecision.Allow("将从该作品去掉引用并重排显示标签。");
        }

        var withdrawCapabilities = FindCapabilities(item.ChannelKey, context);
        if (!withdrawCapabilities.Withdraw)
        {
            return GateDecision.Reject("该栏目未声明撤下能力。");
        }

        if (!CanRemoveFromSource(item, context))
        {
            return GateDecision.Reject("该站点级图库只剩当前主图，撤下会留下断引用。");
        }

        if (item.IsStock)
        {
            return GateDecision.Reject("stock 不可撤下。");
        }

        var refs = context.ContentRefCountDict.GetValueOrDefault(item.ObjectKey);
        var batchRemoves = 0;
        context.BatchRemoveCountDict?.TryGetValue(item.ObjectKey, out batchRemoves);
        if (refs - batchRemoves > 0)
        {
            return GateDecision.Reject("仍有其它内容引用该对象，不能撤下。可先只隐藏当前作品。");
        }

        return GateDecision.Allow("将先隐藏再按对象键撤下正式位与 COS。");
    }

    /// <summary>
    /// 判定整项撤下：删除内容记录，对象键留到发布再删正式位与对象存储。
    /// </summary>
    public static GateDecision EvaluateWorkWithdraw(IntentItem item, IntentContext context)
    {
        if (string.IsNullOrWhiteSpace(item.Channel) || string.IsNullOrWhiteSpace(item.WorkId))
        {
            return GateDecision.Reject("整项撤下须指定栏目与作品。");
        }

        var work = WorkWithdrawRules.FindWork(context.WorkList, item.Channel, item.WorkId);
        if (work == null || work.IsUnregistered)
        {
            return GateDecision.Reject("找不到已入编作品。");
        }

        if (!string.Equals(work.SourceKind, "works", StringComparison.Ordinal))
        {
            return GateDecision.Reject("整项撤下只删除内容池作品记录。");
        }

        if (!FindCapabilities(item.Channel, context).Withdraw)
        {
            return GateDecision.Reject("该栏目未声明撤下能力。");
        }

        var objectKeyList = WorkWithdrawRules.CollectObjectKeys(
            work,
            context.LedgerDict,
            WorkWithdrawRules.ResolvePlaceholders(context.Profile));
        foreach (var objectKey in objectKeyList)
        {
            if (objectKey.Contains("/stock/", StringComparison.Ordinal))
            {
                return GateDecision.Reject("stock 不可撤下。");
            }

            if (!context.LedgerDict.TryGetValue(objectKey, out var record))
            {
                return GateDecision.Reject("台账无此对象，不能整项撤下：" + objectKey);
            }

            if (string.Equals(record.Status, "stock", StringComparison.OrdinalIgnoreCase))
            {
                return GateDecision.Reject("stock 不可撤下。");
            }

            var refs = context.ContentRefCountDict.GetValueOrDefault(objectKey);
            var batchRemoves = 0;
            context.BatchRemoveCountDict?.TryGetValue(objectKey, out batchRemoves);
            if (refs - batchRemoves > 0)
            {
                return GateDecision.Reject("仍有其它内容引用 " + objectKey + "，不能整项撤下。");
            }
        }

        return objectKeyList.Count == 0
            ? GateDecision.Allow("将删除内容记录。没有待撤对象。中转站不动。")
            : GateDecision.Allow(
                "将删除内容记录。发布时撤下 "
                + objectKeyList.Count
                + " 个对象（含视频伴生封面）。中转站不动。");
    }

    /// <summary>
    /// 判定未登记夹写成内容层空壳。
    /// </summary>
    public static GateDecision EvaluateRegister(IntentItem item, IntentContext context)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.WorkRegister)
        {
            return GateDecision.Reject("不是登记意图。");
        }

        if (string.IsNullOrWhiteSpace(item.Channel))
        {
            return GateDecision.Reject("登记须指定栏目。");
        }

        var channel = context.Profile?.Channels.FirstOrDefault(candidate => candidate.Key == item.Channel);
        if (channel == null)
        {
            return GateDecision.Reject("栏目未写入工作区配置。");
        }

        if (!WorkRegisterRules.IsAcceptedWorkId(item.Channel, item.WorkId))
        {
            return GateDecision.Reject(
                WorkRegisterRules.IsPhotoFolderChannel(item.Channel)
                    ? "作品 id 须为项目夹名或小写短横线技术键。"
                    : "作品 id 须为小写短横线技术键。");
        }

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return GateDecision.Reject("登记须填写题名。");
        }

        var schedule = EvaluateScheduleFields(item);
        if (!schedule.Allowed)
        {
            return schedule;
        }

        if (PhotoFactRules.RequiresFacts(item.Channel))
        {
            var missing = PhotoFactRules.DescribeMissingRegister(item.StartedOn, item.Year, item.Place, item.Themes);
            if (missing != null)
            {
                return GateDecision.Reject(missing);
            }
        }

        var kind = context.Profile?.SiteCatalog.Kind ?? "json";
        if (string.Equals(kind, "personalworks-ts", StringComparison.OrdinalIgnoreCase))
        {
            if (!WorkRegisterRules.IsRegisterableChannel(item.Channel))
            {
                return GateDecision.Reject("该栏目本轮不登记空壳。");
            }

            if (string.Equals(item.Channel, "profile", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(context.Profile?.SiteCatalog.SitePath))
            {
                return GateDecision.Reject("形象登记须配置 sitePath。");
            }

            if (string.Equals(item.Channel, "game-dev", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(context.Profile?.SiteCatalog.GameDataPath))
            {
                return GateDecision.Reject("游戏登记须配置 gameDataPath。");
            }
        }
        else if (!string.Equals(kind, "json", StringComparison.OrdinalIgnoreCase))
        {
            return GateDecision.Reject("不支持的编目类型。");
        }

        if (context.Profile != null)
        {
            var catalogPath = WorkspaceProfileLoader.ResolveUnderRoot(
                context.Profile,
                context.Profile.SiteCatalog.Path);
            if (!File.Exists(catalogPath))
            {
                return GateDecision.Reject("找不到内容层编目。");
            }
        }

        if (context.WorkList != null
            && context.WorkList.Any(work =>
                !work.IsUnregistered
                && work.Channel == item.Channel
                && work.Id == item.WorkId))
        {
            return GateDecision.Reject("该栏目已有同 id 作品。");
        }

        if (string.IsNullOrWhiteSpace(item.StageFolder))
        {
            return GateDecision.Reject("登记须指定投放箱文件夹。");
        }

        var folderRel = JsonUtil.ToRel(item.StageFolder);
        if (!WorkRegisterRules.IsAllowedStageFolder(item.Channel, folderRel, channel))
        {
            return GateDecision.Reject("投放箱文件夹须为栏目下作品夹，或已登记容器下的一层。");
        }

        var folderId = WorkRegisterRules.FolderIdFromStageFolder(folderRel);
        if (context.WorkList != null
            && !UnregisteredWorkDiscovery.IsUnregisteredTarget(folderId, item.Channel, context.WorkList))
        {
            return GateDecision.Reject("该文件夹不是未登记作品。");
        }

        return GateDecision.Allow("将写入空 media 作品块，不产生对象键。");
    }

    /// <summary>
    /// 判定已入编作品改题名、开始日与地点。
    /// </summary>
    public static GateDecision EvaluateUpdate(IntentItem item, IntentContext context)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.WorkUpdate)
        {
            return GateDecision.Reject("不是编辑项目意图。");
        }

        if (string.IsNullOrWhiteSpace(item.Channel))
        {
            return GateDecision.Reject("编辑须指定栏目。");
        }

        if (context.Profile?.Channels.All(candidate => candidate.Key != item.Channel) == true)
        {
            return GateDecision.Reject("栏目未写入工作区配置。");
        }

        if (!WorkRegisterRules.IsAcceptedWorkId(item.Channel, item.WorkId))
        {
            return GateDecision.Reject(
                WorkRegisterRules.IsPhotoFolderChannel(item.Channel)
                    ? "作品 id 须为项目夹名或小写短横线技术键。"
                    : "作品 id 须为小写短横线技术键。");
        }

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return GateDecision.Reject("编辑须填写题名。");
        }

        var work = context.WorkList?.FirstOrDefault(candidate =>
            !candidate.IsUnregistered
            && candidate.Channel == item.Channel
            && candidate.Id == item.WorkId);
        if (work == null)
        {
            return GateDecision.Reject("找不到已入编作品。");
        }

        var schedule = EvaluateScheduleFields(item);
        if (!schedule.Allowed)
        {
            return schedule;
        }

        return GateDecision.Allow("将改写题名、开始日与地点，不改媒体与授权。");
    }

    /// <summary>
    /// 开始日与地点可空；有值须能规范化。形象 / 游戏不得带这两项。
    /// </summary>
    private static GateDecision EvaluateScheduleFields(IntentItem item)
    {
        if (!WorkRegisterRules.TryNormalizeStartedOn(item.StartedOn, out _))
        {
            return GateDecision.Reject("开始日须为 YYYY-MM-DD、YYYY-MM 或 YYYY。");
        }

        if (!WorkRegisterRules.TryNormalizePlace(item.Place, out _))
        {
            return GateDecision.Reject("地点只写城市名，不要路径或换行。");
        }

        var hasSchedule = !string.IsNullOrWhiteSpace(item.StartedOn) || !string.IsNullOrWhiteSpace(item.Place);
        if (hasSchedule && !WorkRegisterRules.SupportsScheduleFields(item.Channel))
        {
            return GateDecision.Reject("形象与游戏不写开始日、地点。");
        }

        return GateDecision.Allow("");
    }

    /// <summary>
    /// 判定台账路径重挂：只改 stageRel。
    /// </summary>
    public static GateDecision EvaluateRelocate(IntentItem item, IntentContext context)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.StageRelocate)
        {
            return GateDecision.Reject("不是重挂意图。");
        }

        if (string.IsNullOrWhiteSpace(item.Object)
            || string.IsNullOrWhiteSpace(item.StageRelBefore)
            || string.IsNullOrWhiteSpace(item.StageRelAfter))
        {
            return GateDecision.Reject("重挂须指定对象键与新旧投放路径。");
        }

        var objectKey = JsonUtil.ToRel(item.Object);
        var before = JsonUtil.ToRel(item.StageRelBefore);
        var after = JsonUtil.ToRel(item.StageRelAfter);
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return GateDecision.Reject("新旧投放路径相同，无需重挂。");
        }

        if (MediaPathRules.IsStock(objectKey)
            || MediaPathRules.IsStock(before)
            || MediaPathRules.IsStock(after))
        {
            return GateDecision.Reject("stock 拒绝重挂。");
        }

        if (!context.LedgerDict.TryGetValue(objectKey, out var record))
        {
            return GateDecision.Reject("台账没有该对象。");
        }

        if (!string.Equals(record.Status, "published", StringComparison.OrdinalIgnoreCase))
        {
            return GateDecision.Reject("仅已上页记录可重挂。");
        }

        if (!string.Equals(JsonUtil.ToRel(record.StageRel ?? ""), before, StringComparison.Ordinal))
        {
            return GateDecision.Reject("台账当前路径与声明的旧路径不一致。");
        }

        var channel = StageRelocateRules.ChannelOf(objectKey);
        if (!after.StartsWith(channel + "/", StringComparison.Ordinal))
        {
            return GateDecision.Reject("新路径须仍在同一栏目下。");
        }

        if (context.Profile != null)
        {
            var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(context.Profile, context.Profile.StageRoot);
            var afterPath = Path.Combine(stageRoot, after.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(afterPath))
            {
                return GateDecision.Reject("投放箱新路径不存在。");
            }
        }

        foreach (var other in context.LedgerDict.Values)
        {
            if (string.Equals(other.Object, objectKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(other.Status, "published", StringComparison.OrdinalIgnoreCase)
                && string.Equals(JsonUtil.ToRel(other.StageRel ?? ""), after, StringComparison.Ordinal))
            {
                return GateDecision.Reject("新路径已被另一 published 占用。");
            }
        }

        return GateDecision.Allow("将只改台账 stageRel，对象键与内容层不变。");
    }

    /// <summary>
    /// 判定类型与自由标签。不改 src、id、自动 label，也不改年份。
    /// </summary>
    public static GateDecision EvaluateTags(IntentItem item, IntentContext context)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.TagsUpdate)
        {
            return GateDecision.Reject("不是标签意图。");
        }

        if (string.IsNullOrWhiteSpace(item.Channel) || string.IsNullOrWhiteSpace(item.WorkId))
        {
            return GateDecision.Reject("标签须指定栏目与作品。");
        }

        if (NoteRules.IsNotesChannel(item.Channel))
        {
            return GateDecision.Allow("标签写入心得索引，不写摄影类型。");
        }

        if (string.Equals(item.Channel, "profile", StringComparison.Ordinal))
        {
            return GateDecision.Reject("形象不打标签。");
        }

        var channel = context.Profile?.Channels.FirstOrDefault(candidate => candidate.Key == item.Channel);
        if (channel == null)
        {
            return GateDecision.Reject("栏目未写入工作区配置。");
        }

        if (!channel.Capabilities.Ingest)
        {
            return GateDecision.Reject("该栏目未开通上页，不写标签。");
        }

        var work = context.WorkList?.FirstOrDefault(candidate =>
            !candidate.IsUnregistered
            && candidate.Channel == item.Channel
            && candidate.Id == item.WorkId);
        if (work == null)
        {
            return GateDecision.Reject("找不到已入编作品。");
        }

        var themes = PhotoFactRules.NormalizeThemes(item.Themes);
        var objectList = (item.ObjectList ?? new List<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(JsonUtil.ToRel)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!PhotoFactRules.RequiresFacts(item.Channel) && themes.Count > 0)
        {
            return GateDecision.Reject("该栏目不写摄影类型。");
        }

        var writtenThemeCount = item.Themes?.Count(theme => !string.IsNullOrWhiteSpace(theme)) ?? 0;
        if (writtenThemeCount > 0 && themes.Count != writtenThemeCount)
        {
            return GateDecision.Reject("类型须为风光、人文、人像、游戏或 AI。");
        }

        if (objectList.Count == 0 && themes.Count > 0)
        {
            return GateDecision.Reject("项目不写主题类型。类型写在单张照片上。");
        }

        if (!PhotoFactRules.TryNormalizeTags(item.Tags, out _, out var tagError))
        {
            return GateDecision.Reject(tagError ?? "标签无法写入。");
        }

        if (objectList.Count == 0)
        {
            return GateDecision.Allow("将写入作品的自由标签，不改类型与媒体。");
        }

        foreach (var objectKey in objectList)
        {
            var owned = work.Media.Any(media =>
                !string.IsNullOrWhiteSpace(media.Src)
                && string.Equals(JsonUtil.ToRel(media.Src), objectKey, StringComparison.Ordinal));
            if (!owned)
            {
                return GateDecision.Reject("对象不属于该作品或无 src：" + objectKey);
            }
        }

        return GateDecision.Allow("将写入 " + objectList.Count + " 张的类型与标签，不改 src。");
    }

    /// <summary>
    /// 已入编摄影作品缺年份或地点时拒绝新上页。编目未加载时不判。
    /// </summary>
    private static string? MissingPhotoFacts(StageItem item, IntentContext context)
    {
        if (context.WorkList == null || !PhotoFactRules.RequiresFacts(item.ChannelKey))
        {
            return null;
        }

        var work = context.WorkList.FirstOrDefault(candidate =>
            !candidate.IsUnregistered
            && candidate.Channel == item.ChannelKey
            && candidate.Id == context.TargetWorkId);
        return work == null ? null : PhotoFactRules.DescribeMissingIngest(work);
    }

    /// <summary>
    /// 判定星级回写。已入编对象当场写 <c>stars</c>；未上页的留到上页并入，不挡住同批其它条。
    /// </summary>
    public static GateDecision EvaluateStars(IntentItem item, IntentContext context)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.StarsUpdate)
        {
            return GateDecision.Reject("不是星级意图。");
        }

        if (item.Stars is not (>= 0 and <= 5))
        {
            return GateDecision.Reject("星级须为 0～5 的整数。");
        }

        if (!string.IsNullOrWhiteSpace(item.Object))
        {
            if (string.IsNullOrWhiteSpace(item.Channel) || string.IsNullOrWhiteSpace(item.WorkId))
            {
                return GateDecision.Allow("内容层尚无此对象，星级留到上页后写入。");
            }

            var work = context.WorkList?.FirstOrDefault(candidate =>
                !candidate.IsUnregistered
                && string.Equals(candidate.Channel, item.Channel, StringComparison.Ordinal)
                && string.Equals(candidate.Id, item.WorkId, StringComparison.Ordinal));
            if (work == null)
            {
                return GateDecision.Reject("未知作品：" + item.Channel + "/" + item.WorkId);
            }

            var objectKey = JsonUtil.ToRel(item.Object);
            var owned = work.Media.Any(media =>
                !string.IsNullOrWhiteSpace(media.Src)
                && string.Equals(JsonUtil.ToRel(media.Src), objectKey, StringComparison.Ordinal))
                || work.HiddenObjectSet.Contains(objectKey);
            if (!owned)
            {
                return GateDecision.Reject("对象不属于该作品或无 src：" + objectKey);
            }

            return item.Stars == 0
                ? GateDecision.Allow("清除星级：" + objectKey)
                : GateDecision.Allow("将 " + objectKey + " 写成 " + item.Stars + " 星。");
        }

        if (!string.IsNullOrWhiteSpace(item.StageRel))
        {
            var stageRel = JsonUtil.ToRel(item.StageRel);
            return item.Stars == 0
                ? GateDecision.Allow("未上页，不写入星级。")
                : GateDecision.Allow("上页时将 " + stageRel + " 写成 " + item.Stars + " 星。");
        }

        return GateDecision.Reject("星级须指定对象键。");
    }

    /// <summary>
    /// 判定文案回写：不改对象键、自动 label 与作品 id。
    /// </summary>
    public static GateDecision EvaluateCopy(IntentItem item, IntentContext context)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.CopyUpdate)
        {
            return GateDecision.Reject("不是文案意图。");
        }

        var target = item.Target ?? "";
        if (target is not ("channel" or "work" or "media"))
        {
            return GateDecision.Reject("文案目标须为 channel、work 或 media。");
        }

        if (string.IsNullOrWhiteSpace(item.Channel))
        {
            return GateDecision.Reject("文案须指定栏目。");
        }

        if (NoteRules.IsNotesChannel(item.Channel) && string.Equals(target, "media", StringComparison.Ordinal))
        {
            return GateDecision.Allow("名称与描述写入心得索引，不回写正文。");
        }

        var channel = context.Profile?.Channels.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, item.Channel, StringComparison.Ordinal));
        if (channel == null)
        {
            return GateDecision.Reject("未知频道。");
        }

        var hasTitle = CopyText.IsFilled(item.Title);
        var hasDescription = CopyText.IsFilled(item.Description);
        if (string.Equals(item.Channel, "profile", StringComparison.Ordinal)
            && (hasTitle || hasDescription))
        {
            return GateDecision.Reject("形象频道不写描述与自定义名。");
        }

        if (string.Equals(item.Channel, "portrait-photo", StringComparison.Ordinal) && hasDescription)
        {
            return GateDecision.Reject("人像摄影不写描述。");
        }

        if (string.Equals(target, "channel", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(item.WorkId))
            {
                return GateDecision.Reject("栏目文案禁止带 workId。");
            }

            if (!string.IsNullOrWhiteSpace(item.Object))
            {
                return GateDecision.Reject("栏目文案禁止带 object。");
            }

            return GateDecision.Allow(CopyDisplayRules.DescribeVisibility(item, null));
        }

        if (string.IsNullOrWhiteSpace(item.WorkId))
        {
            return GateDecision.Reject("项目或资源文案须指定作品。");
        }

        var work = context.WorkList?.FirstOrDefault(candidate =>
            !candidate.IsUnregistered
            && string.Equals(candidate.Id, item.WorkId, StringComparison.Ordinal)
            && string.Equals(candidate.Channel, item.Channel, StringComparison.Ordinal));
        if (work == null)
        {
            return GateDecision.Reject("未知作品。");
        }

        if (string.Equals(target, "work", StringComparison.Ordinal))
        {
            return GateDecision.Allow(CopyDisplayRules.DescribeVisibility(item, item.Description));
        }

        if (string.IsNullOrWhiteSpace(item.Object))
        {
            return GateDecision.Reject("资源文案须指定对象键。");
        }

        var objectKey = JsonUtil.ToRel(item.Object);
        var media = work.Media.FirstOrDefault(candidate =>
            string.Equals(candidate.Src, objectKey, StringComparison.Ordinal));
        if (media == null || string.IsNullOrWhiteSpace(media.Src))
        {
            return GateDecision.Reject("对象不属于该作品或无 src。");
        }

        return GateDecision.Allow(CopyDisplayRules.DescribeVisibility(item, work.Summary));
    }

    /// <summary>
    /// 判定站点页显示顺序。
    /// </summary>
    public static GateDecision EvaluateReorder(IntentItem item, IntentContext context)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.MediaReorder)
        {
            return GateDecision.Reject("不是排序意图。");
        }

        if (string.IsNullOrWhiteSpace(item.Channel))
        {
            return GateDecision.Reject("排序须指定栏目。");
        }

        if (context.Profile?.Channels.All(candidate => candidate.Key != item.Channel) == true)
        {
            return GateDecision.Reject("栏目未写入工作区配置。");
        }

        if (string.IsNullOrWhiteSpace(item.WorkId))
        {
            return GateDecision.Reject("排序须指定作品。");
        }

        var work = context.WorkList?.FirstOrDefault(candidate =>
            !candidate.IsUnregistered
            && string.Equals(candidate.Id, item.WorkId, StringComparison.Ordinal)
            && string.Equals(candidate.Channel, item.Channel, StringComparison.Ordinal));
        if (work == null)
        {
            return GateDecision.Reject("未知作品。");
        }

        var draftList = (item.ObjectList ?? new List<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => JsonUtil.ToRel(key))
            .ToList();
        if (draftList.Count == 0)
        {
            return GateDecision.Reject("排序须给出对象键列表。");
        }

        if (draftList.Count != draftList.Distinct(StringComparer.Ordinal).Count())
        {
            return GateDecision.Reject("排序列表含重复对象键。");
        }

        var srcSet = new HashSet<string>(MediaOrder.SrcList(work.Media), StringComparer.Ordinal);
        var draftSet = new HashSet<string>(draftList, StringComparer.Ordinal);
        if (!srcSet.SetEquals(draftSet))
        {
            return GateDecision.Reject("排序须覆盖该作品当前全部在页对象，且不得混入未知键。");
        }

        if (MediaOrder.SameOrder(draftList, MediaOrder.SrcList(work.Media)))
        {
            return GateDecision.Warn("显示顺序与内容层相同，将跳过。");
        }

        return GateDecision.Allow("将按站点页顺序重写该作品 media 数组，对象键不改名。");
    }

    /// <summary>
    /// 目标作品现有 media 是否允许追加上页。
    /// </summary>
    private static bool CanAppendToTargetWork(string channelKey, IntentContext context, bool incomingVideo)
    {
        return CanAppendToWork(channelKey, context.TargetWorkId, incomingVideo, context);
    }

    /// <summary>
    /// 指定作品现有 media 是否允许追加引用。
    /// </summary>
    private static bool CanAppendToWork(string channelKey, string? workId, bool incomingVideo, IntentContext context)
    {
        if (context.WorkList == null || string.IsNullOrWhiteSpace(workId))
        {
            return true;
        }

        var work = context.WorkList.FirstOrDefault(item =>
            item.Id == workId && item.Channel == channelKey);
        return work == null || work.SupportsAppend && ContentPatchPlanner.CanAppendIngest(work.Media, incomingVideo);
    }

    /// <summary>
    /// 站点级主图必须至少保留一个可替代对象。
    /// </summary>
    private static bool CanRemoveFromSource(SiteItem item, IntentContext context)
    {
        var work = context.WorkList?.FirstOrDefault(candidate =>
            candidate.Id == item.WorkId && candidate.Channel == item.ChannelKey);
        if (work == null || !work.SourceKind.StartsWith("site-", StringComparison.Ordinal))
        {
            return true;
        }

        return work.Media.Any(media =>
            !string.IsNullOrWhiteSpace(media.Src)
            && !string.Equals(media.Src, item.ObjectKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// 投放箱条目在磁盘上是否仍存在。
    /// </summary>
    private static bool StageExists(StageItem item)
    {
        if (string.IsNullOrWhiteSpace(item.FullPath))
        {
            return false;
        }

        return item.IsPack ? Directory.Exists(item.FullPath) : File.Exists(item.FullPath);
    }

    /// <summary>
    /// 目标或路径猜测是否指向未登记作品。
    /// </summary>
    private static bool IsUnregisteredStage(StageItem item, IntentContext context)
    {
        return UnregisteredWorkDiscovery.IsUnregisteredTarget(
                context.TargetWorkId,
                item.ChannelKey,
                context.WorkList)
            || (string.IsNullOrWhiteSpace(context.TargetWorkId)
                && UnregisteredWorkDiscovery.IsUnregisteredGuess(item, context.WorkList));
    }

    /// <summary>
    /// 读取栏目能力；旧配置缺省保持兼容。
    /// </summary>
    private static ChannelCapabilities FindCapabilities(string channelKey, IntentContext context)
    {
        return context.Profile?.Channels.FirstOrDefault(channel => channel.Key == channelKey)?.Capabilities
            ?? new ChannelCapabilities();
    }

    /// <summary>
    /// 对象是否出现在内容层。
    /// </summary>
    public static bool IsOnSite(string? objectKey, IntentContext context)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        return context.ContentRefCountDict.GetValueOrDefault(objectKey) > 0;
    }

    /// <summary>
    /// 台账是否为 published。
    /// </summary>
    public static bool IsPublished(string? objectKey, IntentContext context)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        return context.LedgerDict.TryGetValue(objectKey, out var record)
            && IsPublished(record.Status);
    }

    /// <summary>
    /// 状态字符串是否表示已发布。
    /// </summary>
    public static bool IsPublished(string? status)
    {
        return string.Equals(status, "published", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 已隐藏再标隐藏的预演提示；执行跳过写盘，不硬拒绝整批。
    /// </summary>
    public const string RedundantHideMessage = "该资源已隐藏，无需再隐藏，将跳过。";

    /// <summary>
    /// 已上页再标上页的预演提示。
    /// </summary>
    public const string RedundantIngestMessage = "该资源已上页，无需再上页，将跳过。";

    /// <summary>
    /// 已在页上再标恢复上页的预演提示。
    /// </summary>
    public const string RedundantRestoreMessage = "该资源已在页上，无需再上页，将跳过。";

    /// <summary>
    /// 已隐藏恢复显示：只写回原对象键。
    /// </summary>
    public const string RestoreAllowMessage = "将把原对象键写回该作品，不新建键、不重新入库。";

    /// <summary>
    /// 站点条目是否为已隐藏后再标隐藏。
    /// </summary>
    public static bool IsRedundantHide(MediaIntent intent, SiteItem? item)
    {
        return intent == MediaIntent.SiteHide && item is { IsHidden: true };
    }

    /// <summary>
    /// 意图回绑编目后是否为已隐藏后再标隐藏。
    /// </summary>
    public static bool IsRedundantHide(WorkspaceSession session, IntentItem item)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.SiteHide
            || string.IsNullOrWhiteSpace(item.Object))
        {
            return false;
        }

        var objectKey = JsonUtil.ToRel(item.Object);
        var site = session.SiteItems.FirstOrDefault(candidate =>
            string.Equals(candidate.ObjectKey, objectKey, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(item.WorkId)
                || string.Equals(candidate.WorkId, item.WorkId, StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(item.Channel)
                || string.Equals(candidate.ChannelKey, item.Channel, StringComparison.Ordinal)));
        return site is { IsHidden: true };
    }

    /// <summary>
    /// 预演判定是否为已隐藏跳过（可另附中转站缺失等软警告）。
    /// </summary>
    public static bool IsRedundantHideDecision(GateDecision decision)
    {
        return decision.Allowed
            && decision.IsWarn
            && decision.Message.StartsWith(RedundantHideMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 站点条目是否为已在页上再标恢复上页。
    /// </summary>
    public static bool IsRedundantRestore(MediaIntent intent, SiteItem? item)
    {
        return intent == MediaIntent.SiteRestore && item is { IsHidden: false };
    }

    /// <summary>
    /// 意图回绑编目后是否为已在页上再标恢复上页。
    /// </summary>
    public static bool IsRedundantRestore(WorkspaceSession session, IntentItem item)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.SiteRestore
            || string.IsNullOrWhiteSpace(item.Object))
        {
            return false;
        }

        var objectKey = JsonUtil.ToRel(item.Object);
        var site = session.SiteItems.FirstOrDefault(candidate =>
            string.Equals(candidate.ObjectKey, objectKey, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(item.WorkId)
                || string.Equals(candidate.WorkId, item.WorkId, StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(item.Channel)
                || string.Equals(candidate.ChannelKey, item.Channel, StringComparison.Ordinal)));
        return site is { IsHidden: false };
    }

    /// <summary>
    /// 预演判定是否为已在页上跳过恢复上页。
    /// </summary>
    public static bool IsRedundantRestoreDecision(GateDecision decision)
    {
        return decision.Allowed
            && decision.IsWarn
            && decision.Message.StartsWith(RedundantRestoreMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 投放箱条目是否为已上页后再标上页。
    /// </summary>
    public static bool IsRedundantIngest(StageItem item, IntentContext context)
    {
        return IsOnSite(item.MatchedObject, context)
            || IsPublished(item.MatchedObject, context)
            || IsPublished(item.LedgerStatus);
    }

    /// <summary>
    /// 意图回绑编目后是否为已上页后再标上页。
    /// </summary>
    public static bool IsRedundantIngest(WorkspaceSession session, IntentItem item)
    {
        if (MediaIntentCodes.FromCode(item.Intent) != MediaIntent.StageIngest
            || string.IsNullOrWhiteSpace(item.StageRel))
        {
            return false;
        }

        var stageRel = JsonUtil.ToRel(item.StageRel);
        var stage = session.StageItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StageRel, stageRel, StringComparison.Ordinal));
        if (stage == null)
        {
            return false;
        }

        return IsRedundantIngest(
            stage,
            new IntentContext
            {
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict
            });
    }

    /// <summary>
    /// 预演判定是否为已上页跳过入库。
    /// </summary>
    public static bool IsRedundantIngestDecision(GateDecision decision)
    {
        return decision.Allowed
            && decision.IsWarn
            && decision.Message.StartsWith(RedundantIngestMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 预演判定是否为已隐藏 / 已上页 / 已在页上的跳过。
    /// </summary>
    public static bool IsRedundantSkipDecision(GateDecision decision)
    {
        return IsRedundantHideDecision(decision)
            || IsRedundantIngestDecision(decision)
            || IsRedundantRestoreDecision(decision);
    }
}
