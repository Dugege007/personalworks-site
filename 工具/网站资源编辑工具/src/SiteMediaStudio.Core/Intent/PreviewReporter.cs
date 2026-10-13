namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 对意图文档逐条过闸门。
/// </summary>
public static class PreviewReporter
{
    /// <summary>
    /// 生成登记空壳的预演报告。
    /// </summary>
    public static PreviewReport BuildRegister(WorkspaceSession session, IntentDocument document)
    {
        var resolvedList = document.Items
            .Select(item => (item, (StageItem?)null, (SiteItem?)null))
            .ToList();
        return Build(session, document, resolvedList, null);
    }

    /// <summary>
    /// 生成路径重挂的预演报告。
    /// </summary>
    public static PreviewReport BuildRelocate(WorkspaceSession session, IntentDocument document)
    {
        var resolvedList = document.Items
            .Select(item => (item, (StageItem?)null, (SiteItem?)null))
            .ToList();
        return Build(session, document, resolvedList, null);
    }

    /// <summary>
    /// 从已写盘的意图文档生成预演，绑回当前编目。
    /// </summary>
    public static PreviewReport BuildFromDocument(WorkspaceSession session, IntentDocument document)
    {
        var resolvedList = IntentCatalogBinder.Bind(session, document);
        return Build(session, document, resolvedList, IntentCatalogBinder.ReadIngestTargetWorkId(document));
    }

    /// <summary>
    /// 生成预演报告。
    /// </summary>
    public static PreviewReport Build(
        WorkspaceSession session,
        IntentDocument document,
        IReadOnlyList<(IntentItem Item, StageItem? Stage, SiteItem? Site)> resolvedItems,
        string? ingestTargetWorkId)
    {
        var batchRemoveCountDict = CountUniqueWorkRemoves(session, resolvedItems.Select(item => item.Item));

        var context = new IntentContext
        {
            LedgerDict = session.LedgerDict,
            ContentRefCountDict = session.ContentRefCountDict,
            Profile = session.Profile,
            TargetWorkId = ingestTargetWorkId,
            WorkList = session.Works,
            BatchRemoveCountDict = batchRemoveCountDict
        };

        var lineList = new List<PreviewLine>();
        foreach (var (item, stage, site) in resolvedItems)
        {
            var intent = MediaIntentCodes.FromCode(item.Intent);
            GateDecision decision;
            if (intent is MediaIntent.StageIngest or MediaIntent.StageRecycle)
            {
                decision = stage == null
                    ? GateDecision.Reject("找不到投放箱编目条目。")
                    : IntentGate.EvaluateStage(intent, stage, context);
            }
            else if (intent is MediaIntent.SiteHide or MediaIntent.SiteRestore or MediaIntent.SiteWithdraw)
            {
                decision = site == null
                    ? GateDecision.Reject("找不到站点编目条目。")
                    : IntentGate.EvaluateSite(intent, site, context);
            }
            else if (intent == MediaIntent.WorkRegister)
            {
                decision = IntentGate.EvaluateRegister(item, context);
            }
            else if (intent == MediaIntent.WorkUpdate)
            {
                decision = IntentGate.EvaluateUpdate(item, context);
            }
            else if (intent == MediaIntent.WorkWithdraw)
            {
                decision = IntentGate.EvaluateWorkWithdraw(item, context);
            }
            else if (intent == MediaIntent.StageRelocate)
            {
                decision = IntentGate.EvaluateRelocate(item, context);
            }
            else if (intent == MediaIntent.CopyUpdate)
            {
                decision = IntentGate.EvaluateCopy(item, context);
            }
            else if (intent == MediaIntent.MediaReorder)
            {
                decision = IntentGate.EvaluateReorder(item, context);
            }
            else if (intent == MediaIntent.TagsUpdate)
            {
                decision = IntentGate.EvaluateTags(item, context);
            }
            else if (intent == MediaIntent.StarsUpdate)
            {
                decision = IntentGate.EvaluateStars(item, context);
            }
            else
            {
                decision = GateDecision.Reject("找不到对应编目条目。");
            }

            if (document.Mode == "direct"
                && intent is MediaIntent.StageIngest or MediaIntent.SiteWithdraw or MediaIntent.WorkWithdraw
                && decision.Allowed
                && !IntentGate.IsRedundantIngestDecision(decision)
                && ToolPaths.FindSitemediaScript(session.Profile) == null)
            {
                decision = GateDecision.Reject("找不到 sitemedia.mjs，机械上页 / 撤下不可用。");
            }

            if (document.Mode == "direct"
                && intent == MediaIntent.StageIngest
                && decision.Allowed
                && !IntentGate.IsRedundantIngestDecision(decision)
                && stage != null
                && WebpPrepareRules.NeedsPrepare(stage, session.Profile))
            {
                if (ToolPaths.FindPrepareScript() == null)
                {
                    decision = GateDecision.Reject("找不到压图脚本 prepare-initial-batch.py。");
                }
                else if (!PythonHost.CanRunPrepare())
                {
                    decision = GateDecision.Reject("找不到可用的 Python（须已安装 Pillow）。");
                }
            }

            if (document.Mode == "direct"
                && intent == MediaIntent.StageIngest
                && decision.Allowed
                && !IntentGate.IsRedundantIngestDecision(decision)
                && stage != null
                && VideoEncodeRules.NeedsEncode(stage, session.Profile))
            {
                try
                {
                    var settings = AppSettingsStore.Load();
                    if (!VideoEncodeRules.TryAudioBitrate(settings.VideoAudioBitrateKbps, out _, out var bitrateText))
                    {
                        decision = GateDecision.Reject(bitrateText ?? "音频码率不在允许范围。");
                    }
                    else if (!VideoEncoderHost.CanEncode(settings))
                    {
                        decision = GateDecision.Reject(VideoEncodeRules.MissingEncoderMessage);
                    }
                }
                catch (AppSettingsException ex)
                {
                    decision = GateDecision.Reject(ex.Message);
                }
            }

            lineList.Add(new PreviewLine { Item = item, Decision = ApplySoftWarnings(decision, stage, site) });
        }

        ApplyNoteIngestBatch(session, lineList);
        RejectSharedWithdrawKeys(session, lineList);
        RejectDuplicateRelocateAfters(lineList);
        var allowedPatchList = lineList
            .Where(line => line.Decision.Allowed
                && !IntentGate.IsRedundantSkipDecision(line.Decision)
                && MediaIntentCodes.FromCode(line.Item.Intent) is MediaIntent.SiteHide or MediaIntent.SiteRestore or MediaIntent.SiteWithdraw or MediaIntent.StageIngest or MediaIntent.WorkRegister or MediaIntent.WorkUpdate or MediaIntent.CopyUpdate or MediaIntent.MediaReorder or MediaIntent.TagsUpdate or MediaIntent.StarsUpdate)
            .Select(line => line.Item)
            .ToList();
        var patchPreview = allowedPatchList.Count == 0
            ? ""
            : ContentPatchPlanner.RenderFragments(ContentPatchPlanner.Plan(session, allowedPatchList));
        var relocatePreview = RenderRelocatePreview(lineList);
        if (!string.IsNullOrWhiteSpace(relocatePreview))
        {
            patchPreview = string.IsNullOrWhiteSpace(patchPreview)
                ? relocatePreview
                : patchPreview.TrimEnd() + "\n\n" + relocatePreview;
        }

        var withdrawPreview = RenderWorkWithdrawPreview(session, lineList);
        if (!string.IsNullOrWhiteSpace(withdrawPreview))
        {
            patchPreview = string.IsNullOrWhiteSpace(patchPreview)
                ? withdrawPreview
                : patchPreview.TrimEnd() + "\n\n" + withdrawPreview;
        }

        return new PreviewReport
        {
            Document = document,
            Lines = lineList,
            PatchPreview = patchPreview
        };
    }

    /// <summary>
    /// 心得配图必须和正文.md 同一批上页。只标记配图则整批拒绝。
    /// 标记了正文时按上页计划展开配图：GIF 明确拒绝，正文本身不作为配图列出。
    /// </summary>
    private static void ApplyNoteIngestBatch(WorkspaceSession session, List<PreviewLine> lineList)
    {
        var channel = session.Profile.Channels.FirstOrDefault(item => NoteRules.IsNotesChannel(item.Key));
        if (channel == null)
        {
            return;
        }

        var folder = WorkspaceProfileLoader.ChannelStageRelative(session.Profile, channel);
        var noteLines = lineList
            .Where(line => MediaIntentCodes.FromCode(line.Item.Intent) == MediaIntent.StageIngest
                && NoteRules.IsNotesChannel(line.Item.Channel))
            .ToList();
        var groups = noteLines.GroupBy(line => NoteRules.NoteFolderFromStageRel(line.Item.StageRel, folder) ?? "");
        foreach (var group in groups)
        {
            var members = group.ToList();
            var hasBody = members.Any(line => NoteRules.IsBodyStageRel(line.Item.StageRel));
            if (!hasBody)
            {
                ReplaceNoteDecisions(lineList, members, GateDecision.Reject("须标记正文.md 才能上页这篇心得，不能只标记配图。"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(group.Key))
            {
                ReplaceNoteDecisions(lineList, members, GateDecision.Reject("找不到这篇心得的夹。"));
                continue;
            }

            var replacement = BuildNotePublishLines(session, folder, group.Key, members);
            ReplaceNoteLines(lineList, members, replacement);
        }
    }

    /// <summary>
    /// 用上页计划替换这一夹的预演行。成功时每张配图一行。失败且计划返回了导致拒绝的文件时，拒绝行改用该文件，其余配图只在原因里标明本身没问题。
    /// </summary>
    private static List<PreviewLine> BuildNotePublishLines(
        WorkspaceSession session,
        string channelFolder,
        string folderName,
        List<PreviewLine> members)
    {
        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(session.Profile, session.Profile.StageRoot);
        var folderFull = Path.Combine(
            stageRoot,
            channelFolder.Replace('/', Path.DirectorySeparatorChar),
            folderName);
        var body = members.First(line => NoteRules.IsBodyStageRel(line.Item.StageRel));
        NoteIndexFile index;
        try
        {
            index = NoteIndexStore.Load(session.Profile);
        }
        catch (NoteIndexCorruptException ex)
        {
            return NoteLinesFor(body, folderName, Array.Empty<NoteImageJob>(), NoteRules.ListPublishImageStageRels(folderFull, stageRoot), GateDecision.Reject(ex.Message));
        }

        var marked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in members)
        {
            if (string.IsNullOrWhiteSpace(line.Item.StageRel) || NoteRules.IsBodyStageRel(line.Item.StageRel))
            {
                continue;
            }

            marked.Add(JsonUtil.ToRel(line.Item.StageRel));
        }

        var plan = NoteRules.Plan(folderFull, stageRoot, index, marked.Count == 0 ? null : marked, null);
        if (!plan.Ok && !string.IsNullOrWhiteSpace(plan.BlockingReference))
        {
            return NoteLinesForBlockingFile(
                body,
                folderName,
                channelFolder,
                plan.BlockingReference,
                NoteRules.ListPublishImageStageRels(folderFull, stageRoot),
                plan.Error);
        }

        var decision = plan.Ok
            ? GateDecision.Allow("将上页整篇心得，与右键「上页心得」相同。")
            : GateDecision.Reject(plan.Error);
        var fallback = plan.Ok
            ? Array.Empty<string>()
            : NoteRules.ListPublishImageStageRels(folderFull, stageRoot);
        return NoteLinesFor(body, folderName, plan.Images, fallback, decision);
    }

    /// <summary>
    /// 整篇因某一个文件被拒绝时，拒绝行指向这个文件。可上页的配图不单独占一行，只在原因里说明本身没问题。
    /// </summary>
    private static List<PreviewLine> NoteLinesForBlockingFile(
        PreviewLine body,
        string folderName,
        string channelFolder,
        string blockingReference,
        IReadOnlyList<string> innocentStageRels,
        string error)
    {
        var innocentNames = innocentStageRels
            .Select(item => NoteRelativeName(item, channelFolder, folderName))
            .Where(name => name.Length > 0)
            .ToList();
        var message = error + "。这个文件导致整篇被拒绝。";
        if (innocentNames.Count > 0)
        {
            message += string.Join("、", innocentNames) + " 本身没问题，因整篇被拒绝而不会上页。";
        }

        return new List<PreviewLine>
        {
            NoteImageLine(body, folderName, BlockingStageRel(channelFolder, folderName, blockingReference), null, GateDecision.Reject(message))
        };
    }

    /// <summary>
    /// 把导致拒绝的引用写成投放路径，供预演行显示相对路径。网址保持原样。
    /// </summary>
    private static string BlockingStageRel(string channelFolder, string folderName, string reference)
    {
        var text = reference.Trim();
        if (text.Contains("://", StringComparison.Ordinal))
        {
            return text;
        }

        var relative = text.Replace('\\', '/').TrimStart('/');
        return JsonUtil.ToRel(channelFolder + "/" + folderName + "/" + relative);
    }

    /// <summary>
    /// 配图路径相对这篇笔记文件夹，用来在原因里区分同名文件。
    /// </summary>
    private static string NoteRelativeName(string stageRel, string channelFolder, string folderName)
    {
        var prefix = JsonUtil.ToRel(channelFolder + "/" + folderName) + "/";
        var rel = JsonUtil.ToRel(stageRel);
        if (rel.StartsWith(prefix, StringComparison.Ordinal))
        {
            return rel[prefix.Length..];
        }

        var name = Path.GetFileName(rel);
        return string.IsNullOrWhiteSpace(name) ? rel : name;
    }

    /// <summary>
    /// 把计划中的配图写成预演行。没有配图时保留一篇的结论，目标用夹名，不用正文的图片对象键。
    /// </summary>
    private static List<PreviewLine> NoteLinesFor(
        PreviewLine body,
        string folderName,
        IReadOnlyList<NoteImageJob> images,
        IReadOnlyList<string> fallbackStageRels,
        GateDecision decision)
    {
        var lineList = new List<PreviewLine>();
        if (images.Count > 0)
        {
            foreach (var image in images)
            {
                lineList.Add(NoteImageLine(body, folderName, image.SourceStageRel, image.ObjectKey, decision));
            }

            return lineList;
        }

        foreach (var stageRel in fallbackStageRels)
        {
            lineList.Add(NoteImageLine(body, folderName, stageRel, null, decision));
        }

        if (lineList.Count > 0)
        {
            return lineList;
        }

        lineList.Add(new PreviewLine
        {
            Item = new IntentItem
            {
                Intent = body.Item.Intent,
                Channel = body.Item.Channel,
                WorkId = string.IsNullOrWhiteSpace(body.Item.WorkId) ? folderName : body.Item.WorkId,
                StageRel = body.Item.StageRel
            },
            Decision = decision
        });
        return lineList;
    }

    /// <summary>
    /// 一张配图的预演行。对象键用上页计划的正式键；投放路径保留原文件名，供界面列出 ok.png 而不是正文。
    /// </summary>
    private static PreviewLine NoteImageLine(
        PreviewLine body,
        string folderName,
        string stageRel,
        string? objectKey,
        GateDecision decision)
    {
        return new PreviewLine
        {
            Item = new IntentItem
            {
                Intent = body.Item.Intent,
                Channel = body.Item.Channel ?? NoteRules.ChannelKey,
                WorkId = string.IsNullOrWhiteSpace(body.Item.WorkId) ? folderName : body.Item.WorkId,
                StageRel = stageRel,
                SourceStageRel = stageRel,
                Object = string.IsNullOrWhiteSpace(objectKey) ? null : objectKey
            },
            Decision = decision
        };
    }

    /// <summary>
    /// 同一夹的原预演行改成同一条结论，条目本身不动。
    /// </summary>
    private static void ReplaceNoteDecisions(List<PreviewLine> lineList, List<PreviewLine> members, GateDecision decision)
    {
        foreach (var line in members)
        {
            var index = lineList.IndexOf(line);
            if (index < 0)
            {
                continue;
            }

            lineList[index] = new PreviewLine { Item = line.Item, Decision = decision };
        }
    }

    /// <summary>
    /// 用展开后的配图行换掉这一夹原来的标记行，其它栏目的行保持原位。
    /// </summary>
    private static void ReplaceNoteLines(List<PreviewLine> lineList, List<PreviewLine> members, List<PreviewLine> replacement)
    {
        var indexes = members
            .Select(line => lineList.IndexOf(line))
            .Where(index => index >= 0)
            .OrderBy(index => index)
            .ToList();
        if (indexes.Count == 0)
        {
            return;
        }

        var insertAt = indexes[0];
        for (var i = indexes.Count - 1; i >= 0; i--)
        {
            lineList.RemoveAt(indexes[i]);
        }

        lineList.InsertRange(insertAt, replacement);
    }

    /// <summary>
    /// 写出将改的台账路径，强调不改对象键。
    /// </summary>
    private static string RenderRelocatePreview(IReadOnlyList<PreviewLine> lineList)
    {
        var relocateList = lineList
            .Where(line => line.Decision.Allowed
                && MediaIntentCodes.FromCode(line.Item.Intent) == MediaIntent.StageRelocate)
            .Select(line => line.Item)
            .ToList();
        if (relocateList.Count == 0)
        {
            return "";
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("## 台账将改 stageRel");
        builder.AppendLine();
        builder.AppendLine("对象键与内容层 src / 题名不改。");
        builder.AppendLine();
        foreach (var item in relocateList)
        {
            builder.AppendLine($"- `{item.Object}`：`{item.StageRelBefore}` → `{item.StageRelAfter}`");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 写出将删除的内容记录，以及发布时要撤下的对象键。
    /// </summary>
    private static string RenderWorkWithdrawPreview(WorkspaceSession session, IReadOnlyList<PreviewLine> lineList)
    {
        var allowedList = lineList
            .Where(line => line.Decision.Allowed
                && MediaIntentCodes.FromCode(line.Item.Intent) == MediaIntent.WorkWithdraw)
            .Select(line => line.Item)
            .ToList();
        if (allowedList.Count == 0)
        {
            return "";
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("## 将删除内容记录");
        builder.AppendLine();
        foreach (var item in allowedList)
        {
            var work = WorkWithdrawRules.FindWork(session.Works, item.Channel, item.WorkId);
            var objectKeyList = work == null
                ? item.ObjectList ?? new List<string>()
                : WorkWithdrawRules.CollectObjectKeys(
                    work,
                    session.LedgerDict,
                    WorkWithdrawRules.ResolvePlaceholders(session.Profile));
            var title = string.IsNullOrWhiteSpace(work?.Title) ? item.WorkId : work.Title;
            builder.AppendLine($"- {item.Channel}/{item.WorkId}（{title}）");
            builder.AppendLine(objectKeyList.Count == 0
                ? "  没有待撤对象。"
                : "  发布时撤下：" + string.Join("、", objectKeyList));
        }

        builder.AppendLine();
        builder.AppendLine("中转站文件保留。正式位目录清空后删除空目录。");
        return builder.ToString();
    }

    /// <summary>
    /// 同一对象在同一作品上只记一次引用消除。
    /// </summary>
    private static void AddWorkRemove(
        Dictionary<string, HashSet<string>> workSetDict,
        string objectKey,
        string workKey)
    {
        if (!workSetDict.TryGetValue(objectKey, out var workSet))
        {
            workSet = new HashSet<string>(StringComparer.Ordinal);
            workSetDict[objectKey] = workSet;
        }

        workSet.Add(workKey);
    }

    /// <summary>
    /// 已允许的条目追加软警告：站点条目中转站缺失。体积不作警告。
    /// </summary>
    private static GateDecision ApplySoftWarnings(GateDecision decision, StageItem? stage, SiteItem? site)
    {
        if (!decision.Allowed)
        {
            return decision;
        }

        var warnList = new List<string>();
        if (site != null && string.IsNullOrWhiteSpace(site.StageRel))
        {
            warnList.Add("中转站缺失对应文件。");
        }

        if (warnList.Count == 0)
        {
            return decision;
        }

        if (!string.IsNullOrWhiteSpace(decision.Message))
        {
            warnList.Insert(0, decision.Message);
        }

        return GateDecision.Warn(string.Join(" ", warnList));
    }

    /// <summary>
    /// 批次含撤下且对象仍被未纳入批次的作品引用时，该键全部拒绝。
    /// </summary>
    private static void RejectSharedWithdrawKeys(
        WorkspaceSession session,
        List<PreviewLine> lineList)
    {
        var withdrawObjectSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lineList)
        {
            if (MediaIntentCodes.FromCode(line.Item.Intent) == MediaIntent.SiteWithdraw
                && !string.IsNullOrWhiteSpace(line.Item.Object))
            {
                withdrawObjectSet.Add(line.Item.Object);
            }
        }

        var batchRemoveDict = CountUniqueWorkRemoves(session, lineList.Select(line => line.Item));
        var blockedSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objectKey in withdrawObjectSet)
        {
            var refs = session.ContentRefCountDict.GetValueOrDefault(objectKey);
            var removes = batchRemoveDict.GetValueOrDefault(objectKey);
            if (refs - removes > 0)
            {
                blockedSet.Add(objectKey);
            }
        }

        if (blockedSet.Count == 0)
        {
            return;
        }

        for (var i = 0; i < lineList.Count; i++)
        {
            var line = lineList[i];
            if (string.IsNullOrWhiteSpace(line.Item.Object) || !blockedSet.Contains(line.Item.Object))
            {
                continue;
            }

            lineList[i] = new PreviewLine
            {
                Item = line.Item,
                Decision = GateDecision.Reject("批次含撤下，但该对象仍被未纳入批次的其它作品引用，整键拒绝。")
            };
        }
    }

    /// <summary>
    /// 同一批次两条重挂不得指向同一新路径。
    /// </summary>
    private static void RejectDuplicateRelocateAfters(List<PreviewLine> lineList)
    {
        var afterDict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in lineList)
        {
            if (MediaIntentCodes.FromCode(line.Item.Intent) != MediaIntent.StageRelocate
                || string.IsNullOrWhiteSpace(line.Item.StageRelAfter))
            {
                continue;
            }

            var after = JsonUtil.ToRel(line.Item.StageRelAfter);
            afterDict[after] = afterDict.GetValueOrDefault(after) + 1;
        }

        if (afterDict.Values.All(count => count <= 1))
        {
            return;
        }

        for (var i = 0; i < lineList.Count; i++)
        {
            var line = lineList[i];
            if (MediaIntentCodes.FromCode(line.Item.Intent) != MediaIntent.StageRelocate
                || string.IsNullOrWhiteSpace(line.Item.StageRelAfter))
            {
                continue;
            }

            var after = JsonUtil.ToRel(line.Item.StageRelAfter);
            if (afterDict.GetValueOrDefault(after) <= 1)
            {
                continue;
            }

            lineList[i] = new PreviewLine
            {
                Item = line.Item,
                Decision = GateDecision.Reject("批次内多条重挂指向同一新路径。")
            };
        }
    }

    /// <summary>
    /// 同一作品对同一对象的隐藏与撤下只计一次引用消除。
    /// </summary>
    private static Dictionary<string, int> CountUniqueWorkRemoves(
        WorkspaceSession session,
        IEnumerable<IntentItem> itemList)
    {
        var workSetDict = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var item in itemList)
        {
            var intent = MediaIntentCodes.FromCode(item.Intent);
            if (intent == MediaIntent.WorkWithdraw)
            {
                var withdrawn = WorkWithdrawRules.FindWork(session.Works, item.Channel, item.WorkId);
                if (withdrawn == null)
                {
                    continue;
                }

                var workKey = ContentPatchPlanner.WorkMapKey(item.Channel, item.WorkId ?? "");
                foreach (var objectKey in WorkWithdrawRules.CollectObjectKeys(
                    withdrawn,
                    session.LedgerDict,
                    WorkWithdrawRules.ResolvePlaceholders(session.Profile)))
                {
                    AddWorkRemove(workSetDict, objectKey, workKey);
                }

                continue;
            }

            if (intent is not (MediaIntent.SiteHide or MediaIntent.SiteWithdraw))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Object))
            {
                continue;
            }

            AddWorkRemove(workSetDict, item.Object, ContentPatchPlanner.WorkMapKey(item.Channel, item.WorkId ?? ""));
        }

        var countDict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in workSetDict)
        {
            var count = 0;
            foreach (var workKey in pair.Value)
            {
                var work = session.Works.FirstOrDefault(candidate =>
                    workKey == ContentPatchPlanner.WorkMapKey(candidate.Channel, candidate.Id)
                    || workKey == candidate.Id && candidate.Media.Any(media => media.Src == pair.Key));
                var media = work?.Media.FirstOrDefault(item =>
                    string.Equals(item.Src, pair.Key, StringComparison.Ordinal)
                    || string.Equals(item.Poster, pair.Key, StringComparison.Ordinal));
                count += Math.Max(1, media?.ReferenceCount ?? 1);
            }

            countDict[pair.Key] = count;
        }

        return countDict;
    }
}
