using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 由标记条目生成意图文件。
/// </summary>
public static class IntentDocumentBuilder
{
    /// <summary>
    /// 组装一份意图文档。
    /// </summary>
    public static IntentDocument Build(
        WorkspaceSession session,
        ExecutionMode mode,
        IEnumerable<(MediaIntent Intent, StageItem? Stage, SiteItem? Site)> markedItems,
        string? ingestTargetWorkId = null)
    {
        return Build(
            session,
            mode,
            markedItems.Select(item => new MarkedMedia(item.Intent, item.Stage, item.Site)),
            ingestTargetWorkId);
    }

    /// <summary>
    /// 组装一份意图文档；条目可带打标时的作品编号，避免切项目后误用当前下拉。
    /// </summary>
    public static IntentDocument Build(
        WorkspaceSession session,
        ExecutionMode mode,
        IEnumerable<MarkedMedia> markedItems,
        string? ingestTargetWorkId = null)
    {
        var document = new IntentDocument
        {
            Version = 1,
            WorkspaceRoot = session.Profile.ResolvedRoot,
            ProfileName = session.Profile.Name,
            Mode = mode == ExecutionMode.Direct ? "direct" : "prompt",
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Options = new IntentOptions { Relabel = true, Deploy = "none" }
        };

        var reservedSet = new HashSet<string>(StringComparer.Ordinal);
        var ingestContext = new IntentContext
        {
            LedgerDict = session.LedgerDict,
            ContentRefCountDict = session.ContentRefCountDict,
            Profile = session.Profile,
            TargetWorkId = ingestTargetWorkId,
            WorkList = session.Works
        };
        foreach (var marked in markedItems)
        {
            var intent = marked.Intent;
            var stage = marked.Stage;
            var site = marked.Site;
            if (intent == MediaIntent.None)
            {
                continue;
            }

            var workId = site?.WorkId ?? marked.WorkId ?? ingestTargetWorkId ?? stage?.WorkIdGuess;
            var objectKey = site?.ObjectKey ?? stage?.MatchedObject;
            // 已上页只保留原键，不占新序号，避免内容补丁写成空槽。
            if (intent == MediaIntent.StageIngest
                && stage != null
                && !string.IsNullOrWhiteSpace(workId)
                && !IntentGate.IsRedundantIngest(stage, ingestContext))
            {
                objectKey = ObjectKeyAllocator.Allocate(
                    session,
                    stage.ChannelKey,
                    workId,
                    WebpPrepareRules.AllocateExtension(stage, session.Profile),
                    stage.StageRel,
                    document.Options.Bump,
                    reservedSet);
            }

            document.Items.Add(new IntentItem
            {
                Intent = MediaIntentCodes.ToCode(intent),
                WorkId = workId,
                Channel = site?.ChannelKey ?? stage?.ChannelKey,
                Object = objectKey,
                StageRel = stage?.StageRel ?? site?.StageRel,
                LabelBefore = site?.Label
            });
        }

        return document;
    }

    /// <summary>
    /// 组装一条登记空壳意图。
    /// </summary>
    public static IntentDocument BuildRegister(
        WorkspaceSession session,
        ExecutionMode mode,
        string channel,
        string workId,
        string title,
        string stageFolder,
        string? startedOn = null,
        string? place = null,
        IReadOnlyList<string>? themes = null)
    {
        var document = Build(
            session,
            mode,
            Array.Empty<(MediaIntent, StageItem?, SiteItem?)>());
        var folderRel = JsonUtil.ToRel(stageFolder);
        var startedOk = WorkRegisterRules.TryNormalizeStartedOn(startedOn, out var started);
        var placeOk = WorkRegisterRules.TryNormalizePlace(place, out var city);
        document.Items.Add(new IntentItem
        {
            Intent = MediaIntentCodes.WorkRegister,
            Channel = channel,
            WorkId = workId,
            Title = title,
            StageFolder = folderRel,
            StageRel = folderRel,
            StartedOn = string.IsNullOrWhiteSpace(startedOn)
                ? null
                : startedOk ? started : startedOn.Trim(),
            Place = string.IsNullOrWhiteSpace(place)
                ? null
                : placeOk ? city : place.Trim(),
            Year = PhotoFactRules.RequiresFacts(channel)
                ? PhotoFactRules.TryReadExplicitYear(startedOk ? started : null, null, out var explicitYear)
                    ? explicitYear
                    : null
                : WorkRegisterRules.SupportsScheduleFields(channel)
                    ? WorkRegisterRules.YearFromStartedOn(startedOk ? started : null)
                    : null,
            Themes = PhotoFactRules.NormalizeThemes(themes) is { Count: > 0 } themeList ? themeList : null
        });
        return document;
    }

    /// <summary>
    /// 组装一条已入编作品的题名 / 开始日 / 地点改写。
    /// </summary>
    public static IntentDocument BuildUpdate(
        WorkspaceSession session,
        ExecutionMode mode,
        WorkCatalogItem work,
        string title,
        string? startedOn = null,
        string? place = null)
    {
        var document = Build(
            session,
            mode,
            Array.Empty<(MediaIntent, StageItem?, SiteItem?)>());
        var startedOk = WorkRegisterRules.TryNormalizeStartedOn(startedOn, out var started);
        var placeOk = WorkRegisterRules.TryNormalizePlace(place, out var city);
        var allowSchedule = WorkRegisterRules.SupportsScheduleFields(work.Channel);
        document.Items.Add(new IntentItem
        {
            Intent = MediaIntentCodes.WorkUpdate,
            Channel = work.Channel,
            WorkId = work.Id,
            Title = title,
            StageFolder = string.IsNullOrWhiteSpace(work.StageFolder) ? null : JsonUtil.ToRel(work.StageFolder),
            StartedOn = !allowSchedule || string.IsNullOrWhiteSpace(startedOn)
                ? null
                : startedOk ? started : startedOn.Trim(),
            Place = !allowSchedule || string.IsNullOrWhiteSpace(place)
                ? null
                : placeOk ? city : place.Trim(),
            Year = allowSchedule
                ? WorkRegisterRules.YearFromStartedOn(startedOk ? started : null, work.Year)
                : null
        });
        return document;
    }

    /// <summary>
    /// 组装一条类型 / 自由标签写入。对象列表为空时写作品级。
    /// </summary>
    public static IntentDocument BuildTags(
        WorkspaceSession session,
        ExecutionMode mode,
        WorkCatalogItem work,
        IReadOnlyList<string>? objectKeyList,
        IReadOnlyList<string>? themes,
        IReadOnlyList<string>? tags)
    {
        var document = Build(
            session,
            mode,
            Array.Empty<(MediaIntent, StageItem?, SiteItem?)>());
        var keys = (objectKeyList ?? Array.Empty<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(JsonUtil.ToRel)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        document.Items.Add(new IntentItem
        {
            Intent = MediaIntentCodes.TagsUpdate,
            Channel = work.Channel,
            WorkId = work.Id,
            ObjectList = keys.Count == 0 ? null : keys,
            Themes = themes?.ToList(),
            Tags = tags?.ToList()
        });
        return document;
    }

    /// <summary>
    /// 把多帧标签收成若干条 tags.update。同一作品、同一结果合并对象键。
    /// </summary>
    public static IntentDocument BuildTagFrames(
        WorkspaceSession session,
        ExecutionMode mode,
        string channel,
        IReadOnlyList<TagFrameWrite> writes)
    {
        var document = Build(
            session,
            mode,
            Array.Empty<(MediaIntent, StageItem?, SiteItem?)>());
        var groups = new Dictionary<string, TagFrameGroup>(StringComparer.Ordinal);
        foreach (var write in writes)
        {
            var themes = PhotoFactRules.NormalizeThemes(write.Themes);
            PhotoFactRules.TryNormalizeTags(write.Tags, out var tags, out _);
            var workLevel = string.IsNullOrWhiteSpace(write.ObjectKey);
            var signature = write.WorkId
                + "\n"
                + (workLevel ? "w" : "f")
                + "\n"
                + string.Join("\u001f", themes)
                + "\n"
                + string.Join("\u001f", tags);
            if (!groups.TryGetValue(signature, out var group))
            {
                group = new TagFrameGroup(write.WorkId, workLevel ? null : new List<string>(), themes, tags);
                groups[signature] = group;
            }

            if (workLevel || group.ObjectKeyList == null)
            {
                continue;
            }

            var key = JsonUtil.ToRel(write.ObjectKey!);
            if (!group.ObjectKeyList.Contains(key, StringComparer.Ordinal))
            {
                group.ObjectKeyList.Add(key);
            }
        }

        foreach (var group in groups.Values)
        {
            document.Items.Add(new IntentItem
            {
                Intent = MediaIntentCodes.TagsUpdate,
                Channel = channel,
                WorkId = group.WorkId,
                ObjectList = group.ObjectKeyList,
                Themes = group.ObjectKeyList == null ? null : group.Themes,
                Tags = group.Tags
            });
        }

        return document;
    }

    /// <summary>
    /// 组装一批路径重挂意图。
    /// </summary>
    public static IntentDocument BuildRelocate(
        WorkspaceSession session,
        ExecutionMode mode,
        IEnumerable<IntentItem> relocateItemList)
    {
        var document = Build(
            session,
            mode,
            Array.Empty<(MediaIntent, StageItem?, SiteItem?)>());
        foreach (var item in relocateItemList)
        {
            document.Items.Add(item);
        }

        return document;
    }

    /// <summary>
    /// 把脏星级草稿编成的条追加进同一份意图。
    /// </summary>
    public static void AppendStars(IntentDocument document, IEnumerable<IntentItem> starItemList)
    {
        foreach (var item in starItemList)
        {
            if (item.Intent == MediaIntentCodes.StarsUpdate)
            {
                document.Items.Add(item);
            }
        }
    }

    /// <summary>
    /// 把脏草稿编成的文案条追加进同一份意图。
    /// </summary>
    public static void AppendCopy(IntentDocument document, IEnumerable<IntentItem> copyItemList)
    {
        foreach (var item in copyItemList)
        {
            if (item.Intent == MediaIntentCodes.CopyUpdate)
            {
                document.Items.Add(item);
            }
        }
    }

    /// <summary>
    /// 组装整项撤下：本机删除内容记录，对象键留到发布。
    /// </summary>
    public static IntentDocument BuildWorkWithdraw(
        WorkspaceSession session,
        ExecutionMode mode,
        string channel,
        string workId)
    {
        var document = Build(session, mode, Array.Empty<(MediaIntent, StageItem?, SiteItem?)>());
        var work = WorkWithdrawRules.FindWork(session.Works, channel, workId);
        document.Items.Add(new IntentItem
        {
            Intent = MediaIntentCodes.WorkWithdraw,
            Channel = channel,
            WorkId = workId,
            ObjectList = work == null
                ? new List<string>()
                : WorkWithdrawRules.CollectObjectKeys(
                    work,
                    session.LedgerDict,
                    WorkWithdrawRules.ResolvePlaceholders(session.Profile))
        });
        return document;
    }

    /// <summary>
    /// 把脏排序草稿编成的条追加进同一份意图。
    /// </summary>
    public static void AppendReorder(IntentDocument document, IEnumerable<IntentItem> reorderItemList)
    {
        foreach (var item in reorderItemList)
        {
            if (item.Intent == MediaIntentCodes.MediaReorder)
            {
                document.Items.Add(item);
            }
        }
    }

    /// <summary>
    /// 序列化为缩进 JSON。
    /// </summary>
    public static string ToJson(IntentDocument document)
    {
        return JsonSerializer.Serialize(document, JsonUtil.Options);
    }

    /// <summary>
    /// 从意图文件读取文档。
    /// </summary>
    public static IntentDocument FromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("找不到意图文件。", path);
        }

        return FromJson(File.ReadAllText(path));
    }

    /// <summary>
    /// 从 JSON 文本读取文档。
    /// </summary>
    public static IntentDocument FromJson(string json)
    {
        var document = JsonSerializer.Deserialize<IntentDocument>(json, JsonUtil.Options);
        if (document == null)
        {
            throw new JsonException("意图文件无法解析。");
        }

        return document;
    }

    /// <summary>
    /// 同一结果的帧合并成一条意图。
    /// </summary>
    private sealed class TagFrameGroup
    {
        public TagFrameGroup(string workId, List<string>? objectKeyList, List<string> themes, List<string> tags)
        {
            WorkId = workId;
            ObjectKeyList = objectKeyList;
            Themes = themes;
            Tags = tags;
        }

        public string WorkId { get; }

        public List<string>? ObjectKeyList { get; }

        public List<string> Themes { get; }

        public List<string> Tags { get; }
    }
}
