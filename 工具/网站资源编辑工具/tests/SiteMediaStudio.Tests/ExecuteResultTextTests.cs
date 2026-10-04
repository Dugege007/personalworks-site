using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ExecuteResultTextTests
{
    [Fact]
    public void FormatCompact_UsesShortSeconds()
    {
        Assert.Equal("不足0.1s", CommandStepTiming.FormatCompact(TimeSpan.Zero));
        Assert.Equal("不足0.1s", CommandStepTiming.FormatCompact(TimeSpan.FromSeconds(0.1)));
        Assert.Equal("3.3s", CommandStepTiming.FormatCompact(TimeSpan.FromSeconds(3.3)));
        Assert.Equal("补丁", CommandStepTiming.DisplayTitle("内容补丁"));
    }

    [Fact]
    public void BuildSuccess_OmitsUnusedRowsAndKeepsUsedCounts()
    {
        var lines = ExecuteResultComposer.BuildSuccess(
            new BatchRunResult
            {
                Ok = true,
                IngestOk = 3,
                HideOk = 0,
                Elapsed = TimeSpan.FromSeconds(3.7),
                StepTimingList =
                [
                    new CommandStepTiming { Title = "压图", Elapsed = TimeSpan.FromSeconds(3.3) },
                    new CommandStepTiming { Title = "内容补丁", Elapsed = TimeSpan.FromSeconds(0.04) },
                    new CommandStepTiming { Title = "收尾", Elapsed = TimeSpan.FromSeconds(0.2) }
                ],
                ResourceList =
                [
                    CommandResourceResult.Of("01_37.jpg", true),
                    CommandResourceResult.Of("clip.mov", false)
                ]
            },
            new ExecuteResultCounts(3, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            Array.Empty<ResultTextLine>(),
            @"C:\Users\you\AppData\Roaming\SiteMediaStudio\run.log");

        var text = ExecuteResultComposer.ToPlainText(lines);
        Assert.StartsWith("执行已完成！（需要再点“发布上线”按钮才会更新正式网页）", text, StringComparison.Ordinal);
        Assert.Contains("上页：3/3", text, StringComparison.Ordinal);
        Assert.DoesNotContain("显示：", text, StringComparison.Ordinal);
        Assert.DoesNotContain("隐藏：", text, StringComparison.Ordinal);
        Assert.DoesNotContain("待撤：", text, StringComparison.Ordinal);
        Assert.DoesNotContain("回收：", text, StringComparison.Ordinal);
        Assert.DoesNotContain("文案：", text, StringComparison.Ordinal);
        Assert.Contains("各步骤耗时：", text, StringComparison.Ordinal);
        Assert.Contains("压图：3.3s", text, StringComparison.Ordinal);
        Assert.Contains("补丁：不足0.1s", text, StringComparison.Ordinal);
        Assert.Contains("收尾：0.2s", text, StringComparison.Ordinal);
        Assert.Contains("合计：3.7s", text, StringComparison.Ordinal);
        Assert.Contains("资源2项：", text, StringComparison.Ordinal);
        Assert.Contains("[成功] 01_37.jpg", text, StringComparison.Ordinal);
        Assert.Contains("[失败] clip.mov", text, StringComparison.Ordinal);
        Assert.Contains(@"日志已写入本机：C:\Users\you\AppData\Roaming\SiteMediaStudio\run.log", text, StringComparison.Ordinal);
        Assert.Equal(ResultLineKind.CountIngest, lines.First(line => line.Text.StartsWith("上页：", StringComparison.Ordinal)).Kind);
        Assert.Equal(ResultLineKind.Success, lines.First(line => line.Text.StartsWith("[成功]", StringComparison.Ordinal)).Kind);
        Assert.Equal(ResultLineKind.Failure, lines.First(line => line.Text.StartsWith("[失败]", StringComparison.Ordinal)).Kind);
        Assert.DoesNotContain("台账改名重试", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSuccess_ListsLedgerRenameRetriesWithoutMarkingFailure()
    {
        var lines = ExecuteResultComposer.BuildSuccess(
            new BatchRunResult
            {
                Ok = true,
                IngestOk = 2,
                ResourceList =
                [
                    CommandResourceResult.Of("01.jpg", true, 2),
                    CommandResourceResult.Of("02.jpg", true)
                ]
            },
            new ExecuteResultCounts(2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            Array.Empty<ResultTextLine>(),
            "run.log");
        var text = ExecuteResultComposer.ToPlainText(lines);
        Assert.Contains("[成功] 01.jpg", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[失败] 01.jpg", text, StringComparison.Ordinal);
        Assert.Contains("台账改名重试1项：", text, StringComparison.Ordinal);
        Assert.Contains("01.jpg：2 次", text, StringComparison.Ordinal);
        Assert.DoesNotContain("02.jpg：", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSuccess_ShowsCopyChanges()
    {
        var copyLines = new[]
        {
            new ResultTextLine("[成功] 小瓦窑湿地公园的名称：旧名更改为新名", ResultLineKind.Success)
        };
        var lines = ExecuteResultComposer.BuildSuccess(
            new BatchRunResult { Ok = true },
            new ExecuteResultCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1),
            copyLines,
            "run.log");
        var text = ExecuteResultComposer.ToPlainText(lines);
        Assert.Contains("文案：1/1", text, StringComparison.Ordinal);
        Assert.Contains("文案1条：", text, StringComparison.Ordinal);
        Assert.Contains("小瓦窑湿地公园的名称：旧名更改为新名", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCopyLines_WritesNameAndDescription()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-copy-result-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var profile = new WorkspaceProfile
            {
                ProfilePath = Path.Combine(dir, "profile.json"),
                ResolvedRoot = dir,
                Channels = [new ChannelProfile { Key = "landscape-design", Zh = "景观设计" }]
            };
            var session = new WorkspaceSession
            {
                Profile = profile,
                Works =
                [
                    new WorkCatalogItem
                    {
                        Id = "xiaowayao",
                        Channel = "landscape-design",
                        Title = "小瓦窑湿地公园",
                        Summary = "旧描述",
                        Media =
                        [
                            new WorkMediaItem
                            {
                                Src = "landscape-design/xiaowayao/01.webp",
                                Label = "效果图 01",
                                DisplayName = "旧名",
                                Description = "旧导语"
                            }
                        ]
                    }
                ]
            };
            var draft = new CopyDraftFile
            {
                ProfilePath = profile.ProfilePath,
                WorkspaceRoot = profile.ResolvedRoot,
                Entries =
                [
                    new CopyDraftEntry
                    {
                        Key = CopyDraftStore.MediaKey("landscape-design", "xiaowayao", "landscape-design/xiaowayao/01.webp"),
                        Title = "新名",
                        Description = "新导语",
                        PublishedTitle = "旧名",
                        PublishedDescription = "旧导语"
                    }
                ]
            };
            var lines = ExecuteResultComposer.BuildCopyLines(
                session,
                new IntentDocument
                {
                    Items =
                    [
                        new IntentItem
                        {
                            Intent = MediaIntentCodes.CopyUpdate,
                            Target = "media",
                            Channel = "landscape-design",
                            WorkId = "xiaowayao",
                            Object = "landscape-design/xiaowayao/01.webp",
                            Title = "新名",
                            Description = "新导语",
                            LabelBefore = "效果图 01"
                        }
                    ]
                },
                draft);
            Assert.Equal(2, lines.Count);
            Assert.Equal("[成功] 旧名的名称：旧名更改为新名", lines[0].Text);
            Assert.Equal("[成功] 旧名的描述：旧导语更改为新导语", lines[1].Text);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
