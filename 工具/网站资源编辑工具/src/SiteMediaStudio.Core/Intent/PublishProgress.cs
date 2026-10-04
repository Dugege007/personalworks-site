using System.Diagnostics;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 发布上线的一条进度快照，供窗口进度条绑定。
/// </summary>
public sealed class PublishProgress
{
    public int Percent { get; init; }
    public int CurrentStep { get; init; }
    public int TotalStep { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";

    /// <summary>
    /// 台账改名重试统计；没有重试时为空，窗口不显示。
    /// </summary>
    public string RetrySummary { get; init; } = "";
}

/// <summary>
/// 一条命令步。权重是进度条份额，按预估耗时加权，不按步数均分。
/// </summary>
public sealed class PublishStep
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public int Weight { get; init; } = 1;
}

/// <summary>
/// 按队列排出发布命令步，并据 deploy 日志推进。不定时假动画。
/// </summary>
public static class PublishProgressPlanner
{
    public const string PrepareKey = "prepare";
    public const string TidyKey = "tidy-satellites";
    public const string BuildKey = "deploy-build";
    public const string CosKey = "deploy-cos";
    public const string PackKey = "deploy-pack";
    public const string UploadKey = "deploy-upload";
    public const string ReloadKey = "deploy-reload";
    public const string PurgeKey = "purge";
    public const string FinishKey = "finish";

    /// <summary>构建静态包预估耗时权。</summary>
    public const int BuildWeight = 8;

    /// <summary>同步一张图到 COS 的预估耗时权。</summary>
    public const int CosImageWeight = 1;

    /// <summary>同步一条视频到 COS 的预估耗时权。</summary>
    public const int CosVideoWeight = 12;

    /// <summary>完整发布即使没有待入库对象，COS 列举与跳过也占一步份额。</summary>
    public const int CosMinWeight = 6;

    /// <summary>打包 dist 预估耗时权。</summary>
    public const int PackWeight = 2;

    /// <summary>上传静态包到轻量机预估耗时权。</summary>
    public const int UploadWeight = 6;

    /// <summary>远端覆盖并重载预估耗时权。</summary>
    public const int ReloadWeight = 3;

    /// <summary>提交 CDN 刷新预估耗时权。</summary>
    public const int PurgeWeight = 2;

    /// <summary>准备、收尾。</summary>
    public const int QuickStepWeight = 1;

    /// <summary>撤下一条对象。</summary>
    public const int WithdrawItemWeight = 2;

    /// <summary>
    /// 排出本次将走的命令步。spa 不含 COS；无待刷则无 purge。
    /// 步权按预估耗时，COS 随待入库对象加权。
    /// </summary>
    public static IReadOnlyList<PublishStep> Plan(PendingPublish pending)
    {
        var spa = string.Equals(pending.Deploy, "spa", StringComparison.OrdinalIgnoreCase);
        var stepList = new List<PublishStep>
        {
            new() { Key = PrepareKey, Title = "准备发布", Weight = QuickStepWeight },
            new() { Key = BuildKey, Title = "构建静态包", Weight = BuildWeight }
        };
        if (pending.WithdrawObjectList.Count > 0)
        {
            stepList.Insert(1, new PublishStep
            {
                Key = TidyKey,
                Title = "核对撤下引用",
                Weight = QuickStepWeight
            });
        }
        if (!spa)
        {
            stepList.Add(new PublishStep
            {
                Key = CosKey,
                Title = "同步媒体到 COS",
                Weight = CosStepWeight(pending)
            });
        }

        stepList.Add(new PublishStep { Key = PackKey, Title = "打包站点", Weight = PackWeight });
        stepList.Add(new PublishStep { Key = UploadKey, Title = "上传到服务器", Weight = UploadWeight });
        stepList.Add(new PublishStep { Key = ReloadKey, Title = "覆盖站点并重载", Weight = ReloadWeight });

        if (pending.WithdrawObjectList.Count > 0)
        {
            stepList.Add(new PublishStep
            {
                Key = WithdrawKey,
                Title = "撤下",
                Weight = WithdrawItemWeight * pending.WithdrawObjectList.Count
            });
        }

        if (HasPurge(pending))
        {
            stepList.Add(new PublishStep { Key = PurgeKey, Title = "提交 CDN 刷新", Weight = PurgeWeight });
        }

        stepList.Add(new PublishStep { Key = FinishKey, Title = "收尾", Weight = QuickStepWeight });
        return stepList;
    }

    /// <summary>
    /// COS 步权：待入库图 1、视频 12，至少 6。不按文件体积现算。
    /// </summary>
    public static int CosStepWeight(PendingPublish pending)
    {
        var weight = 0;
        foreach (var objectKey in pending.IngestObjectList)
        {
            weight += MediaPathRules.IsVideoFile(objectKey) ? CosVideoWeight : CosImageWeight;
        }

        return Math.Max(CosMinWeight, weight);
    }

    /// <summary>
    /// 全部待撤对象共用一步。
    /// </summary>
    public const string WithdrawKey = "withdraw";

    /// <summary>
    /// 队列是否会走 CDN 刷新。
    /// </summary>
    public static bool HasPurge(PendingPublish pending)
    {
        if (pending.IngestObjectList.Count > 0 || pending.WithdrawObjectList.Count > 0)
        {
            return true;
        }

        return pending.PurgeUrlList.Any(url =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 将 deploy 一行日志映射到命令步键；对不上则空。
    /// </summary>
    public static string? MatchDeployLine(string line, bool spa)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (line.Contains("构建中", StringComparison.Ordinal)
            || line.Contains("跳过构建", StringComparison.Ordinal))
        {
            return BuildKey;
        }

        if (!spa
            && (line.Contains("COS", StringComparison.Ordinal)
                || line.Contains("缺少 cos-nodejs-sdk", StringComparison.Ordinal)))
        {
            return CosKey;
        }

        if (line.Contains("打包 dist", StringComparison.Ordinal))
        {
            return PackKey;
        }

        if (line.Contains("上传到", StringComparison.Ordinal))
        {
            return UploadKey;
        }

        if (line.Contains("远端覆盖", StringComparison.Ordinal)
            || line.Contains("发布完成：", StringComparison.Ordinal))
        {
            return ReloadKey;
        }

        if (line.Contains("全部完成", StringComparison.Ordinal)
            || line.Contains("预演结束", StringComparison.Ordinal))
        {
            return ReloadKey;
        }

        return null;
    }

    /// <summary>
    /// 已完成权重对应的百分比，收在 0～100。
    /// </summary>
    public static int ToPercent(int completedWeight, int totalWeight)
    {
        return ToPercent(completedWeight, (double)totalWeight);
    }

    /// <summary>
    /// 已完成权重可为步内小数；收在 0～100。
    /// </summary>
    public static int ToPercent(double completedWeight, double totalWeight)
    {
        if (totalWeight <= 0)
        {
            return 0;
        }

        if (completedWeight <= 0)
        {
            return 0;
        }

        if (completedWeight >= totalWeight)
        {
            return 100;
        }

        return Math.Clamp((int)Math.Round(completedWeight * 100.0 / totalWeight), 1, 99);
    }
}

/// <summary>
/// 按规划表推进发布进度；百分比只跟真实命令步或步内真实输出，不定时假动画。
/// </summary>
public sealed class PublishProgressTracker
{
    private readonly IReadOnlyList<PublishStep> mStepList;
    private readonly IProgress<PublishProgress>? mProgress;
    private readonly HashSet<string> mCompletedSet = new(StringComparer.Ordinal);
    private readonly List<CommandStepTiming> mTimingList = new();
    private readonly Stopwatch mTotalWatch = Stopwatch.StartNew();
    private readonly Stopwatch mStepWatch = new();
    private readonly object mGate = new();
    private readonly Dictionary<string, int> mRetryCountDict = new(StringComparer.Ordinal);
    private readonly int mCosExpectedCount;
    private int mCurrentIndex;
    private double mStepFraction;
    private bool mHasBegun;
    private string? mTimingTitle;
    private string mDetail = "";
    private string? mInFlightRetryName;
    private int mInFlightRetries;
    private int mCosStartedCount;

    public PublishProgressTracker(PendingPublish pending, IProgress<PublishProgress>? progress)
        : this(PublishProgressPlanner.Plan(pending), progress, pending.IngestObjectList.Count)
    {
    }

    /// <summary>
    /// 按已排出的命令步推进；执行与发布共用。
    /// </summary>
    public PublishProgressTracker(IReadOnlyList<PublishStep> stepList, IProgress<PublishProgress>? progress)
        : this(stepList, progress, 0)
    {
    }

    /// <summary>
    /// 发布可传入待入库条数，供 COS 步内按文件推进。
    /// </summary>
    public PublishProgressTracker(
        IReadOnlyList<PublishStep> stepList,
        IProgress<PublishProgress>? progress,
        int cosExpectedCount)
    {
        mStepList = stepList;
        mProgress = progress;
        mCosExpectedCount = Math.Max(0, cosExpectedCount);
    }

    public IReadOnlyList<PublishStep> StepList => mStepList;

    /// <summary>
    /// 已结束步的实测耗时；进行中的当前步不在内。
    /// </summary>
    public IReadOnlyList<CommandStepTiming> TimingList
    {
        get
        {
            lock (mGate)
            {
                return mTimingList.ToArray();
            }
        }
    }

    /// <summary>
    /// 自创建追踪器起的总耗时。
    /// </summary>
    public TimeSpan Elapsed => mTotalWatch.Elapsed;

    /// <summary>
    /// 进入指定命令步，并补齐其前未报完的步。条停在该步起点，等步内分数或下一步。
    /// </summary>
    public void Begin(string key, string? detail = null)
    {
        lock (mGate)
        {
            var index = IndexOf(key);
            if (index < 0)
            {
                SetDetailUnlocked(detail);
                return;
            }

            if (mHasBegun && mCurrentIndex == index)
            {
                SetDetailUnlocked(detail);
                return;
            }

            CompleteBefore(index);
            CloseCurrentStepUnlocked();
            mCurrentIndex = index;
            mStepFraction = 0;
            mHasBegun = true;
            mTimingTitle = mStepList[index].Title;
            mStepWatch.Restart();
            if (detail != null)
            {
                mDetail = TrimDetail(detail);
            }

            ReportUnlocked();
        }
    }

    /// <summary>
    /// 当前资源台账改名正在重试；有次数才刷新进度窗统计。
    /// </summary>
    public void NoteLedgerRenameAttempt(string name, int retriesSoFar)
    {
        if (retriesSoFar <= 0 || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (mGate)
        {
            mInFlightRetryName = name;
            mInFlightRetries = retriesSoFar;
            ReportUnlocked();
        }
    }

    /// <summary>
    /// 当前资源台账改名结束，把重试次数并入累计。
    /// </summary>
    public void CommitLedgerRenameRetries(string name, int retries)
    {
        lock (mGate)
        {
            if (string.Equals(mInFlightRetryName, name, StringComparison.Ordinal))
            {
                mInFlightRetryName = null;
                mInFlightRetries = 0;
            }

            if (retries > 0 && !string.IsNullOrWhiteSpace(name))
            {
                mRetryCountDict.TryGetValue(name, out var previous);
                mRetryCountDict[name] = previous + retries;
            }

            ReportUnlocked();
        }
    }

    /// <summary>
    /// 只刷新当前步说明，不改百分比。
    /// </summary>
    public void SetDetail(string? detail)
    {
        lock (mGate)
        {
            SetDetailUnlocked(detail);
        }
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
    /// 按当前步真实进度填条；分数须单调不减。无真实输出时不要调用。
    /// </summary>
    public void SetFraction(double fraction, string? detail = null)
    {
        lock (mGate)
        {
            if (!mHasBegun)
            {
                return;
            }

            var next = Math.Clamp(fraction, 0, 1);
            if (next + 1e-6 < mStepFraction)
            {
                return;
            }

            mStepFraction = next;
            if (detail != null)
            {
                mDetail = TrimDetail(detail);
            }

            ReportUnlocked();
        }
    }

    /// <summary>
    /// 将当前步标为已完成。
    /// </summary>
    public void CompleteCurrent()
    {
        lock (mGate)
        {
            if (mCurrentIndex < 0 || mCurrentIndex >= mStepList.Count)
            {
                return;
            }

            mCompletedSet.Add(mStepList[mCurrentIndex].Key);
            mStepFraction = 1;
            ReportUnlocked();
        }
    }

    /// <summary>
    /// 收到 deploy 一行输出：能对上则切步，否则只更新说明。
    /// </summary>
    public void OnDeployLine(string line, bool spa)
    {
        var key = PublishProgressPlanner.MatchDeployLine(line, spa);
        if (key == null)
        {
            SetDetail(line);
            return;
        }

        if (string.Equals(key, PublishProgressPlanner.CosKey, StringComparison.Ordinal))
        {
            Begin(key, line);
            if (line.Contains("COS 上传", StringComparison.Ordinal))
            {
                AdvanceCosUpload(line);
            }
            else if (line.Contains("COS 完成", StringComparison.Ordinal))
            {
                SetFraction(1, line);
            }

            return;
        }

        Begin(key, line);
    }

    /// <summary>
    /// 每条「COS 上传」在整步份额内前进一步；条数多于预估时按已见条数扩分母。
    /// </summary>
    private void AdvanceCosUpload(string line)
    {
        int index;
        int total;
        lock (mGate)
        {
            index = mCosStartedCount;
            mCosStartedCount++;
            total = Math.Max(Math.Max(1, mCosExpectedCount), mCosStartedCount);
        }

        SetBatchFraction(index, total, 0, line);
    }

    /// <summary>
    /// 全部命令步走完。
    /// </summary>
    public void Finish(string? detail = null)
    {
        lock (mGate)
        {
            CompleteBefore(mStepList.Count);
            CloseCurrentStepUnlocked();
            mTotalWatch.Stop();
            mCurrentIndex = Math.Max(0, mStepList.Count - 1);
            mStepFraction = 1;
            mHasBegun = true;
            mDetail = TrimDetail(detail ?? "发布上线完成。");
            ReportUnlocked();
        }
    }

    /// <summary>
    /// 失败时停在当前步，说明失败原因。
    /// </summary>
    public void Fail(string? detail)
    {
        lock (mGate)
        {
            CloseCurrentStepUnlocked();
            mTotalWatch.Stop();
            mDetail = TrimDetail(detail ?? "发布失败。");
            ReportUnlocked();
        }
    }

    /// <summary>
    /// 写入耗时并记入运行日志。
    /// </summary>
    public BatchRunResult Stamp(BatchRunResult result, string kind)
    {
        lock (mGate)
        {
            CloseCurrentStepUnlocked();
            mTotalWatch.Stop();
        }

        var stamped = result.WithTiming(CommandStepTiming.CoalesceByTitle(TimingList), Elapsed);
        RunLog.AppendOutcome(kind, stamped);
        return stamped;
    }

    /// <summary>
    /// 把刚结束的步记入耗时表。
    /// </summary>
    private void CloseCurrentStepUnlocked()
    {
        if (mTimingTitle == null)
        {
            return;
        }

        mStepWatch.Stop();
        mTimingList.Add(new CommandStepTiming
        {
            Title = mTimingTitle,
            Elapsed = mStepWatch.Elapsed
        });
        mTimingTitle = null;
    }

    /// <summary>
    /// 查找命令步下标；没有则 -1。
    /// </summary>
    private int IndexOf(string key)
    {
        for (var i = 0; i < mStepList.Count; i++)
        {
            if (string.Equals(mStepList[i].Key, key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 把 index 之前的步全部记为已完成。
    /// </summary>
    private void CompleteBefore(int index)
    {
        var last = Math.Min(index, mStepList.Count);
        for (var i = 0; i < last; i++)
        {
            mCompletedSet.Add(mStepList[i].Key);
        }
    }

    /// <summary>
    /// 步权至少为 1，避免 0 权把条卡死。
    /// </summary>
    private static int StepWeight(PublishStep step)
    {
        return step.Weight > 0 ? step.Weight : 1;
    }

    /// <summary>
    /// 只改说明；调用方已持锁。
    /// </summary>
    private void SetDetailUnlocked(string? detail)
    {
        if (detail == null)
        {
            return;
        }

        mDetail = TrimDetail(detail);
        ReportUnlocked();
    }

    /// <summary>
    /// 推送一条进度快照；调用方已持锁。
    /// </summary>
    private void ReportUnlocked()
    {
        if (mProgress == null || mStepList.Count == 0)
        {
            return;
        }

        var current = mStepList[Math.Clamp(mCurrentIndex, 0, mStepList.Count - 1)];
        var allDone = mCompletedSet.Count >= mStepList.Count;
        var currentDone = mCompletedSet.Contains(current.Key);
        var completed = 0.0;
        for (var i = 0; i < mCurrentIndex; i++)
        {
            completed += StepWeight(mStepList[i]);
        }

        var currentWeight = StepWeight(current);
        completed += currentDone ? currentWeight : currentWeight * mStepFraction;
        var totalWeight = 0;
        foreach (var step in mStepList)
        {
            totalWeight += StepWeight(step);
        }

        mProgress.Report(new PublishProgress
        {
            Percent = allDone
                ? 100
                : PublishProgressPlanner.ToPercent(completed, totalWeight),
            CurrentStep = mCurrentIndex + 1,
            TotalStep = mStepList.Count,
            Title = current.Title,
            Detail = mDetail,
            RetrySummary = BuildRetrySummaryUnlocked()
        });
    }

    /// <summary>
    /// 拼进度窗重试行；没有任何重试时为空。
    /// </summary>
    private string BuildRetrySummaryUnlocked()
    {
        var countDict = new Dictionary<string, int>(mRetryCountDict, StringComparer.Ordinal);
        if (mInFlightRetries > 0 && !string.IsNullOrWhiteSpace(mInFlightRetryName))
        {
            countDict.TryGetValue(mInFlightRetryName, out var previous);
            countDict[mInFlightRetryName] = previous + mInFlightRetries;
        }

        if (countDict.Count == 0)
        {
            return "";
        }

        var totalRetries = countDict.Values.Sum();
        var lineList = new List<string>
        {
            $"台账改名重试{countDict.Count}项，共{totalRetries}次"
        };
        foreach (var pair in countDict)
        {
            lineList.Add($"{pair.Key}：{pair.Value} 次");
        }

        return string.Join("\n", lineList);
    }

    /// <summary>
    /// 进度说明只留一行，避免把整段构建日志塞进窗口。
    /// </summary>
    private static string TrimDetail(string text)
    {
        var line = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (line.Length <= 160)
        {
            return line;
        }

        return line[..157] + "…";
    }
}
