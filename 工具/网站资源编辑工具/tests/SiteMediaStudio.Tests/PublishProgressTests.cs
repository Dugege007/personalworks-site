using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PublishProgressTests
{
    [Fact]
    public void Plan_Spa_OmitsCos_KeepsDeployThenFinish()
    {
        var steps = PublishProgressPlanner.Plan(new PendingPublish { Deploy = "spa" });
        var keys = steps.Select(step => step.Key).ToList();
        Assert.Equal(PublishProgressPlanner.PrepareKey, keys[0]);
        Assert.Equal(PublishProgressPlanner.BuildKey, keys[1]);
        Assert.DoesNotContain(PublishProgressPlanner.TidyKey, keys);
        Assert.DoesNotContain(PublishProgressPlanner.CosKey, keys);
        Assert.Contains(PublishProgressPlanner.PackKey, keys);
        Assert.Contains(PublishProgressPlanner.UploadKey, keys);
        Assert.Contains(PublishProgressPlanner.ReloadKey, keys);
        Assert.DoesNotContain(PublishProgressPlanner.PurgeKey, keys);
        Assert.Equal(PublishProgressPlanner.FinishKey, keys[^1]);
    }

    [Fact]
    public void Plan_FullWithWithdrawAndUrl_HasCosWithdrawPurge()
    {
        var steps = PublishProgressPlanner.Plan(new PendingPublish
        {
            Deploy = "full",
            IngestObjectList = { "a/01.png" },
            WithdrawObjectList = { "a/02.png", "a/03.png" },
            PurgeUrlList = { "https://cdn.example/a/01.png" }
        });
        var keys = steps.Select(step => step.Key).ToList();
        Assert.Contains(PublishProgressPlanner.CosKey, keys);
        Assert.Equal(PublishProgressPlanner.TidyKey, keys[keys.IndexOf(PublishProgressPlanner.PrepareKey) + 1]);
        Assert.Equal("核对撤下引用", steps.First(step => step.Key == PublishProgressPlanner.TidyKey).Title);
        Assert.Equal(PublishProgressPlanner.WithdrawKey, keys[keys.IndexOf(PublishProgressPlanner.ReloadKey) + 1]);
        Assert.Single(keys, key => key == PublishProgressPlanner.WithdrawKey);
        Assert.Contains(PublishProgressPlanner.PurgeKey, keys);
        Assert.Equal("撤下", steps.First(step => step.Key == PublishProgressPlanner.WithdrawKey).Title);
        Assert.Equal(4, steps.First(step => step.Key == PublishProgressPlanner.WithdrawKey).Weight);
        Assert.Equal(PublishProgressPlanner.CosMinWeight, steps.First(step => step.Key == PublishProgressPlanner.CosKey).Weight);
    }

    [Fact]
    public void Plan_ManyIngestImages_CosWeightScalesAndOutweighsBuild()
    {
        var pending = new PendingPublish { Deploy = "full" };
        for (var i = 1; i <= 20; i++)
        {
            pending.IngestObjectList.Add($"a/{i:00}.webp");
        }

        var steps = PublishProgressPlanner.Plan(pending);
        var cos = steps.First(step => step.Key == PublishProgressPlanner.CosKey);
        var build = steps.First(step => step.Key == PublishProgressPlanner.BuildKey);
        Assert.Equal(20, cos.Weight);
        Assert.True(cos.Weight > build.Weight);
        Assert.Equal(PublishProgressPlanner.BuildWeight, build.Weight);
    }

    [Fact]
    public void Plan_IngestVideos_HeavierThanImages()
    {
        var steps = PublishProgressPlanner.Plan(new PendingPublish
        {
            Deploy = "full",
            IngestObjectList = { "digital-twin/a/01.mp4", "digital-twin/a/02.mp4" }
        });
        var cos = steps.First(step => step.Key == PublishProgressPlanner.CosKey);
        Assert.Equal(PublishProgressPlanner.CosVideoWeight * 2, cos.Weight);
    }

    [Theory]
    [InlineData("构建中（VITE_ASSET_BASE=https://cdn.example）", false, PublishProgressPlanner.BuildKey)]
    [InlineData("跳过构建，使用已有 dist/", true, PublishProgressPlanner.BuildKey)]
    [InlineData("COS 上传 landscape-rendering/a/01.webp", false, PublishProgressPlanner.CosKey)]
    [InlineData("COS 上传 landscape-rendering/a/01.webp", true, null)]
    [InlineData("打包 dist（排除 placeholders，媒体不进轻量盘）", false, PublishProgressPlanner.PackKey)]
    [InlineData("上传到 deploy@host:/tmp/personal-site-dist.tgz", false, PublishProgressPlanner.UploadKey)]
    [InlineData("远端覆盖网站根目录并重载 Nginx", false, PublishProgressPlanner.ReloadKey)]
    [InlineData("全部完成。", false, PublishProgressPlanner.ReloadKey)]
    [InlineData("vite v5 built in 8s", false, null)]
    public void MatchDeployLine_MapsKnownMarkers(string line, bool spa, string? expected)
    {
        Assert.Equal(expected, PublishProgressPlanner.MatchDeployLine(line, spa));
    }

    [Fact]
    public void ToPercent_ClampsAndScales()
    {
        Assert.Equal(0, PublishProgressPlanner.ToPercent(0, 8));
        Assert.Equal(100, PublishProgressPlanner.ToPercent(8, 8));
        Assert.Equal(25, PublishProgressPlanner.ToPercent(2, 8));
        Assert.Equal(0, PublishProgressPlanner.ToPercent(1, 0));
        Assert.Equal(12, PublishProgressPlanner.ToPercent(0.5, 4));
    }

    [Fact]
    public void Tracker_SetFraction_StaysInsideCurrentStep()
    {
        var progress = new CollectingProgress();
        var tracker = new PublishProgressTracker(new PendingPublish { Deploy = "spa" }, progress);
        tracker.Begin(PublishProgressPlanner.PrepareKey, "正在校验发布队列。");
        Assert.Equal(0, progress.SnapshotList[^1].Percent);
        tracker.SetFraction(0.4, "校验中");
        var mid = progress.SnapshotList[^1];
        tracker.Begin(PublishProgressPlanner.BuildKey, "构建中");
        Assert.True(mid.Percent > 0);
        Assert.True(mid.Percent < progress.SnapshotList[^1].Percent);
        Assert.Equal("校验中", mid.Detail);
    }

    [Fact]
    public void Tracker_BeginAndFinish_ReportStepAndPercent()
    {
        var progress = new CollectingProgress();
        var pending = new PendingPublish { Deploy = "spa" };
        var tracker = new PublishProgressTracker(pending, progress);
        tracker.Begin(PublishProgressPlanner.PrepareKey, "正在校验发布队列。");
        tracker.Begin(PublishProgressPlanner.BuildKey, "构建中");
        tracker.OnDeployLine("打包 dist（排除 placeholders，媒体不进轻量盘）", spa: true);
        tracker.Finish("发布上线完成。");

        Assert.NotEmpty(progress.SnapshotList);
        Assert.Equal("准备发布", progress.SnapshotList[0].Title);
        Assert.Contains(progress.SnapshotList, item => item.Title == "构建静态包");
        Assert.Contains(progress.SnapshotList, item => item.Title == "打包站点");
        Assert.Equal(100, progress.SnapshotList[^1].Percent);
        Assert.Equal("发布上线完成。", progress.SnapshotList[^1].Detail);
        Assert.True(progress.SnapshotList[0].Percent < progress.SnapshotList[^1].Percent);
    }

    [Fact]
    public void Tracker_BeginSameStepAgain_KeepsFractionAndUpdatesDetail()
    {
        var progress = new CollectingProgress();
        var tracker = new PublishProgressTracker(new PendingPublish { Deploy = "spa" }, progress);
        tracker.Begin(PublishProgressPlanner.PrepareKey, "正在校验发布队列。");
        tracker.SetFraction(0.5, "校验中");
        var mid = progress.SnapshotList[^1];
        tracker.Begin(PublishProgressPlanner.PrepareKey, "仍在校验。");
        var again = progress.SnapshotList[^1];
        Assert.Equal(mid.Percent, again.Percent);
        Assert.Equal("仍在校验。", again.Detail);
    }

    [Fact]
    public void Tracker_CosUploadLines_AdvanceInsideCosStep()
    {
        var pending = new PendingPublish { Deploy = "full" };
        for (var i = 1; i <= 10; i++)
        {
            pending.IngestObjectList.Add($"a/{i:00}.webp");
        }

        var progress = new CollectingProgress();
        var tracker = new PublishProgressTracker(pending, progress);
        tracker.Begin(PublishProgressPlanner.PrepareKey);
        tracker.Begin(PublishProgressPlanner.BuildKey, "构建中");
        tracker.OnDeployLine("COS 上传 a/01.webp", spa: false);
        var first = progress.SnapshotList[^1];
        tracker.OnDeployLine("COS 上传 a/02.webp", spa: false);
        var second = progress.SnapshotList[^1];
        tracker.OnDeployLine("COS 完成：上传 2，跳过未改 8", spa: false);
        var done = progress.SnapshotList[^1];

        Assert.Equal("同步媒体到 COS", first.Title);
        Assert.Equal("同步媒体到 COS", second.Title);
        Assert.True(second.Percent > first.Percent);
        Assert.True(done.Percent > second.Percent);
        Assert.Equal(27, first.Percent);
        Assert.Equal(30, second.Percent);
        Assert.Equal(58, done.Percent);

        tracker.Finish("发布上线完成。");
        Assert.Single(tracker.TimingList, item => item.Title == "同步媒体到 COS");
        var stamped = tracker.Stamp(new BatchRunResult { Ok = true }, "发布");
        Assert.Single(stamped.StepTimingList, item => item.Title == "同步媒体到 COS");
    }

    [Fact]
    public void Tracker_UnmatchedDeployLine_OnlyUpdatesDetail()
    {
        var progress = new CollectingProgress();
        var tracker = new PublishProgressTracker(
            new PendingPublish { Deploy = "full" },
            progress);
        tracker.Begin(PublishProgressPlanner.BuildKey, "构建中");
        var afterBegin = progress.SnapshotList[^1];
        tracker.OnDeployLine("vite v5 built in 8s", spa: false);
        var afterLine = progress.SnapshotList[^1];
        Assert.Equal(afterBegin.Title, afterLine.Title);
        Assert.Equal(afterBegin.Percent, afterLine.Percent);
        Assert.Equal("vite v5 built in 8s", afterLine.Detail);
        Assert.Equal("", afterLine.RetrySummary);
    }

    [Fact]
    public void Tracker_LedgerRenameRetry_ShowsSummaryOnlyAfterAttempt()
    {
        var progress = new CollectingProgress();
        var tracker = new PublishProgressTracker(new PendingPublish { Deploy = "spa" }, progress);
        tracker.Begin(PublishProgressPlanner.PrepareKey, "正在校验发布队列。");
        Assert.Equal("", progress.SnapshotList[^1].RetrySummary);

        tracker.NoteLedgerRenameAttempt("井盖详图.jpg", 1);
        var mid = progress.SnapshotList[^1];
        Assert.Contains("台账改名重试1项，共1次", mid.RetrySummary, StringComparison.Ordinal);
        Assert.Contains("井盖详图.jpg：1 次", mid.RetrySummary, StringComparison.Ordinal);
        Assert.Equal("正在校验发布队列。", mid.Detail);

        tracker.CommitLedgerRenameRetries("井盖详图.jpg", 2);
        var done = progress.SnapshotList[^1];
        Assert.Contains("台账改名重试1项，共2次", done.RetrySummary, StringComparison.Ordinal);
        Assert.Contains("井盖详图.jpg：2 次", done.RetrySummary, StringComparison.Ordinal);
    }

    /// <summary>
    /// 同步收集进度，避免测试线程没有同步上下文时漏报。
    /// </summary>
    private sealed class CollectingProgress : IProgress<PublishProgress>
    {
        public List<PublishProgress> SnapshotList { get; } = new();

        public void Report(PublishProgress value)
        {
            SnapshotList.Add(value);
        }
    }
}
