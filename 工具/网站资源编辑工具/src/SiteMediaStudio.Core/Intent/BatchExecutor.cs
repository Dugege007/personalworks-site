namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 机械模式本机顺序：回收 → ingest → 内容补丁。不发布、不 withdraw --apply。
/// </summary>
public static class BatchExecutor
{
    /// <summary>
    /// 执行已通过预演的意图文件。
    /// </summary>
    /// <param name="marksPath">标记文件；空则用本机默认路径。</param>
    public static BatchRunResult Run(
        WorkspaceSession session,
        IntentDocument document,
        string intentPath,
        IProgress<PublishProgress>? progress = null,
        string? marksPath = null)
    {
        if (NoteCommands.PullMeta(session.Profile, document) && File.Exists(intentPath))
        {
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
        }

        if (document.Options.Deploy != "none")
        {
            var rejected = Fail("发布", "阶段3 机械模式 options.deploy 必须为 none。", Array.Empty<string>());
            RunLog.AppendOutcome("执行", rejected);
            return rejected;
        }

        var tracker = new ExecuteProgressTracker(session, document, progress);
        var completedList = new List<string>();
        var refreshUrlList = new List<string>();
        var recycleOk = 0;
        var ingestOk = 0;
        var hideOk = 0;
        var restoreOk = 0;
        var withdrawOk = 0;
        var resourceList = new List<CommandResourceResult>();

        var recycleItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.StageRecycle)
            .ToList();
        var ingestItemList = ExecuteProgressPlanner.OrderIngestItems(
            session,
            document.Items.Where(item =>
                item.Intent == MediaIntentCodes.StageIngest
                && !IntentGate.IsRedundantIngest(session, item)));
        foreach (var skipped in document.Items)
        {
            var redundant = skipped.Intent == MediaIntentCodes.StageIngest
                    && IntentGate.IsRedundantIngest(session, skipped)
                || skipped.Intent == MediaIntentCodes.SiteHide
                    && IntentGate.IsRedundantHide(session, skipped)
                || skipped.Intent == MediaIntentCodes.SiteRestore
                    && IntentGate.IsRedundantRestore(session, skipped);
            if (redundant)
            {
                ClearSucceededMark(session, skipped, marksPath);
            }
        }
        var hideItemList = document.Items
            .Where(item => item.Intent is MediaIntentCodes.SiteHide or MediaIntentCodes.SiteWithdraw)
            .Where(item => !IntentGate.IsRedundantHide(session, item))
            .ToList();
        var withdrawItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.SiteWithdraw)
            .ToList();
        var registerItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.WorkRegister)
            .ToList();
        var updateItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.WorkUpdate)
            .ToList();
        var relocateItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.StageRelocate)
            .ToList();

        if (recycleItemList.Count > 0)
        {
            tracker.Begin(ExecuteProgressPlanner.RecycleKey, "正在回收。");
        }

        for (var recycleIndex = 0; recycleIndex < recycleItemList.Count; recycleIndex++)
        {
            var item = recycleItemList[recycleIndex];
            tracker.SetBatchFraction(
                recycleIndex,
                recycleItemList.Count,
                0,
                "正在回收 " + ExecuteProgressPlanner.DisplayName(item));
            var stage = session.StageItems.FirstOrDefault(candidate =>
                string.Equals(candidate.StageRel, item.StageRel, StringComparison.Ordinal));
            if (stage == null)
            {
                resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(item), false));
                return Stop(tracker, "回收", "找不到投放箱文件：" + item.StageRel, completedList, resourceList: resourceList);
            }

            try
            {
                RecycleService.SendToRecycleBin(stage.FullPath);
                recycleOk++;
                resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(item), true));
                ClearSucceededMark(session, item, marksPath);
                RunLog.Append("回收 " + item.StageRel, true, "");
            }
            catch (Exception ex)
            {
                resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(item), false));
                RunLog.Append("回收 " + item.StageRel, false, ex.Message);
                return Stop(tracker, "回收", ex.Message, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
            }
        }

        if (recycleItemList.Count > 0)
        {
            completedList.Add($"回收 {recycleOk}/{recycleItemList.Count}");
        }

        var posterOk = 0;
        var intentDirty = false;
        var encodedItemSet = new HashSet<IntentItem>();
        foreach (var item in ingestItemList)
        {
            if (string.IsNullOrWhiteSpace(item.StageRel) || string.IsNullOrWhiteSpace(item.Object))
            {
                resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(item), false));
                return Stop(
                    tracker,
                    "上页",
                    "上页条目缺少 stageRel 或 object。",
                    completedList,
                    recycleOk,
                    ingestOk,
                    hideOk,
                    withdrawOk,
                    refreshUrlList,
                    resourceList);
            }

            if (string.IsNullOrWhiteSpace(item.SourceStageRel))
            {
                item.SourceStageRel = item.StageRel;
            }
        }

        var prepareItemList = ingestItemList
            .Where(item => ExecuteProgressPlanner.NeedsImagePrepare(session, item))
            .ToList();
        if (prepareItemList.Count > 0)
        {
            tracker.Begin(ExecuteProgressPlanner.PrepareImageKey, "正在压图。");
            for (var prepareIndex = 0; prepareIndex < prepareItemList.Count; prepareIndex++)
            {
                var item = prepareItemList[prepareIndex];
                tracker.SetBatchFraction(
                    prepareIndex,
                    prepareItemList.Count,
                    0,
                    "正在压图 " + ExecuteProgressPlanner.DisplayName(item));
                var prepared = TryPrepareIngest(
                    session,
                    item,
                    (fraction, detail) => tracker.SetBatchFraction(
                        prepareIndex,
                        prepareItemList.Count,
                        fraction,
                        detail));
                if (prepared == null)
                {
                    continue;
                }

                if (!prepared.Ok)
                {
                    resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(item), false));
                    RunLog.Append("压图 " + item.StageRel, false, prepared.FailureText);
                    return Stop(
                        tracker,
                        "压图",
                        prepared.FailureText,
                        completedList,
                        recycleOk,
                        ingestOk,
                        hideOk,
                        withdrawOk,
                        refreshUrlList,
                        resourceList);
                }

                intentDirty = true;
                RunLog.Append("压图 " + item.StageRel, true, prepared.StdOut);
            }

            completedList.Add("压图");
        }

        var encodeItemList = ingestItemList
            .Where(item => ExecuteProgressPlanner.NeedsVideoEncode(session, item))
            .ToList();
        if (encodeItemList.Count > 0)
        {
            tracker.Begin(ExecuteProgressPlanner.EncodeVideoKey, "正在压码。");
            for (var encodeIndex = 0; encodeIndex < encodeItemList.Count; encodeIndex++)
            {
                var item = encodeItemList[encodeIndex];
                tracker.SetBatchFraction(
                    encodeIndex,
                    encodeItemList.Count,
                    0,
                    "正在压码 " + ExecuteProgressPlanner.DisplayName(item));
                var encoded = TryPrepareVideo(
                    session,
                    item,
                    (fraction, detail) => tracker.SetBatchFraction(
                        encodeIndex,
                        encodeItemList.Count,
                        fraction,
                        detail));
                if (encoded == null)
                {
                    continue;
                }

                if (!encoded.Ok)
                {
                    resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(item), false));
                    RunLog.Append("压码 " + item.StageRel, false, encoded.FailureText);
                    return Stop(
                        tracker,
                        "压码",
                        encoded.FailureText,
                        completedList,
                        recycleOk,
                        ingestOk,
                        hideOk,
                        withdrawOk,
                        refreshUrlList,
                        resourceList);
                }

                encodedItemSet.Add(item);
                intentDirty = true;
                RunLog.Append("压码 " + item.StageRel, true, encoded.StdOut);
            }

            completedList.Add("压码");
        }

        if (ingestItemList.Count > 0)
        {
            tracker.Begin(ExecuteProgressPlanner.IngestKey, "正在上页。");
        }

        for (var ingestIndex = 0; ingestIndex < ingestItemList.Count; ingestIndex++)
        {
            var item = ingestItemList[ingestIndex];
            var sourceStageRel = item.SourceStageRel ?? item.StageRel;
            var displayName = ExecuteProgressPlanner.DisplayName(new IntentItem
            {
                StageRel = sourceStageRel,
                Object = item.Object
            });
            tracker.SetBatchFraction(
                ingestIndex,
                ingestItemList.Count,
                0,
                "正在上页 " + displayName);
            SitemediaRunResult run;
            try
            {
                run = SitemediaClient.Ingest(
                    session.Profile,
                    item.StageRel ?? "",
                    item.Object ?? "",
                    document.Options.Bump,
                    SourceRelIfRewritten(sourceStageRel, item.StageRel),
                    line => NoteLedgerRenameLine(tracker, displayName, line));
            }
            catch (Exception ex)
            {
                tracker.CommitLedgerRenameRetries(displayName, 0);
                resourceList.Add(CommandResourceResult.Of(displayName, false));
                RunLog.Append("上页 " + item.StageRel, false, ex.Message);
                return Stop(tracker, "上页", ex.Message, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
            }

            RunLog.Append("上页 " + item.Object, run.Ok, run.StdOut + run.StdErr);
            tracker.CommitLedgerRenameRetries(displayName, SitemediaClient.ParseLedgerRenameRetries(run.StdOut));
            if (!run.Ok)
            {
                resourceList.Add(CommandResourceResult.Of(displayName, false));
                return Stop(tracker, "上页", run.FailureText, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
            }

            ingestOk++;
            refreshUrlList.AddRange(run.RefreshUrlList);
            var ledgerRenameRetries = SitemediaClient.ParseLedgerRenameRetries(run.StdOut);

            if (encodedItemSet.Contains(item) && VideoEncodeRules.IsFormalVideoObject(item.Object))
            {
                var posterObject = VideoEncodeRules.PosterObjectKey(item.Object);
                var posterRel = VideoEncodeRules.PosterRelFromPrepared(item.StageRel);
                tracker.SetBatchFraction(
                    ingestIndex,
                    ingestItemList.Count,
                    0,
                    "正在入库封面 " + posterObject);
                SitemediaRunResult posterRun;
                try
                {
                    posterRun = SitemediaClient.Ingest(
                        session.Profile,
                        posterRel,
                        posterObject,
                        document.Options.Bump,
                        onOutputLine: line => NoteLedgerRenameLine(tracker, displayName, line));
                }
                catch (Exception ex)
                {
                    tracker.CommitLedgerRenameRetries(displayName, 0);
                    resourceList.Add(CommandResourceResult.Of(displayName, false));
                    RunLog.Append("封面 " + posterRel, false, ex.Message);
                    return Stop(tracker, "封面", ex.Message, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
                }

                RunLog.Append("封面 " + posterObject, posterRun.Ok, posterRun.StdOut + posterRun.StdErr);
                tracker.CommitLedgerRenameRetries(
                    displayName,
                    SitemediaClient.ParseLedgerRenameRetries(posterRun.StdOut));
                if (!posterRun.Ok)
                {
                    resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(new IntentItem
                    {
                        StageRel = sourceStageRel,
                        Object = item.Object
                    }), false));
                    return Stop(tracker, "封面", posterRun.FailureText, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
                }

                posterOk++;
                refreshUrlList.AddRange(posterRun.RefreshUrlList);
                ledgerRenameRetries += SitemediaClient.ParseLedgerRenameRetries(posterRun.StdOut);
            }

            resourceList.Add(CommandResourceResult.Of(
                ExecuteProgressPlanner.DisplayName(new IntentItem
                {
                    StageRel = sourceStageRel,
                    Object = item.Object
                }),
                true,
                ledgerRenameRetries));
            ClearSucceededMark(session, item, marksPath);
        }

        if (posterOk > 0)
        {
            completedList.Add("封面");
        }

        if (ingestItemList.Count > 0)
        {
            completedList.Add($"上页 {ingestOk}/{ingestItemList.Count}");
        }

        if (intentDirty)
        {
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
        }

        var restoreItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.SiteRestore)
            .Where(item => !IntentGate.IsRedundantRestore(session, item))
            .ToList();
        var copyItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.CopyUpdate)
            .ToList();
        var tagItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.TagsUpdate)
            .ToList();
        var reorderItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.MediaReorder)
            .ToList();
        var starItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.StarsUpdate)
            .ToList();
        var workWithdrawItemList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.WorkWithdraw)
            .ToList();
        if (hideItemList.Count > 0 || ingestItemList.Count > 0 || registerItemList.Count > 0 || updateItemList.Count > 0 || copyItemList.Count > 0 || tagItemList.Count > 0 || restoreItemList.Count > 0 || reorderItemList.Count > 0 || workWithdrawItemList.Count > 0 || starItemList.Count > 0)
        {
            tracker.Begin(ExecuteProgressPlanner.PatchKey, "正在写入内容补丁。");
            ContentPatchRunResult patch;
            try
            {
                patch = ContentPatchClient.Apply(session.Profile.ProfilePath, intentPath);
            }
            catch (Exception ex)
            {
                RunLog.Append("内容补丁", false, ex.Message);
                return Stop(tracker, "内容补丁", ex.Message, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
            }

            RunLog.Append("内容补丁", patch.Ok, patch.StdOut + patch.StdErr);
            if (!patch.Ok)
            {
                return Stop(tracker, "内容补丁", patch.FailureText, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
            }

            hideOk = hideItemList.Count(item => item.Intent == MediaIntentCodes.SiteHide);
            restoreOk = restoreItemList.Count;
            foreach (var item in hideItemList.Concat(restoreItemList))
            {
                resourceList.Add(CommandResourceResult.Of(ExecuteProgressPlanner.DisplayName(item), true));
                ClearSucceededMark(session, item, marksPath);
            }

            completedList.Add("内容补丁已写入");
            if (restoreOk > 0)
            {
                completedList.Add($"恢复显示 {restoreOk}");
            }
            if (registerItemList.Count > 0)
            {
                completedList.Add($"登记 {registerItemList.Count}");
            }

            if (updateItemList.Count > 0)
            {
                completedList.Add($"编辑项目 {updateItemList.Count}");
            }

            if (copyItemList.Count > 0)
            {
                completedList.Add($"文案 {copyItemList.Count}");
            }

            if (tagItemList.Count > 0)
            {
                completedList.Add($"标签 {tagItemList.Count}");
            }

            if (reorderItemList.Count > 0)
            {
                completedList.Add($"排序 {reorderItemList.Count}");
            }
        }

        withdrawOk = withdrawItemList.Count;
        if (withdrawOk > 0)
        {
            completedList.Add($"待撤下已去引用 {withdrawOk}（正式位与 COS 留到发布上线）");
        }

        if (workWithdrawItemList.Count > 0)
        {
            completedList.Add($"整项撤下已删除内容记录 {workWithdrawItemList.Count}（正式位与 COS 留到发布上线）");
        }

        if (relocateItemList.Count > 0)
        {
            tracker.Begin(ExecuteProgressPlanner.RelocateKey, "正在重挂台账。");
            LedgerWriteResult relocate;
            try
            {
                relocate = LedgerWriter.Apply(session.Profile, relocateItemList);
            }
            catch (Exception ex)
            {
                RunLog.Append("重挂", false, ex.Message);
                return Stop(tracker, "重挂", ex.Message, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
            }

            RunLog.Append("重挂", relocate.Ok, string.Join("；", relocate.ChangeList) + relocate.FailureText);
            if (!relocate.Ok)
            {
                return Stop(tracker, "重挂", relocate.FailureText, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList);
            }

            completedList.Add($"重挂 {relocate.ChangeList.Count}");
        }

        var pending = PendingPublishStore.FromDocument(
            session.Profile,
            document,
            DistinctUrls(refreshUrlList),
            session);
        tracker.Begin(ExecuteProgressPlanner.FinishKey, "正在收尾。");
        tracker.Finish("本机执行完成。");
        return tracker.Stamp(new BatchRunResult
        {
            Ok = true,
            CompletedStepList = completedList,
            RecycleOk = recycleOk,
            IngestOk = ingestOk,
            HideOk = hideOk,
            RestoreOk = restoreOk,
            WithdrawOk = withdrawOk,
            RefreshUrlList = DistinctUrls(refreshUrlList),
            Pending = pending,
            ResourceList = resourceList
        });
    }

    /// <summary>
    /// 视频先压成 <c>.site-ready</c> MP4 与封面，并改写本条 stageRel / object。
    /// </summary>
    private static VideoEncodeResult? TryPrepareVideo(
        WorkspaceSession session,
        IntentItem item,
        Action<double, string>? onProgress)
    {
        var sourceStageRel = item.StageRel ?? "";
        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(session.Profile, session.Profile.StageRoot);
        var stage = session.StageItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StageRel, sourceStageRel, StringComparison.Ordinal));
        var sourcePath = stage?.FullPath
            ?? Path.Combine(stageRoot, sourceStageRel.Replace('/', Path.DirectorySeparatorChar));
        var probe = stage ?? new StageItem
        {
            StageRel = sourceStageRel,
            FullPath = sourcePath,
            ChannelKey = item.Channel ?? ""
        };
        if (!VideoEncodeRules.NeedsEncode(probe, session.Profile))
        {
            return null;
        }

        if (!File.Exists(sourcePath))
        {
            return new VideoEncodeResult
            {
                Ok = false,
                StdErr = "找不到投放箱文件：" + sourceStageRel
            };
        }

        var preparedRel = VideoEncodeRules.PreparedStageRel(sourceStageRel, item.WorkId, item.Object);
        var posterRel = VideoEncodeRules.PreparedPosterRel(sourceStageRel, item.WorkId, item.Object);
        var outputPath = Path.Combine(stageRoot, preparedRel.Replace('/', Path.DirectorySeparatorChar));
        var posterPath = Path.Combine(stageRoot, posterRel.Replace('/', Path.DirectorySeparatorChar));
        var result = VideoEncodeClient.Prepare(
            sourcePath,
            outputPath,
            posterPath,
            sourceStageRel,
            onProgress: onProgress);
        if (!result.Ok)
        {
            return result;
        }

        item.StageRel = preparedRel;
        item.Object = EnsureMp4Object(item.Object);
        return result;
    }

    /// <summary>
    /// 压码后对象键须为 MP4。
    /// </summary>
    private static string EnsureMp4Object(string? objectKey)
    {
        var rel = JsonUtil.ToRel(objectKey ?? "");
        if (string.IsNullOrWhiteSpace(rel)
            || rel.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return rel;
        }

        var slash = rel.LastIndexOf('/');
        var name = slash < 0 ? rel : rel[(slash + 1)..];
        var next = Path.ChangeExtension(name, ".mp4");
        return slash < 0 ? next : rel[..(slash + 1)] + next;
    }

    /// <summary>
    /// 原图先压成 <c>.site-ready</c> WebP，并改写本条 stageRel / object。
    /// </summary>
    private static WebpPrepareResult? TryPrepareIngest(
        WorkspaceSession session,
        IntentItem item,
        Action<double, string>? onProgress)
    {
        var stageRel = item.StageRel ?? "";
        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(session.Profile, session.Profile.StageRoot);
        var stage = session.StageItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StageRel, stageRel, StringComparison.Ordinal));
        var sourcePath = stage?.FullPath
            ?? Path.Combine(stageRoot, stageRel.Replace('/', Path.DirectorySeparatorChar));
        var probe = stage ?? new StageItem
        {
            StageRel = stageRel,
            FullPath = sourcePath,
            ChannelKey = item.Channel ?? ""
        };
        if (!WebpPrepareRules.NeedsPrepare(probe, session.Profile))
        {
            return null;
        }

        if (!File.Exists(sourcePath))
        {
            return new WebpPrepareResult
            {
                Ok = false,
                StdErr = "找不到投放箱文件：" + stageRel
            };
        }

        var preparedRel = WebpPrepareRules.PreparedStageRel(stageRel, item.WorkId, item.Object);
        var outputPath = Path.Combine(stageRoot, preparedRel.Replace('/', Path.DirectorySeparatorChar));
        var result = WebpPrepareClient.Prepare(sourcePath, outputPath, item.Channel, onProgress);
        if (!result.Ok)
        {
            return result;
        }

        item.StageRel = preparedRel;
        item.Object = EnsureWebpObject(item.Object);
        return result;
    }

    /// <summary>
    /// 把 sitemedia 一行输出里的台账改名重试推到进度窗。
    /// </summary>
    private static void NoteLedgerRenameLine(ExecuteProgressTracker tracker, string name, string line)
    {
        var retries = SitemediaClient.ParseLedgerRenameAttempt(line);
        if (retries > 0)
        {
            tracker.NoteLedgerRenameAttempt(name, retries);
        }
    }

    /// <summary>
    /// 一条完成或判定跳过时立刻清该条标记，避免后续步骤失败时已完成资源仍带着角标。
    /// </summary>
    private static void ClearSucceededMark(WorkspaceSession session, IntentItem item, string? marksPath)
    {
        var key = MarkDraftStore.KeyOf(item);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        MarkDraftStore.RemoveKeys(session.Profile, new[] { key }, marksPath);
    }

    /// <summary>
    /// 压成 <c>.site-ready</c> 后保留原片路径，写入台账 <c>sourceStageRel</c>。
    /// </summary>
    private static string? SourceRelIfRewritten(string? sourceStageRel, string? preparedStageRel)
    {
        var source = JsonUtil.ToRel(sourceStageRel ?? "");
        var prepared = JsonUtil.ToRel(preparedStageRel ?? "");
        if (string.IsNullOrWhiteSpace(source)
            || string.Equals(source, prepared, StringComparison.Ordinal))
        {
            return null;
        }

        return source;
    }

    /// <summary>
    /// 压图后对象键须为 WebP。
    /// </summary>
    private static string EnsureWebpObject(string? objectKey)
    {
        var rel = JsonUtil.ToRel(objectKey ?? "");
        if (string.IsNullOrWhiteSpace(rel)
            || rel.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return rel;
        }

        var slash = rel.LastIndexOf('/');
        var name = slash < 0 ? rel : rel[(slash + 1)..];
        var next = Path.ChangeExtension(name, ".webp");
        return slash < 0 ? next : rel[..(slash + 1)] + next;
    }

    /// <summary>
    /// 回报失败进度并组装结果。
    /// </summary>
    private static BatchRunResult Stop(
        ExecuteProgressTracker tracker,
        string step,
        string text,
        IReadOnlyList<string> completedList,
        int recycleOk = 0,
        int ingestOk = 0,
        int hideOk = 0,
        int withdrawOk = 0,
        IReadOnlyList<string>? refreshUrlList = null,
        IReadOnlyList<CommandResourceResult>? resourceList = null)
    {
        tracker.Fail(text);
        return tracker.Stamp(Fail(step, text, completedList, recycleOk, ingestOk, hideOk, withdrawOk, refreshUrlList, resourceList));
    }

    /// <summary>
    /// 组装失败结果并保留已完成步。
    /// </summary>
    private static BatchRunResult Fail(
        string step,
        string text,
        IReadOnlyList<string> completedList,
        int recycleOk = 0,
        int ingestOk = 0,
        int hideOk = 0,
        int withdrawOk = 0,
        IReadOnlyList<string>? refreshUrlList = null,
        IReadOnlyList<CommandResourceResult>? resourceList = null)
    {
        return new BatchRunResult
        {
            Ok = false,
            FailedStep = step,
            FailureText = text,
            CompletedStepList = completedList,
            RecycleOk = recycleOk,
            IngestOk = ingestOk,
            HideOk = hideOk,
            WithdrawOk = withdrawOk,
            RefreshUrlList = refreshUrlList ?? Array.Empty<string>(),
            Pending = null,
            ResourceList = resourceList ?? Array.Empty<CommandResourceResult>()
        };
    }

    /// <summary>
    /// 去重待刷 URL，保持出现顺序。
    /// </summary>
    private static IReadOnlyList<string> DistinctUrls(IEnumerable<string> urlList)
    {
        var seenSet = new HashSet<string>(StringComparer.Ordinal);
        var resultList = new List<string>();
        foreach (var url in urlList)
        {
            if (seenSet.Add(url))
            {
                resultList.Add(url);
            }
        }

        return resultList;
    }
}

/// <summary>
/// 一批机械执行的汇总。
/// </summary>
public sealed class BatchRunResult
{
    public bool Ok { get; init; }
    public string? FailedStep { get; init; }
    public string FailureText { get; init; } = "";
    public IReadOnlyList<string> CompletedStepList { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RefreshUrlList { get; init; } = Array.Empty<string>();
    public int RecycleOk { get; init; }
    public int IngestOk { get; init; }
    public int HideOk { get; init; }
    public int RestoreOk { get; init; }
    public int WithdrawOk { get; init; }
    public PendingPublish? Pending { get; init; }
    public IReadOnlyList<CommandStepTiming> StepTimingList { get; init; } = Array.Empty<CommandStepTiming>();
    public IReadOnlyList<CommandResourceResult> ResourceList { get; init; } = Array.Empty<CommandResourceResult>();
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// 带上追踪器记下的各步耗时。
    /// </summary>
    public BatchRunResult WithTiming(IReadOnlyList<CommandStepTiming> timingList, TimeSpan elapsed)
    {
        return new BatchRunResult
        {
            Ok = Ok,
            FailedStep = FailedStep,
            FailureText = FailureText,
            CompletedStepList = CompletedStepList,
            RefreshUrlList = RefreshUrlList,
            RecycleOk = RecycleOk,
            IngestOk = IngestOk,
            HideOk = HideOk,
            RestoreOk = RestoreOk,
            WithdrawOk = WithdrawOk,
            Pending = Pending,
            StepTimingList = timingList,
            ResourceList = ResourceList,
            Elapsed = elapsed
        };
    }
}
