using System.Text;
using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次内容补丁将改写的作品片段。
/// </summary>
public sealed class ContentPatchChange
{
    public required string WorkId { get; init; }
    public required string Channel { get; init; }
    public required string Title { get; init; }
    public required string KindBefore { get; init; }
    public required string KindAfter { get; init; }
    public required string BeforeSnippet { get; init; }
    public required string AfterSnippet { get; init; }
    public IReadOnlyList<string> RemovedObjectList { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AddedObjectList { get; init; } = Array.Empty<string>();
    public IReadOnlyList<WorkMediaItem> AfterMediaList { get; init; } = Array.Empty<WorkMediaItem>();

    /// <summary>
    /// 是否按站点页顺序改写了数组序。
    /// </summary>
    public bool OrderChanged { get; init; }
}

/// <summary>
/// 内容补丁预演：按作品去掉 src；album 改为 listed 并重排标签；cover / covered 抽掉后无 src 则写 images；含 video 的数组保留视频。
/// </summary>
public static class ContentPatchPlanner
{
    private static readonly Regex AlbumRegex = new(
        @"media:\s*album\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(\d+)(?:\s*,\s*""([^""]+)"")?\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex CoveredHeadRegex = new(
        @"media:\s*covered\s*\(",
        RegexOptions.Compiled);
    private static readonly Regex CoverArrayHeadRegex = new(
        @"media:\s*\[",
        RegexOptions.Compiled);
    private static readonly Regex CoveredArgsRegex = new(
        @"covered\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*\[([\s\S]*)\]\s*\)",
        RegexOptions.Compiled);
    private static readonly Regex CoverTokenRegex = new(
        @"cover\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\)|\.\.\.images\(([^)]*)\)|\{\s*[""']?kind[""']?\s*:\s*['""](video|image)['""]([^}]*)\}",
        RegexOptions.Compiled);
    private static readonly Regex CoverSrcRegex = new(
        @"^([^/]+)/([^/]+)/01\.webp$",
        RegexOptions.Compiled);
    private static readonly Regex IdRegex = new(
        @"id:\s*""([^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex ChannelRegex = new(
        @"channel:\s*""([^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// 根据已允许的隐藏 / 撤下 / 上页条目生成将写入的片段。
    /// </summary>
    public static IReadOnlyList<ContentPatchChange> Plan(
        WorkspaceSession session,
        IEnumerable<IntentItem> itemList)
    {
        var items = itemList as IReadOnlyList<IntentItem> ?? itemList.ToList();
        var sortKeyDict = StageRelOrder.BuildSortKeyDict(session.LedgerDict, items);
        var removeDict = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var addDict = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var videoLabelDict = new Dictionary<string, string>(StringComparer.Ordinal);
        var photoStemDict = BuildPhotoStemDict(session.LedgerDict, items);
        var registerList = new List<IntentItem>();
        var updateList = new List<IntentItem>();
        var copyList = new List<IntentItem>();
        var tagList = new List<IntentItem>();
        var starList = new List<IntentItem>();
        var orderDict = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var intent = MediaIntentCodes.FromCode(item.Intent);
            if (intent == MediaIntent.WorkRegister)
            {
                registerList.Add(item);
                continue;
            }

            if (intent == MediaIntent.WorkUpdate)
            {
                updateList.Add(item);
                continue;
            }

            if (intent == MediaIntent.CopyUpdate)
            {
                copyList.Add(item);
                continue;
            }

            if (intent == MediaIntent.TagsUpdate)
            {
                tagList.Add(item);
                continue;
            }

            if (intent == MediaIntent.StarsUpdate)
            {
                starList.Add(item);
                continue;
            }

            if (intent == MediaIntent.MediaReorder)
            {
                if (string.IsNullOrWhiteSpace(item.WorkId)
                    || string.IsNullOrWhiteSpace(item.Channel)
                    || item.ObjectList == null
                    || item.ObjectList.Count == 0)
                {
                    continue;
                }

                orderDict[WorkMapKey(item.Channel, item.WorkId)] = item.ObjectList
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => JsonUtil.ToRel(key))
                    .ToList();
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.WorkId) || string.IsNullOrWhiteSpace(item.Object))
            {
                continue;
            }

            var mapKey = WorkMapKey(item.Channel, item.WorkId);
            if (intent is MediaIntent.SiteHide or MediaIntent.SiteWithdraw)
            {
                if (!removeDict.TryGetValue(mapKey, out var objectSet))
                {
                    objectSet = new HashSet<string>(StringComparer.Ordinal);
                    removeDict[mapKey] = objectSet;
                }

                objectSet.Add(item.Object);
            }
            else if (intent is MediaIntent.StageIngest or MediaIntent.SiteRestore)
            {
                if (intent == MediaIntent.StageIngest && IntentGate.IsRedundantIngest(session, item))
                {
                    continue;
                }

                if (!addDict.TryGetValue(mapKey, out var objectList))
                {
                    objectList = new List<string>();
                    addDict[mapKey] = objectList;
                }

                if (!objectList.Contains(item.Object, StringComparer.Ordinal))
                {
                    objectList.Add(item.Object);
                    videoLabelDict[item.Object] = DefaultVideoLabel(item.SourceStageRel, item.StageRel);
                }
            }
        }

        if (removeDict.Count == 0
            && addDict.Count == 0
            && registerList.Count == 0
            && updateList.Count == 0
            && copyList.Count == 0
            && tagList.Count == 0
            && starList.Count == 0
            && orderDict.Count == 0)
        {
            return Array.Empty<ContentPatchChange>();
        }

        var kind = session.Profile.SiteCatalog.Kind ?? "json";
        List<ContentPatchChange> changeList;
        if (string.Equals(kind, "personalworks-ts", StringComparison.OrdinalIgnoreCase))
        {
            var worksPath = WorkspaceProfileLoader.ResolveUnderRoot(session.Profile, session.Profile.SiteCatalog.Path);
            changeList = PlanWorksTs(
                    worksPath,
                    session.Works.Where(work => work.SourceKind == "works").ToList(),
                    removeDict,
                    addDict,
                    videoLabelDict,
                    sortKeyDict,
                    orderDict,
                    photoStemDict)
                .ToList();
            changeList.AddRange(PlanSiteCatalog(
                session.Works.Where(work => work.SourceKind.StartsWith("site-", StringComparison.Ordinal)).ToList(),
                removeDict,
                addDict,
                videoLabelDict,
                sortKeyDict,
                orderDict,
                photoStemDict));
        }
        else
        {
            changeList = PlanJsonCatalog(session.Works, removeDict, addDict, videoLabelDict, sortKeyDict, orderDict, photoStemDict).ToList();
        }

        changeList.AddRange(PlanRegister(session, registerList));
        changeList.AddRange(PlanUpdate(session, updateList));
        changeList.AddRange(PlanCopy(session, copyList));
        changeList.AddRange(PlanTags(tagList));
        changeList.AddRange(PlanStars(session, starList));
        return changeList;
    }

    /// <summary>
    /// 把补丁片段写成预演文本。
    /// </summary>
    public static string RenderFragments(IReadOnlyList<ContentPatchChange> changeList)
    {
        if (changeList.Count == 0)
        {
            return "";
        }

        var builder = new StringBuilder();
        builder.AppendLine("## 内容层将改片段");
        builder.AppendLine();
        foreach (var change in changeList)
        {
            builder.AppendLine($"### {change.Title}（{change.WorkId}）");
            builder.AppendLine(
                change.KindBefore == "none"
                    ? "登记空壳；media 为空；不产生对象键。"
                    : change.KindBefore == "copy"
                        ? "只改文案字段；id / src / 自动 label 不改。"
                        : DescribeMediaChange(change));
            builder.AppendLine();
            builder.AppendLine("改前：");
            builder.AppendLine();
            builder.AppendLine("```");
            builder.AppendLine(change.BeforeSnippet.TrimEnd());
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("改后：");
            builder.AppendLine();
            builder.AppendLine("```");
            builder.AppendLine(change.AfterSnippet.TrimEnd());
            builder.AppendLine("```");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// 媒体补丁预演句：保序或按站点页顺序，不再写投放箱整表重排。
    /// </summary>
    private static string DescribeMediaChange(ContentPatchChange change)
    {
        var orderText = change.OrderChanged ? "按站点页顺序重排显示" : "保留原显示顺序，新图追加在末尾";
        return $"去掉 {change.RemovedObjectList.Count} 条，追加 {change.AddedObjectList.Count} 条；{orderText}；{change.KindBefore} → {change.KindAfter}；对象键不改名。";
    }

    /// <summary>
    /// 预演将插入的空 media 作品块。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanRegister(
        WorkspaceSession session,
        IReadOnlyList<IntentItem> registerList)
    {
        if (registerList.Count == 0)
        {
            return Array.Empty<ContentPatchChange>();
        }

        var kind = session.Profile.SiteCatalog.Kind ?? "json";
        var isTs = string.Equals(kind, "personalworks-ts", StringComparison.OrdinalIgnoreCase);
        if (!isTs && !string.Equals(kind, "json", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<ContentPatchChange>();
        }

        var changeList = new List<ContentPatchChange>();
        foreach (var item in registerList)
        {
            if (string.IsNullOrWhiteSpace(item.WorkId) || string.IsNullOrWhiteSpace(item.Channel))
            {
                continue;
            }

            var kindAfter = !isTs
                ? "json"
                : item.Channel == "profile"
                    ? "site-profile-session"
                    : item.Channel == "game-dev"
                        ? "site-game-shell"
                        : "works";
            var afterSnippet = !isTs
                ? FormatJsonWork(item)
                : item.Channel == "profile"
                    ? FormatProfileSession(item)
                    : item.Channel == "game-dev"
                        ? FormatGameShell(item)
                        : FormatTsWork(item);
            changeList.Add(new ContentPatchChange
            {
                WorkId = item.WorkId,
                Channel = item.Channel,
                Title = string.IsNullOrWhiteSpace(item.Title) ? item.WorkId : item.Title,
                KindBefore = "none",
                KindAfter = kindAfter,
                BeforeSnippet = "(无此作品)",
                AfterSnippet = afterSnippet,
                AfterMediaList = Array.Empty<WorkMediaItem>()
            });
        }

        return changeList;
    }

    /// <summary>
    /// 预演已入编作品的题名、开始日与地点改写。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanUpdate(
        WorkspaceSession session,
        IReadOnlyList<IntentItem> updateList)
    {
        if (updateList.Count == 0)
        {
            return Array.Empty<ContentPatchChange>();
        }

        var changeList = new List<ContentPatchChange>();
        foreach (var item in updateList)
        {
            var work = session.Works.FirstOrDefault(candidate =>
                !candidate.IsUnregistered
                && string.Equals(candidate.Id, item.WorkId, StringComparison.Ordinal)
                && string.Equals(candidate.Channel, item.Channel, StringComparison.Ordinal));
            var before = work == null
                ? "(无此作品)"
                : $"{work.Title}；{work.StartedOn ?? work.Year}；{work.Place ?? ""}";
            var afterDate = string.IsNullOrWhiteSpace(item.StartedOn) ? item.Year : item.StartedOn;
            changeList.Add(new ContentPatchChange
            {
                WorkId = item.WorkId ?? "",
                Channel = item.Channel ?? "",
                Title = item.Title ?? work?.Title ?? item.WorkId ?? "项目",
                KindBefore = "meta",
                KindAfter = "meta",
                BeforeSnippet = before,
                AfterSnippet = $"{item.Title}；{afterDate}；{item.Place ?? ""}"
            });
        }

        return changeList;
    }

    /// <summary>
    /// 预演文案字段改写与网页显隐。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanCopy(
        WorkspaceSession session,
        IReadOnlyList<IntentItem> copyList)
    {
        if (copyList.Count == 0)
        {
            return Array.Empty<ContentPatchChange>();
        }

        var changeList = new List<ContentPatchChange>();
        foreach (var item in copyList)
        {
            var work = session.Works.FirstOrDefault(candidate =>
                !candidate.IsUnregistered
                && string.Equals(candidate.Id, item.WorkId, StringComparison.Ordinal)
                && string.Equals(candidate.Channel, item.Channel, StringComparison.Ordinal));
            var media = work?.Media.FirstOrDefault(candidate =>
                string.Equals(candidate.Src, JsonUtil.ToRel(item.Object ?? ""), StringComparison.Ordinal));
            var field = item.Target == "channel"
                ? "lead"
                : item.Target == "media"
                    ? "displayName / description"
                    : string.Equals(item.Channel, "game-dev", StringComparison.Ordinal)
                        ? "lead"
                        : "summary";
            var published = item.Target == "channel"
                ? (session.ChannelLeadDict.TryGetValue(item.Channel ?? "", out var lead) ? lead : "")
                : item.Target == "media"
                    ? $"displayName={media?.DisplayName ?? ""}；description={media?.Description ?? ""}"
                    : work?.Summary ?? "";
            var next = item.Target == "media"
                ? $"displayName={item.Title ?? ""}；description={item.Description ?? ""}"
                : item.Description ?? "";
            changeList.Add(new ContentPatchChange
            {
                WorkId = item.WorkId ?? item.Channel ?? "",
                Channel = item.Channel ?? "",
                Title = work?.Title ?? item.Channel ?? "文案",
                KindBefore = "copy",
                KindAfter = "copy",
                BeforeSnippet = field + "：" + published,
                AfterSnippet = field + "：" + next + Environment.NewLine
                    + CopyDisplayRules.DescribeVisibility(item, work?.Summary)
            });
        }

        return changeList;
    }

    /// <summary>
    /// 年份跟开始日走；没有开始日则用意图里的 year 或当年。摄影栏目不补当年。
    /// </summary>
    private static string ResolveYear(IntentItem item)
    {
        if (PhotoFactRules.RequiresFacts(item.Channel))
        {
            return PhotoFactRules.TryReadExplicitYear(item.StartedOn, item.Year, out var year) ? year : "";
        }

        return WorkRegisterRules.YearFromStartedOn(item.StartedOn, item.Year);
    }

    /// <summary>
    /// 预演星级字段。不改对象键、自动 label 与数组序。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanStars(
        WorkspaceSession session,
        IReadOnlyList<IntentItem> starList)
    {
        var changeList = new List<ContentPatchChange>();
        foreach (var item in starList)
        {
            if (string.IsNullOrWhiteSpace(item.Object) || item.Stars is not (>= 0 and <= 5))
            {
                continue;
            }

            var work = session.Works.FirstOrDefault(candidate =>
                !candidate.IsUnregistered
                && string.Equals(candidate.Id, item.WorkId, StringComparison.Ordinal)
                && string.Equals(candidate.Channel, item.Channel, StringComparison.Ordinal));
            var media = work?.Media.FirstOrDefault(candidate =>
                string.Equals(JsonUtil.ToRel(candidate.Src ?? ""), JsonUtil.ToRel(item.Object), StringComparison.Ordinal));
            var before = media?.Stars ?? 0;
            var after = item.Stars.Value == 0 ? "清除星级" : item.Stars + " 星";
            changeList.Add(new ContentPatchChange
            {
                WorkId = item.WorkId ?? "",
                Channel = item.Channel ?? "",
                Title = work?.Title ?? item.Object,
                KindBefore = "stars",
                KindAfter = "stars",
                BeforeSnippet = "stars=" + before,
                AfterSnippet = after
            });
        }

        return changeList;
    }

    /// <summary>
    /// 预演标签将写入的类型与自由标签，不展开媒体正文。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanTags(IReadOnlyList<IntentItem> tagList)
    {
        var changeList = new List<ContentPatchChange>();
        foreach (var item in tagList)
        {
            if (string.IsNullOrWhiteSpace(item.WorkId) || string.IsNullOrWhiteSpace(item.Channel))
            {
                continue;
            }

            var themes = PhotoFactRules.NormalizeThemes(item.Themes);
            PhotoFactRules.TryNormalizeTags(item.Tags, out var tags, out _);
            var objectList = (item.ObjectList ?? new List<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(JsonUtil.ToRel)
                .ToList();
            var builder = new StringBuilder();
            builder.Append("themes: [");
            builder.Append(string.Join(", ", themes.Select(key => "\"" + key + "\"")));
            builder.AppendLine("]");
            builder.Append("tags: [");
            builder.Append(string.Join(", ", tags.Select(tag => "\"" + tag + "\"")));
            builder.AppendLine("]");
            builder.Append(objectList.Count == 0 ? "作品" : string.Join(", ", objectList));
            changeList.Add(new ContentPatchChange
            {
                WorkId = item.WorkId,
                Channel = item.Channel,
                Title = item.WorkId,
                KindBefore = "tags",
                KindAfter = "tags",
                BeforeSnippet = "",
                AfterSnippet = builder.ToString()
            });
        }

        return changeList;
    }

    /// <summary>
    /// 写成将追加的 TypeScript 空壳作品块。
    /// </summary>
    public static string FormatTsWork(IntentItem item)
    {
        var title = WorkRegisterRules.EscapeTsString(item.Title);
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"    id: \"{item.WorkId}\",");
        builder.AppendLine($"    channel: \"{item.Channel}\",");
        builder.AppendLine($"    title: \"{title}\",");
        builder.AppendLine($"    year: \"{ResolveYear(item)}\",");
        if (!string.IsNullOrWhiteSpace(item.StartedOn))
        {
            builder.AppendLine($"    startedOn: \"{WorkRegisterRules.EscapeTsString(item.StartedOn)}\",");
        }

        if (!string.IsNullOrWhiteSpace(item.Place))
        {
            builder.AppendLine($"    place: \"{WorkRegisterRules.EscapeTsString(item.Place)}\",");
        }

        builder.AppendLine("    summary: \"\",");
        builder.AppendLine("    body: \"\",");
        builder.AppendLine("    consent: \"pending\",");
        if (!string.IsNullOrWhiteSpace(item.StageFolder))
        {
            builder.AppendLine($"    stageFolder: \"{JsonUtil.ToRel(item.StageFolder)}\",");
        }

        builder.AppendLine("    media: [],");
        builder.Append("  }");
        return builder.ToString();
    }

    /// <summary>
    /// 形象场次空壳；只给工具认领，不进访客作品树。
    /// </summary>
    public static string FormatProfileSession(IntentItem item)
    {
        var title = WorkRegisterRules.EscapeTsString(item.Title);
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"    id: \"{item.WorkId}\",");
        builder.AppendLine($"    title: \"{title}\",");
        builder.AppendLine($"    stageFolder: \"{JsonUtil.ToRel(item.StageFolder ?? "")}\",");
        builder.Append("  }");
        return builder.ToString();
    }

    /// <summary>
    /// 游戏空壳；无截图时不进访客选单。
    /// </summary>
    public static string FormatGameShell(IntentItem item)
    {
        var title = WorkRegisterRules.EscapeTsString(item.Title);
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"    id: \"{item.WorkId}\",");
        builder.AppendLine($"    title: \"{title}\",");
        builder.AppendLine("    titleEn: \"\",");
        builder.AppendLine("    lead: \"\",");
        builder.AppendLine("    playable: false,");
        builder.AppendLine("    consent: \"pending\",");
        if (!string.IsNullOrWhiteSpace(item.StageFolder))
        {
            builder.AppendLine($"    stageFolder: \"{JsonUtil.ToRel(item.StageFolder)}\",");
        }

        builder.AppendLine("    screenshots: [],");
        builder.Append("  }");
        return builder.ToString();
    }

    /// <summary>
    /// 写成将追加的 JSON 作品块。
    /// </summary>
    public static string FormatJsonWork(IntentItem item)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine($"  \"id\": \"{item.WorkId}\",");
        builder.AppendLine($"  \"channel\": \"{item.Channel}\",");
        builder.AppendLine($"  \"title\": \"{item.Title}\",");
        if (WorkRegisterRules.SupportsScheduleFields(item.Channel))
        {
            builder.AppendLine($"  \"year\": \"{ResolveYear(item)}\",");
            if (!string.IsNullOrWhiteSpace(item.StartedOn))
            {
                builder.AppendLine($"  \"startedOn\": \"{item.StartedOn}\",");
            }

            if (!string.IsNullOrWhiteSpace(item.Place))
            {
                builder.AppendLine($"  \"place\": \"{item.Place}\",");
            }
        }

        if (!string.IsNullOrWhiteSpace(item.StageFolder))
        {
            builder.AppendLine($"  \"stageFolder\": \"{JsonUtil.ToRel(item.StageFolder)}\",");
        }

        builder.AppendLine("  \"media\": []");
        builder.Append("}");
        return builder.ToString();
    }

    /// <summary>
    /// 按 JSON 编目计算去掉引用后的 media。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanJsonCatalog(
        IReadOnlyList<WorkCatalogItem> workList,
        IReadOnlyDictionary<string, HashSet<string>> removeDict,
        IReadOnlyDictionary<string, List<string>> addDict,
        IReadOnlyDictionary<string, string> videoLabelDict,
        IReadOnlyDictionary<string, string> sortKeyDict,
        IReadOnlyDictionary<string, List<string>> orderDict,
        IReadOnlyDictionary<string, string> photoStemDict)
    {
        var changeList = new List<ContentPatchChange>();
        foreach (var work in workList)
        {
            if (!TryGetMediaMaps(work, removeDict, addDict, orderDict, out var objectSet, out var addList, out var orderList))
            {
                continue;
            }

            var planned = PlanMedia(work.Media, objectSet, addList, videoLabelDict, sortKeyDict, orderList, work.Channel, photoStemDict);
            var orderChanged = !MediaOrder.SameOrder(MediaOrder.SrcList(work.Media), MediaOrder.SrcList(planned.AfterList));
            if (planned.RemovedList.Count == 0 && planned.AddedList.Count == 0 && !orderChanged)
            {
                continue;
            }

            changeList.Add(new ContentPatchChange
            {
                WorkId = work.Id,
                Channel = work.Channel,
                Title = work.Title,
                KindBefore = "json",
                KindAfter = "json",
                BeforeSnippet = FormatJsonMedia(work.Media),
                AfterSnippet = FormatJsonMedia(planned.AfterList),
                RemovedObjectList = planned.RemovedList,
                AddedObjectList = planned.AddedList,
                AfterMediaList = planned.AfterList,
                OrderChanged = orderChanged
            });
        }

        return changeList;
    }

    /// <summary>
    /// 按 works.ts 原文抽出 album / listed / cover / covered，再生成改后片段。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanWorksTs(
        string worksPath,
        IReadOnlyList<WorkCatalogItem> workList,
        IReadOnlyDictionary<string, HashSet<string>> removeDict,
        IReadOnlyDictionary<string, List<string>> addDict,
        IReadOnlyDictionary<string, string> videoLabelDict,
        IReadOnlyDictionary<string, string> sortKeyDict,
        IReadOnlyDictionary<string, List<string>> orderDict,
        IReadOnlyDictionary<string, string> photoStemDict)
    {
        var text = File.Exists(worksPath) ? File.ReadAllText(worksPath) : "";
        var blockDict = SplitWorkBlocks(text);
        var changeList = new List<ContentPatchChange>();
        foreach (var work in workList)
        {
            if (!TryGetMediaMaps(work, removeDict, addDict, orderDict, out var objectSet, out var addList, out var orderList))
            {
                continue;
            }

            blockDict.TryGetValue(WorkMapKey(work.Channel, work.Id), out var block);
            if (block == null)
            {
                blockDict.TryGetValue(work.Id, out block);
            }

            var extracted = ExtractTsMedia(block);
            var sourceList = extracted?.MediaList ?? work.Media;
            var kindBefore = extracted?.Kind ?? DetectMediaKind(block);
            var planned = PlanMedia(sourceList, objectSet, addList, videoLabelDict, sortKeyDict, orderList, work.Channel, photoStemDict);
            var orderChanged = !MediaOrder.SameOrder(MediaOrder.SrcList(sourceList), MediaOrder.SrcList(planned.AfterList));
            if (planned.RemovedList.Count == 0 && planned.AddedList.Count == 0 && !orderChanged)
            {
                continue;
            }

            if (!CanRewriteAfter(planned.AfterList))
            {
                continue;
            }

            var beforeSnippet = extracted?.Expr ?? ExtractMediaSnippet(block) ?? FormatListed(work.Media);
            changeList.Add(new ContentPatchChange
            {
                WorkId = work.Id,
                Channel = work.Channel,
                Title = work.Title,
                KindBefore = kindBefore,
                KindAfter = AfterKind(planned.AfterList),
                BeforeSnippet = beforeSnippet,
                AfterSnippet = FormatAfterExpr(planned.AfterList),
                RemovedObjectList = planned.RemovedList,
                AddedObjectList = planned.AddedList,
                AfterMediaList = planned.AfterList,
                OrderChanged = orderChanged
            });
        }

        return changeList;
    }

    /// <summary>
    /// 生成 site.ts 中 profile 与游戏图库的安全改写预览。
    /// </summary>
    private static IReadOnlyList<ContentPatchChange> PlanSiteCatalog(
        IReadOnlyList<WorkCatalogItem> workList,
        IReadOnlyDictionary<string, HashSet<string>> removeDict,
        IReadOnlyDictionary<string, List<string>> addDict,
        IReadOnlyDictionary<string, string> videoLabelDict,
        IReadOnlyDictionary<string, string> sortKeyDict,
        IReadOnlyDictionary<string, List<string>> orderDict,
        IReadOnlyDictionary<string, string> photoStemDict)
    {
        var changeList = new List<ContentPatchChange>();
        foreach (var work in workList)
        {
            if (!TryGetMediaMaps(work, removeDict, addDict, orderDict, out var objectSet, out var addList, out var orderList))
            {
                continue;
            }

            var planned = PlanMedia(work.Media, objectSet, addList, videoLabelDict, sortKeyDict, orderList, work.Channel, photoStemDict);
            var orderChanged = !MediaOrder.SameOrder(MediaOrder.SrcList(work.Media), MediaOrder.SrcList(planned.AfterList));
            if (planned.RemovedList.Count == 0 && planned.AddedList.Count == 0 && !orderChanged)
            {
                continue;
            }

            if (planned.AfterList.Count == 0)
            {
                throw new InvalidOperationException($"{work.Title} 删除后没有剩余主图，拒绝留下断引用。");
            }

            if (!work.SupportsAppend && planned.AfterList.Count > 1)
            {
                throw new InvalidOperationException($"{work.Title} 只有单图字段，不能安全追加图库图片。");
            }

            var beforePrimary = work.Media.FirstOrDefault(item => item.IsPrimary)?.Src
                ?? work.Media.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Src))?.Src
                ?? "";
            var afterPrimary = planned.AfterList.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Src))?.Src
                ?? planned.AfterList.First().Src
                ?? "";
            changeList.Add(new ContentPatchChange
            {
                WorkId = work.Id,
                Channel = work.Channel,
                Title = work.Title,
                KindBefore = work.SourceKind,
                KindAfter = work.SourceKind,
                BeforeSnippet = FormatSiteSnippet(work, beforePrimary, work.Media),
                AfterSnippet = FormatSiteSnippet(work, afterPrimary, planned.AfterList),
                RemovedObjectList = planned.RemovedList,
                AddedObjectList = planned.AddedList,
                AfterMediaList = planned.AfterList,
                OrderChanged = orderChanged
            });
        }

        return changeList;
    }

    /// <summary>
    /// 写出站点级主图与图库预览片段。
    /// </summary>
    private static string FormatSiteSnippet(
        WorkCatalogItem work,
        string primary,
        IReadOnlyList<WorkMediaItem> mediaList)
    {
        var srcList = mediaList
            .Where(item => !string.IsNullOrWhiteSpace(item.Src))
            .Select(item => item.Src!)
            .ToList();
        if (work.SourceKind == "site-profile")
        {
            return $"portraitSrc: \"{primary}\"\nportraitSrcs: ["
                + string.Join(", ", srcList.Select(src => $"\"{src}\""))
                + "]";
        }

        if (work.SupportsAppend)
        {
            return $"coverSrc: \"{primary}\"\ngallerySrcs: ["
                + string.Join(", ", srcList.Select(src => $"\"{src}\""))
                + "]";
        }

        return $"coverSrc: \"{primary}\"";
    }

    /// <summary>
    /// 先去掉引用再追加上页项，再按站点页顺序（无则保留原序），最后重排标签。
    /// </summary>
    public static (List<string> RemovedList, List<string> AddedList, List<WorkMediaItem> AfterList) PlanMedia(
        IReadOnlyList<WorkMediaItem> mediaList,
        HashSet<string>? removeSet,
        IReadOnlyList<string>? addList,
        IReadOnlyDictionary<string, string>? videoLabelDict = null,
        IReadOnlyDictionary<string, string>? sortKeyDict = null,
        IReadOnlyList<string>? orderList = null,
        string? channel = null,
        IReadOnlyDictionary<string, string>? photoStemDict = null)
    {
        var keptList = new List<WorkMediaItem>();
        var removedList = new List<string>();
        foreach (var item in mediaList)
        {
            if (IsVideo(item))
            {
                var hitSrc = item.Src != null && removeSet != null && removeSet.Contains(item.Src);
                var hitPoster = item.Poster != null && removeSet != null && removeSet.Contains(item.Poster);
                if (hitSrc || hitPoster)
                {
                    if (!string.IsNullOrWhiteSpace(item.Src))
                    {
                        removedList.Add(item.Src);
                    }

                    if (!string.IsNullOrWhiteSpace(item.Poster))
                    {
                        removedList.Add(item.Poster);
                    }

                    keptList.Add(new WorkMediaItem { Kind = "video", Label = item.Label });
                }
                else
                {
                    keptList.Add(item);
                }

                continue;
            }

            if (item.Src != null && removeSet != null && removeSet.Contains(item.Src))
            {
                removedList.Add(item.Src);
            }
            else
            {
                keptList.Add(item);
            }
        }

        var addedList = new List<string>();
        if (addList != null)
        {
            foreach (var objectKey in addList)
            {
                if (VideoEncodeRules.IsPosterObject(objectKey)
                    || keptList.Any(item => item.Src == objectKey))
                {
                    continue;
                }

                if (VideoEncodeRules.IsFormalVideoObject(objectKey))
                {
                    var poster = VideoEncodeRules.PosterObjectKey(objectKey);
                    var emptyIndex = keptList.FindIndex(item =>
                        IsVideo(item) && string.IsNullOrWhiteSpace(item.Src));
                    var sourceLabel = videoLabelDict != null
                        && videoLabelDict.TryGetValue(objectKey, out var mapped)
                        && !string.IsNullOrWhiteSpace(mapped)
                        ? mapped
                        : "视频";
                    var label = emptyIndex >= 0 ? keptList[emptyIndex].Label : sourceLabel;
                    var video = new WorkMediaItem
                    {
                        Kind = "video",
                        Label = label,
                        Src = objectKey,
                        Poster = poster
                    };
                    if (emptyIndex >= 0)
                    {
                        keptList[emptyIndex] = video;
                    }
                    else
                    {
                        keptList.Add(video);
                    }

                    addedList.Add(objectKey);
                    continue;
                }

                keptList.Add(new WorkMediaItem { Kind = "image", Label = "", Src = objectKey });
                addedList.Add(objectKey);
            }
        }

        keptList = MediaOrder.Apply(keptList, orderList);
        if (WorkRegisterRules.IsPhotoFolderChannel(channel))
        {
            return (removedList, addedList, ApplyOriginalPhotoLabels(keptList, photoStemDict));
        }

        var shouldRelabel = keptList.Count > 0 && keptList.All(item => !string.IsNullOrWhiteSpace(item.Src));
        return (removedList, addedList, shouldRelabel ? Relabel(keptList) : keptList);
    }

    private static readonly Regex EffectLabelRegex = new(
        @"^效果图\s+\d+$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 摄影栏目的自动名称用原片主文件名。空标签和「效果图 01」这类缺省前缀才替换，已写的自定义名留下。
    /// </summary>
    private static List<WorkMediaItem> ApplyOriginalPhotoLabels(
        List<WorkMediaItem> mediaList,
        IReadOnlyDictionary<string, string>? stemDict)
    {
        var afterList = new List<WorkMediaItem>(mediaList.Count);
        foreach (var item in mediaList)
        {
            if (IsVideo(item) || string.IsNullOrWhiteSpace(item.Src))
            {
                afterList.Add(item);
                continue;
            }

            var label = (item.Label ?? "").Trim();
            if (label.Length > 0 && !EffectLabelRegex.IsMatch(label))
            {
                afterList.Add(item);
                continue;
            }

            if (stemDict != null
                && stemDict.TryGetValue(item.Src, out var stem)
                && !string.IsNullOrWhiteSpace(stem))
            {
                afterList.Add(CopyWithLabel(item, stem));
                continue;
            }

            afterList.Add(item);
        }

        return afterList;
    }

    /// <summary>
    /// 原片主文件名。成片路径与空路径不用。
    /// </summary>
    private static string? OriginalMediaStem(string? sourceStageRel, string? stageRel)
    {
        foreach (var rel in new[] { sourceStageRel, stageRel })
        {
            if (string.IsNullOrWhiteSpace(rel) || IsPreparedRel(rel))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(JsonUtil.ToRel(rel).Replace('/', Path.DirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// 台账与本次意图里的原片主文件名，按正式对象键索引。
    /// </summary>
    private static Dictionary<string, string> BuildPhotoStemDict(
        IReadOnlyDictionary<string, LedgerRecord> ledgerDict,
        IReadOnlyList<IntentItem> itemList)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in ledgerDict.Values)
        {
            var stem = OriginalMediaStem(record.SourceStageRel, record.StageRel);
            if (!string.IsNullOrWhiteSpace(record.Object) && stem != null)
            {
                dict[JsonUtil.ToRel(record.Object)] = stem;
            }
        }

        foreach (var item in itemList)
        {
            var stem = OriginalMediaStem(item.SourceStageRel, item.StageRel);
            if (!string.IsNullOrWhiteSpace(item.Object) && stem != null)
            {
                dict[JsonUtil.ToRel(item.Object)] = stem;
            }
        }

        return dict;
    }

    private static WorkMediaItem CopyWithLabel(WorkMediaItem item, string label)
    {
        return new WorkMediaItem
        {
            Kind = item.Kind,
            Label = label,
            DisplayName = item.DisplayName,
            Description = item.Description,
            Stars = item.Stars,
            Src = item.Src,
            Themes = item.Themes,
            Tags = item.Tags,
            Poster = item.Poster,
            ReferenceCount = item.ReferenceCount,
            IsPrimary = item.IsPrimary
        };
    }

    /// <summary>
    /// 新追加视频的默认 label：中转站原片文件名（去扩展名）。成片路径与对象序号不用。
    /// </summary>
    public static string DefaultVideoLabel(string? sourceStageRel, string? stageRel)
    {
        foreach (var rel in new[] { sourceStageRel, stageRel })
        {
            if (string.IsNullOrWhiteSpace(rel) || IsPreparedRel(rel))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(JsonUtil.ToRel(rel).Replace('/', Path.DirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return "视频";
    }

    /// <summary>
    /// 压码成片相对路径，文件名是对象序号，不是原名。
    /// </summary>
    private static bool IsPreparedRel(string? rel)
    {
        var normalized = JsonUtil.ToRel(rel ?? "");
        return normalized.StartsWith(".site-ready/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.site-ready/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按留下的显示顺序从 01 起重写标签前缀。
    /// </summary>
    public static List<WorkMediaItem> Relabel(IReadOnlyList<WorkMediaItem> mediaList)
    {
        var prefix = InferLabelPrefix(mediaList);
        var afterList = new List<WorkMediaItem>();
        var imageIndex = 0;
        foreach (var item in mediaList)
        {
            if (IsVideo(item))
            {
                afterList.Add(item);
                continue;
            }

            imageIndex++;
            afterList.Add(new WorkMediaItem
            {
                Kind = "image",
                Label = $"{prefix} {imageIndex.ToString("00")}",
                Src = item.Src
            });
        }

        return afterList;
    }

    /// <summary>
    /// 从现有标签取出「效果图 / 照片」一类前缀。
    /// </summary>
    public static string InferLabelPrefix(IReadOnlyList<WorkMediaItem> mediaList)
    {
        foreach (var item in mediaList)
        {
            if (IsVideo(item) || string.IsNullOrWhiteSpace(item.Label))
            {
                continue;
            }

            var match = Regex.Match(item.Label.Trim(), @"^(.*?)(?:\s+)(\d+)$");
            var prefix = match.Success ? match.Groups[1].Value.Trim() : item.Label.Trim();
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                return prefix;
            }
        }

        return "效果图";
    }

    /// <summary>
    /// 意图与作品块共用的栏目+作品键；无栏目时退回作品 id。
    /// </summary>
    public static string WorkMapKey(string? channel, string workId)
    {
        return string.IsNullOrWhiteSpace(channel) ? workId : channel + "\n" + workId;
    }

    /// <summary>
    /// 先按栏目+id 取，再按裸 id 兼容旧意图。
    /// </summary>
    private static bool TryGetMapped<T>(
        IReadOnlyDictionary<string, T> dict,
        WorkCatalogItem work,
        out T? value)
    {
        if (dict.TryGetValue(WorkMapKey(work.Channel, work.Id), out value))
        {
            return true;
        }

        return dict.TryGetValue(work.Id, out value);
    }

    /// <summary>
    /// 取出该作品的隐藏、上页与排序映射；三者皆空则跳过。
    /// </summary>
    private static bool TryGetMediaMaps(
        WorkCatalogItem work,
        IReadOnlyDictionary<string, HashSet<string>> removeDict,
        IReadOnlyDictionary<string, List<string>> addDict,
        IReadOnlyDictionary<string, List<string>> orderDict,
        out HashSet<string>? objectSet,
        out List<string>? addList,
        out List<string>? orderList)
    {
        TryGetMapped(removeDict, work, out objectSet);
        TryGetMapped(addDict, work, out addList);
        TryGetMapped(orderDict, work, out orderList);
        return objectSet != null || addList != null || (orderList != null && orderList.Count > 0);
    }

    /// <summary>
    /// 作品现有 media 是否允许追加上页。图像须全有 src；上页视频时允许图像全无 src。
    /// </summary>
    public static bool CanAppendIngest(IReadOnlyList<WorkMediaItem> mediaList, bool incomingVideo = false)
    {
        var imageList = mediaList.Where(item => !IsVideo(item)).ToList();
        if (imageList.Count == 0)
        {
            return true;
        }

        var allHaveSrc = imageList.All(item => !string.IsNullOrWhiteSpace(item.Src));
        var allLackSrc = imageList.All(item => string.IsNullOrWhiteSpace(item.Src));
        return incomingVideo ? allHaveSrc || allLackSrc : allHaveSrc;
    }

    /// <summary>
    /// 抽出 works.ts 单块 media，供编目与补丁共用。
    /// </summary>
    public static IReadOnlyList<WorkMediaItem>? TryReadMediaList(string? block)
    {
        return ExtractTsMedia(block)?.MediaList;
    }

    /// <summary>
    /// 改后 media 是否可写成 listed / images / 含 video 的数组 / 空数组。
    /// 图像须全有 src 或全无 src；video 不参与该判定。
    /// </summary>
    public static bool CanRewriteAfter(IReadOnlyList<WorkMediaItem> mediaList)
    {
        var imageList = mediaList.Where(item => !IsVideo(item)).ToList();
        if (imageList.Count == 0 || imageList.All(item => !string.IsNullOrWhiteSpace(item.Src)))
        {
            return true;
        }

        return imageList.All(item => string.IsNullOrWhiteSpace(item.Src));
    }

    /// <summary>
    /// 改后片段形态：含 video 写 cover-array；全有 src 写 listed；全无 src 写 images；空数组写 empty。
    /// </summary>
    public static string AfterKind(IReadOnlyList<WorkMediaItem> mediaList)
    {
        if (mediaList.Any(IsVideo))
        {
            return "cover-array";
        }

        if (mediaList.Count == 0)
        {
            return "empty";
        }

        return mediaList.All(item => !string.IsNullOrWhiteSpace(item.Src)) ? "listed" : "images";
    }

    /// <summary>
    /// 写成 listed / images / 含 video 的数组 / 空数组，与脚本 formatAfterExpr 对齐。
    /// </summary>
    public static string FormatAfterExpr(IReadOnlyList<WorkMediaItem> mediaList)
    {
        if (mediaList.Any(IsVideo))
        {
            return FormatCoverArray(mediaList);
        }

        if (mediaList.Count == 0)
        {
            return "media: []";
        }

        if (mediaList.All(item => !string.IsNullOrWhiteSpace(item.Src)))
        {
            return "media: " + FormatListed(mediaList);
        }

        if (mediaList.Any(item => !string.IsNullOrWhiteSpace(item.Src)))
        {
            throw new InvalidOperationException("剩余媒体 src 与占位槽混排，本轮不改写。");
        }

        return "media: images(" + string.Join(", ", mediaList.Select(item => $"\"{item.Label}\"")) + ")";
    }

    /// <summary>
    /// 写成 [cover(), ...images(), { kind: video }]，按原序保留视频。
    /// </summary>
    public static string FormatCoverArray(IReadOnlyList<WorkMediaItem> mediaList)
    {
        var partList = new List<string>();
        var pendingLabelList = new List<string>();
        foreach (var item in mediaList)
        {
            if (IsVideo(item))
            {
                FlushPendingImages(partList, pendingLabelList);
                partList.Add(FormatVideoObject(item));
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Src))
            {
                pendingLabelList.Add(item.Label);
                continue;
            }

            FlushPendingImages(partList, pendingLabelList);
            if (TryParseCoverSrc(item.Src, out var channel, out var id))
            {
                partList.Add($"cover(\"{channel}\", \"{id}\", \"{item.Label}\")");
            }
            else
            {
                partList.Add("{ kind: \"image\", label: \"" + item.Label + "\", src: \"" + item.Src + "\" }");
            }
        }

        FlushPendingImages(partList, pendingLabelList);
        return "media: [" + string.Join(", ", partList) + "]";
    }

    /// <summary>
    /// 写成 listed([...]) 字面量。
    /// </summary>
    public static string FormatListed(IReadOnlyList<WorkMediaItem> mediaList)
    {
        var builder = new StringBuilder();
        builder.AppendLine("listed([");
        for (var i = 0; i < mediaList.Count; i++)
        {
            var item = mediaList[i];
            var src = item.Src ?? "";
            var comma = i + 1 < mediaList.Count ? "," : "";
            builder.AppendLine($"      [\"{src}\", \"{item.Label}\"]{comma}");
        }

        builder.Append("    ])");
        return builder.ToString();
    }

    /// <summary>
    /// 写成 JSON media 数组片段。
    /// </summary>
    public static string FormatJsonMedia(IReadOnlyList<WorkMediaItem> mediaList)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[");
        for (var i = 0; i < mediaList.Count; i++)
        {
            var item = mediaList[i];
            var comma = i + 1 < mediaList.Count ? "," : "";
            var poster = string.IsNullOrWhiteSpace(item.Poster) ? "" : $", \"poster\": \"{item.Poster}\"";
            builder.AppendLine(
                $"  {{ \"kind\": \"{item.Kind}\", \"label\": \"{item.Label}\", \"src\": \"{item.Src}\"{poster} }}{comma}");
        }

        builder.Append("]");
        return builder.ToString();
    }

    /// <summary>
    /// 内容池数组标记：空壳在 registeredWorks，隔离夹具仍用 placeholderWorks。
    /// </summary>
    private static string? ResolveWorksArrayMarker(string text)
    {
        foreach (var marker in new[]
        {
            "export const placeholderWorks",
            "export const registeredWorks",
            "const registeredWorks",
            "const lineSimulationWorks"
        })
        {
            if (text.Contains(marker, StringComparison.Ordinal))
            {
                return marker;
            }
        }

        return null;
    }

    /// <summary>
    /// 按栏目+id 切开 works.ts 作品块；裸 id 仅保留首次出现，供旧意图回退。
    /// </summary>
    private static Dictionary<string, string> SplitWorkBlocks(string text)
    {
        var blockDict = new Dictionary<string, string>(StringComparer.Ordinal);
        var marker = ResolveWorksArrayMarker(text);
        if (marker == null)
        {
            return blockDict;
        }

        var start = text.IndexOf(marker, StringComparison.Ordinal);
        var slice = text[start..];
        var idMatches = IdRegex.Matches(slice);
        for (var i = 0; i < idMatches.Count; i++)
        {
            var from = idMatches[i].Index;
            var to = i + 1 < idMatches.Count ? idMatches[i + 1].Index : slice.Length;
            var block = slice[from..to];
            var id = idMatches[i].Groups[1].Value;
            var channel = ChannelRegex.Match(block).Groups[1].Value;
            blockDict[WorkMapKey(channel, id)] = block;
            blockDict.TryAdd(id, block);
        }

        return blockDict;
    }

    /// <summary>
    /// 识别作品块里的 media 形态。
    /// </summary>
    private static string DetectMediaKind(string? block)
    {
        return ExtractTsMedia(block)?.Kind ?? "unknown";
    }

    /// <summary>
    /// 抽出 media 表达式原文。
    /// </summary>
    private static string? ExtractMediaSnippet(string? block)
    {
        return ExtractTsMedia(block)?.Expr;
    }

    /// <summary>
    /// 解析 works.ts 单块的 album / listed / covered / cover 数组。
    /// </summary>
    private static TsMediaExtract? ExtractTsMedia(string? block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return null;
        }

        var album = AlbumRegex.Match(block);
        if (album.Success)
        {
            var count = int.Parse(album.Groups[3].Value);
            var ext = album.Groups[4].Success ? album.Groups[4].Value : "jpg";
            var mediaList = new List<WorkMediaItem>();
            for (var i = 1; i <= count; i++)
            {
                var slot = i.ToString("00");
                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = $"效果图 {slot}",
                    Src = $"{album.Groups[1].Value}/{album.Groups[2].Value}/{slot}.{ext}"
                });
            }

            return new TsMediaExtract("album", album.Value, mediaList);
        }

        var listedStart = block.IndexOf("media: listed", StringComparison.Ordinal);
        if (listedStart >= 0)
        {
            var open = block.IndexOf('(', listedStart);
            var close = MatchDelimited(block, open, '(', ')');
            if (close > open)
            {
                var expr = block[listedStart..(close + 1)];
                var mediaList = ReadListedMedia(block[(open + 1)..close]);
                return new TsMediaExtract("listed", expr, mediaList);
            }
        }

        var coveredHead = CoveredHeadRegex.Match(block);
        if (coveredHead.Success)
        {
            var open = block.IndexOf('(', coveredHead.Index);
            var close = MatchDelimited(block, open, '(', ')');
            if (close > open)
            {
                var expr = block[coveredHead.Index..(close + 1)];
                var parsed = CoveredArgsRegex.Match(expr);
                if (parsed.Success)
                {
                    var labels = Regex.Matches(parsed.Groups[3].Value, "\"([^\"]+)\"")
                        .Select(match => match.Groups[1].Value)
                        .ToList();
                    var mediaList = labels.Select((label, index) => new WorkMediaItem
                    {
                        Kind = "image",
                        Label = label,
                        Src = index == 0 ? $"{parsed.Groups[1].Value}/{parsed.Groups[2].Value}/01.webp" : null
                    }).ToList();
                    return new TsMediaExtract("covered", expr, mediaList);
                }
            }
        }

        var arrayHead = CoverArrayHeadRegex.Match(block);
        if (arrayHead.Success)
        {
            var open = block.IndexOf('[', arrayHead.Index);
            var close = MatchDelimited(block, open, '[', ']');
            if (close > open)
            {
                var inner = block[(open + 1)..close];
                var mediaList = ParseCoverArray(inner);
                if (mediaList != null)
                {
                    return new TsMediaExtract("cover-array", block[arrayHead.Index..(close + 1)], mediaList);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 解析 [cover(), ...images(), { kind: video }]。
    /// </summary>
    private static List<WorkMediaItem>? ParseCoverArray(string inner)
    {
        if (string.IsNullOrWhiteSpace(inner))
        {
            return new List<WorkMediaItem>();
        }

        var mediaList = new List<WorkMediaItem>();
        var found = false;
        foreach (Match match in CoverTokenRegex.Matches(inner))
        {
            found = true;
            if (match.Groups[1].Success)
            {
                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = match.Groups[3].Value,
                    Src = $"{match.Groups[1].Value}/{match.Groups[2].Value}/01.webp"
                });
            }
            else if (match.Groups[5].Success)
            {
                var fields = ParseObjectFields(match.Groups[6].Value);
                var body = match.Groups[6].Value;
                mediaList.Add(new WorkMediaItem
                {
                    Kind = match.Groups[5].Value,
                    Label = fields.Label,
                    Src = fields.Src,
                    Poster = fields.Poster,
                    Stars = ReadObjectStars(body),
                    Themes = ReadObjectStringArray(body, "themes"),
                    Tags = ReadObjectStringArray(body, "tags")
                });
            }
            else
            {
                foreach (Match label in Regex.Matches(match.Groups[4].Value, "\"([^\"]+)\""))
                {
                    mediaList.Add(new WorkMediaItem
                    {
                        Kind = "image",
                        Label = label.Groups[1].Value
                    });
                }
            }
        }

        return found ? mediaList : null;
    }

    /// <summary>
    /// 把连续无 src 图像收成 ...images()。
    /// </summary>
    private static void FlushPendingImages(List<string> partList, List<string> pendingLabelList)
    {
        if (pendingLabelList.Count == 0)
        {
            return;
        }

        partList.Add("...images(" + string.Join(", ", pendingLabelList.Select(label => $"\"{label}\"")) + ")");
        pendingLabelList.Clear();
    }

    /// <summary>
    /// cover() 对象键：{channel}/{id}/01.webp。
    /// </summary>
    private static bool TryParseCoverSrc(string src, out string channel, out string id)
    {
        var match = CoverSrcRegex.Match(src);
        channel = match.Success ? match.Groups[1].Value : "";
        id = match.Success ? match.Groups[2].Value : "";
        return match.Success;
    }

    /// <summary>
    /// 写成带 src / poster 的 video 对象；无 src 时只保留占位。
    /// </summary>
    private static string FormatVideoObject(WorkMediaItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Src))
        {
            return "{ kind: \"video\", label: \"" + item.Label + "\" }";
        }

        var poster = string.IsNullOrWhiteSpace(item.Poster) ? "" : $", poster: \"{item.Poster}\"";
        return "{ kind: \"video\", src: \"" + item.Src + "\"" + poster + ", label: \"" + item.Label + "\" }";
    }

    /// <summary>
    /// 从对象字面量抽出 src / poster / label。
    /// </summary>
    private static (string? Src, string? Poster, string Label) ParseObjectFields(string body)
    {
        return (ReadObjectField(body, "src"), ReadObjectField(body, "poster"), ReadObjectField(body, "label") ?? "");
    }

    /// <summary>
    /// 读取对象字段字符串。
    /// </summary>
    private static string? ReadObjectField(string body, string name)
    {
        var match = Regex.Match(body, @"[""']?" + Regex.Escape(name) + @"[""']?\s*:\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 读 1～5 星。缺字段或 0 视为未写。
    /// </summary>
    private static int? ReadObjectStars(string body)
    {
        var match = Regex.Match(body, @"[""']?stars[""']?\s*:\s*([0-9]+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var stars) || stars is < 1 or > 5)
        {
            return null;
        }

        return stars;
    }

    /// <summary>
    /// 读取对象里的字符串数组。键可以带引号。
    /// </summary>
    private static List<string> ReadObjectStringArray(string body, string name)
    {
        var match = Regex.Match(body, @"[""']?" + Regex.Escape(name) + @"[""']?\s*:\s*\[([^\]]*)\]");
        if (!match.Success)
        {
            return new List<string>();
        }

        var valueList = new List<string>();
        foreach (Match item in Regex.Matches(match.Groups[1].Value, @"""([^""]+)"""))
        {
            valueList.Add(item.Groups[1].Value);
        }

        return valueList;
    }

    /// <summary>
    /// 是否为视频条目。
    /// </summary>
    private static bool IsVideo(WorkMediaItem item)
    {
        return string.Equals(item.Kind, "video", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按源码顺序读 listed 里的二元组与带 src 的对象。
    /// </summary>
    private static List<WorkMediaItem> ReadListedMedia(string body)
    {
        var mediaList = new List<WorkMediaItem>();
        var index = 0;
        while (index < body.Length)
        {
            var tupleAt = body.IndexOf('[', index);
            var objectAt = body.IndexOf('{', index);
            if (tupleAt < 0 && objectAt < 0)
            {
                break;
            }

            var useObject = objectAt >= 0 && (tupleAt < 0 || objectAt < tupleAt);
            if (useObject)
            {
                var objectEnd = MatchDelimited(body, objectAt, '{', '}');
                if (objectEnd < 0)
                {
                    break;
                }

                AddObjectMedia(body[objectAt..(objectEnd + 1)], mediaList);
                index = objectEnd + 1;
                continue;
            }

            var tupleEnd = MatchDelimited(body, tupleAt, '[', ']');
            if (tupleEnd < 0)
            {
                break;
            }

            var tuple = body[tupleAt..(tupleEnd + 1)];
            var match = Regex.Match(tuple, @"\[\s*""([^""]+)""\s*,\s*""([^""]*)""\s*\]");
            if (match.Success)
            {
                mediaList.Add(new WorkMediaItem
                {
                    Kind = "image",
                    Label = match.Groups[2].Value,
                    Src = JsonUtil.ToRel(match.Groups[1].Value)
                });
            }

            index = tupleEnd + 1;
        }

        return mediaList;
    }

    /// <summary>
    /// 从媒体对象读出 src、类型与自由标签。
    /// </summary>
    private static void AddObjectMedia(string obj, List<WorkMediaItem> mediaList)
    {
        var srcMatch = Regex.Match(obj, @"[""']?src[""']?\s*:\s*""([^""]+)""");
        if (!srcMatch.Success)
        {
            return;
        }

        var labelMatch = Regex.Match(obj, @"[""']?label[""']?\s*:\s*""([^""]*)""");
        var kindMatch = Regex.Match(obj, @"[""']?kind[""']?\s*:\s*""([^""]+)""");
        mediaList.Add(new WorkMediaItem
        {
            Kind = kindMatch.Success ? kindMatch.Groups[1].Value : "image",
            Label = labelMatch.Success ? labelMatch.Groups[1].Value : "",
            Src = JsonUtil.ToRel(srcMatch.Groups[1].Value),
            Themes = ReadQuotedArray(obj, "themes"),
            Tags = ReadQuotedArray(obj, "tags")
        });
    }

    /// <summary>
    /// 读出对象里的字符串数组；没有该字段则空。
    /// </summary>
    private static List<string> ReadQuotedArray(string obj, string property)
    {
        var head = Regex.Match(obj, @"[""']?" + Regex.Escape(property) + @"[""']?\s*:\s*\[");
        if (!head.Success)
        {
            return new List<string>();
        }

        var open = obj.IndexOf('[', head.Index);
        var close = MatchDelimited(obj, open, '[', ']');
        if (close < 0)
        {
            return new List<string>();
        }

        return Regex.Matches(obj[(open + 1)..close], "\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    /// <summary>
    /// 按开闭符匹配表达式终点。
    /// </summary>
    private static int MatchDelimited(string text, int openIndex, char openChar, char closeChar)
    {
        if (openIndex < 0)
        {
            return -1;
        }

        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == openChar)
            {
                depth++;
            }
            else if (text[i] == closeChar)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private sealed record TsMediaExtract(string Kind, string Expr, List<WorkMediaItem> MediaList);
}
