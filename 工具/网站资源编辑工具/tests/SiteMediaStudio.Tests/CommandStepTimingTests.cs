using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class CommandStepTimingTests
{
    [Theory]
    [InlineData(0, "不足 0.1 秒")]
    [InlineData(0.05, "不足 0.1 秒")]
    [InlineData(0.4, "0.4 秒")]
    [InlineData(12.4, "12 秒")]
    [InlineData(75, "1 分 15 秒")]
    public void FormatElapsed_UsesChineseUnits(double seconds, string expected)
    {
        Assert.Equal(expected, CommandStepTiming.FormatElapsed(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Tracker_BeginThenFinish_RecordsEachStep()
    {
        var progress = new CollectingProgress();
        var tracker = new PublishProgressTracker(new PendingPublish { Deploy = "spa" }, progress);
        tracker.Begin(PublishProgressPlanner.PrepareKey, "准备");
        tracker.Begin(PublishProgressPlanner.BuildKey, "构建");
        tracker.Finish("发布上线完成。");

        Assert.True(tracker.TimingList.Count >= 2);
        Assert.Equal("准备发布", tracker.TimingList[0].Title);
        Assert.Contains(tracker.TimingList, item => item.Title == "构建静态包");
        Assert.True(tracker.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void CoalesceByTitle_MergesConsecutiveSameStep()
    {
        var merged = CommandStepTiming.CoalesceByTitle(
        [
            new CommandStepTiming { Title = "同步媒体到 COS", Elapsed = TimeSpan.FromSeconds(25) },
            new CommandStepTiming { Title = "同步媒体到 COS", Elapsed = TimeSpan.FromSeconds(0.05) },
            new CommandStepTiming { Title = "同步媒体到 COS", Elapsed = TimeSpan.FromSeconds(9.3) },
            new CommandStepTiming { Title = "打包站点", Elapsed = TimeSpan.FromSeconds(2) }
        ]);
        Assert.Equal(2, merged.Count);
        Assert.Equal("同步媒体到 COS", merged[0].Title);
        Assert.Equal(34.35, merged[0].Elapsed.TotalSeconds, 2);
        Assert.Equal("打包站点", merged[1].Title);
    }

    [Fact]
    public void AppendBlock_WritesStepsThenTotal()
    {
        var lines = new List<string>();
        CommandStepTiming.AppendBlock(
            lines,
            [
                new CommandStepTiming { Title = "压码 clip.mov", Elapsed = TimeSpan.FromSeconds(90) }
            ],
            TimeSpan.FromSeconds(93));
        Assert.Equal("各步耗时：", lines[0]);
        Assert.Equal("压码 clip.mov  1 分 30 秒", lines[1]);
        Assert.Equal("合计 1 分 33 秒", lines[2]);
    }

    [Fact]
    public void ResourceAppendBlock_WritesSuccessAndFailMarks()
    {
        var lines = new List<string>();
        CommandResourceResult.AppendBlock(
            lines,
            [
                CommandResourceResult.Of("原图.jpg", true),
                CommandResourceResult.Of("clip.mov", false)
            ]);
        Assert.Equal("资源：", lines[0]);
        Assert.Equal("[成功] 原图.jpg", lines[1]);
        Assert.Equal("[失败] clip.mov", lines[2]);
    }

    private sealed class CollectingProgress : IProgress<PublishProgress>
    {
        public void Report(PublishProgress value)
        {
        }
    }
}
