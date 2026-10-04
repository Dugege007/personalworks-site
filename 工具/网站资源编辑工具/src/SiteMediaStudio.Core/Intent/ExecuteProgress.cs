namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 上页队列媒类。数值即排队优先级：图 → 音频 → 视频 → 游戏包。
/// </summary>
public enum ExecuteMediaClass
{
    Image = 0,
    Audio = 1,
    Video = 2,
    Pack = 3
}

/// <summary>
/// 按媒类排出执行命令步。不定时假动画；压图 / 压码可按真实输出填当前步。
/// </summary>
public static class ExecuteProgressPlanner
{
    public const string PatchKey = "patch";
    public const string RelocateKey = "relocate";
    public const string FinishKey = "finish";

    /// <summary>压码预估耗时权；单条视频约占整条进度的九成。</summary>
    public const int EncodeVideoWeight = 36;

    /// <summary>压图预估耗时权；其余快步各为 1。</summary>
    public const int PrepareImageWeight = 12;

    /// <summary>回收、上页、封面、补丁、重挂、收尾。</summary>
    public const int QuickStepWeight = 1;

    /// <summary>
    /// 全部回收条目共用一步。
    /// </summary>
    public const string RecycleKey = "recycle";

    /// <summary>
    /// 全部须压图条目共用一步。
    /// </summary>
    public const string PrepareImageKey = "prep-image";

    /// <summary>
    /// 全部须压码条目共用一步。
    /// </summary>
    public const string EncodeVideoKey = "encode-video";

    /// <summary>
    /// 全部上页条目共用一步。
    /// </summary>
    public const string IngestKey = "ingest";

    /// <summary>
    /// 全部封面条目共用一步。
    /// </summary>
    public const string PosterKey = "poster";

    /// <summary>
    /// 判定上页条媒类；包优先于扩展名。
    /// </summary>
    public static ExecuteMediaClass Classify(WorkspaceSession? session, IntentItem item)
    {
        var stage = FindStage(session, item);
        if (stage?.IsPack == true || MediaPathRules.IsPackDirectory(item.StageRel))
        {
            return ExecuteMediaClass.Pack;
        }

        var path = stage?.FullPath ?? item.StageRel;
        if (MediaPathRules.IsVideoFile(path))
        {
            return ExecuteMediaClass.Video;
        }

        if (MediaPathRules.IsAudioFile(path))
        {
            return ExecuteMediaClass.Audio;
        }

        return ExecuteMediaClass.Image;
    }

    /// <summary>
    /// 上页条按媒类排队，同类保持原序。
    /// </summary>
    public static IReadOnlyList<IntentItem> OrderIngestItems(
        WorkspaceSession? session,
        IEnumerable<IntentItem> ingestItemList)
    {
        return ingestItemList
            .Select((item, index) => (item, index, Class: Classify(session, item)))
            .OrderBy(row => (int)row.Class)
            .ThenBy(row => row.index)
            .Select(row => row.item)
            .ToList();
    }

    /// <summary>
    /// 排出本次将走的命令步。同类批量命令（回收、压图、压码、上页）各只排一步，不按条拆。
    /// </summary>
    public static IReadOnlyList<PublishStep> Plan(WorkspaceSession? session, IntentDocument document)
    {
        var stepList = new List<PublishStep>();
        var recycleList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.StageRecycle)
            .ToList();
        if (recycleList.Count > 0)
        {
            stepList.Add(new PublishStep
            {
                Key = RecycleKey,
                Title = "回收",
                Weight = QuickStepWeight * recycleList.Count
            });
        }

        var ingestList = OrderIngestItems(
            session,
            document.Items.Where(item =>
                item.Intent == MediaIntentCodes.StageIngest
                && (session == null || !IntentGate.IsRedundantIngest(session, item))));
        var prepareCount = ingestList.Count(item => NeedsImagePrepare(session, item));
        var encodeCount = ingestList.Count(item => NeedsVideoEncode(session, item));
        if (prepareCount > 0)
        {
            stepList.Add(new PublishStep
            {
                Key = PrepareImageKey,
                Title = "压图",
                Weight = PrepareImageWeight * prepareCount
            });
        }

        if (encodeCount > 0)
        {
            stepList.Add(new PublishStep
            {
                Key = EncodeVideoKey,
                Title = "压码",
                Weight = EncodeVideoWeight * encodeCount
            });
        }

        if (ingestList.Count > 0)
        {
            stepList.Add(new PublishStep
            {
                Key = IngestKey,
                Title = "上页",
                Weight = QuickStepWeight * ingestList.Count
            });
        }

        var hasPatch = ingestList.Count > 0
            || document.Items.Any(item =>
                item.Intent is MediaIntentCodes.SiteWithdraw
                    or MediaIntentCodes.WorkRegister
                    or MediaIntentCodes.WorkUpdate
                    or MediaIntentCodes.CopyUpdate
                    or MediaIntentCodes.MediaReorder
                    or MediaIntentCodes.StarsUpdate
                || (item.Intent == MediaIntentCodes.SiteHide
                    && (session == null || !IntentGate.IsRedundantHide(session, item)))
                || (item.Intent == MediaIntentCodes.SiteRestore
                    && (session == null || !IntentGate.IsRedundantRestore(session, item))));
        if (hasPatch)
        {
            stepList.Add(new PublishStep { Key = PatchKey, Title = "内容补丁", Weight = QuickStepWeight });
        }

        if (document.Items.Any(item => item.Intent == MediaIntentCodes.StageRelocate))
        {
            stepList.Add(new PublishStep { Key = RelocateKey, Title = "重挂台账", Weight = QuickStepWeight });
        }

        stepList.Add(new PublishStep { Key = FinishKey, Title = "收尾", Weight = QuickStepWeight });
        return stepList;
    }

    /// <summary>
    /// 进度标题用的短名。
    /// </summary>
    public static string DisplayName(IntentItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.StageRel))
        {
            return Path.GetFileName(item.StageRel.Replace('/', Path.DirectorySeparatorChar));
        }

        if (!string.IsNullOrWhiteSpace(item.Object))
        {
            return Path.GetFileName(item.Object.Replace('/', Path.DirectorySeparatorChar));
        }

        return item.WorkId ?? "条目";
    }

    /// <summary>
    /// 按投放路径找回编目条。
    /// </summary>
    public static StageItem? FindStage(WorkspaceSession? session, IntentItem item)
    {
        if (session == null || string.IsNullOrWhiteSpace(item.StageRel))
        {
            return null;
        }

        return session.StageItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StageRel, item.StageRel, StringComparison.Ordinal));
    }

    /// <summary>
    /// 该上页条是否须先压成网页 WebP。
    /// </summary>
    public static bool NeedsImagePrepare(WorkspaceSession? session, IntentItem item)
    {
        return Classify(session, item) == ExecuteMediaClass.Image
            && WebpPrepareRules.NeedsPrepare(Probe(session, item), session?.Profile);
    }

    /// <summary>
    /// 该上页条是否须先压成网页 MP4。
    /// </summary>
    public static bool NeedsVideoEncode(WorkspaceSession? session, IntentItem item)
    {
        return Classify(session, item) == ExecuteMediaClass.Video
            && VideoEncodeRules.NeedsEncode(Probe(session, item), session?.Profile);
    }

    /// <summary>
    /// 供压图 / 压码判定用的探测条；找不到编目时用路径拼一条。
    /// </summary>
    public static StageItem Probe(WorkspaceSession? session, IntentItem item)
    {
        var stage = FindStage(session, item);
        if (stage != null)
        {
            return stage;
        }

        return new StageItem
        {
            StageRel = item.StageRel ?? "",
            FullPath = item.StageRel ?? "",
            ChannelKey = item.Channel ?? "",
            IsPack = MediaPathRules.IsPackDirectory(item.StageRel)
        };
    }
}

/// <summary>
/// 按规划表推进执行进度。
/// </summary>
public sealed class ExecuteProgressTracker
{
    private readonly PublishProgressTracker mInner;

    public ExecuteProgressTracker(
        WorkspaceSession session,
        IntentDocument document,
        IProgress<PublishProgress>? progress)
    {
        mInner = new PublishProgressTracker(ExecuteProgressPlanner.Plan(session, document), progress);
    }

    /// <summary>
    /// 进入指定命令步。
    /// </summary>
    public void Begin(string key, string? detail = null)
    {
        mInner.Begin(key, detail);
    }

    /// <summary>
    /// 按当前步真实进度填条；无输出时不要调用。
    /// </summary>
    public void SetFraction(double fraction, string? detail = null)
    {
        mInner.SetFraction(fraction, detail);
    }

    /// <summary>
    /// 当前资源台账改名正在重试。
    /// </summary>
    public void NoteLedgerRenameAttempt(string name, int retriesSoFar)
    {
        mInner.NoteLedgerRenameAttempt(name, retriesSoFar);
    }

    /// <summary>
    /// 当前资源台账改名结束，并入累计。
    /// </summary>
    public void CommitLedgerRenameRetries(string name, int retries)
    {
        mInner.CommitLedgerRenameRetries(name, retries);
    }

    /// <summary>
    /// 把第 <paramref name="index"/> 条在整步中的份额与条内分数合成 0～1。
    /// </summary>
    public void SetBatchFraction(int index, int count, double inner, string? detail = null)
    {
        var total = Math.Max(1, count);
        var clamped = Math.Clamp(inner, 0, 1);
        SetFraction((index + clamped) / total, detail);
    }

    /// <summary>
    /// 全部命令步走完。
    /// </summary>
    public void Finish(string? detail = null)
    {
        mInner.Finish(detail ?? "本机执行完成。");
    }

    /// <summary>
    /// 失败时停在当前步。
    /// </summary>
    public void Fail(string? detail)
    {
        mInner.Fail(detail ?? "执行失败。");
    }

    /// <summary>
    /// 写入耗时并记入运行日志。
    /// </summary>
    public BatchRunResult Stamp(BatchRunResult result)
    {
        return mInner.Stamp(result, "执行");
    }
}
