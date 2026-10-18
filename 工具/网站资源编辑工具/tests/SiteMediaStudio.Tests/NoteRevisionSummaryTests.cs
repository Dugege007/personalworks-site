using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteRevisionSummaryTests
{
    [Fact]
    public void Generate_NamesHeadingImageAndProse()
    {
        var diff = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline(),
            new NoteRevisionBaseline
            {
                Body = "# 传感器阈值\n\n补了一段说明。\n\n![](图.png)\n",
                Images = { "图.png" }
            });

        var summary = HeuristicRevisionSummaryGenerator.Instance.Generate(diff);

        Assert.Equal("新增小节「传感器阈值」，新增 1 张配图，更新了正文。", summary);
        Assert.True(diff.ImageReferencesChanged);
    }

    [Fact]
    public void Generate_NamesUpToTwoHeadingsAndCountsTheRest()
    {
        var two = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline(),
            new NoteRevisionBaseline { Body = "# 甲\n\n# 乙\n" });
        Assert.Equal("新增小节「甲」「乙」。", HeuristicRevisionSummaryGenerator.Instance.Generate(two));

        var heading = new string('标', 30);
        var many = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline(),
            new NoteRevisionBaseline
            {
                Body = "# " + heading + "\n# " + heading + "乙\n# " + heading + "丙\n\n正文\n"
            });
        var summary = HeuristicRevisionSummaryGenerator.Instance.Generate(many);
        Assert.Contains("新增 3 个小节", summary, StringComparison.Ordinal);
        Assert.Contains("更新了正文", summary, StringComparison.Ordinal);
        Assert.DoesNotContain(heading, summary, StringComparison.Ordinal);
        Assert.True(summary.Length <= NoteRevisionSummaries.MaxLength);
    }

    [Fact]
    public void Generate_IgnoresNewlineKindAndTrailingSpace()
    {
        var diff = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Body = "有改动\n" },
            new NoteRevisionBaseline { Body = "有改动 \r\n" });

        Assert.False(diff.HasMeaningfulChange);
        Assert.Equal("", HeuristicRevisionSummaryGenerator.Instance.Generate(diff));
    }

    [Fact]
    public void Generate_DescribesTitleLeadOrderRemovalAndImageReference()
    {
        var renamed = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Title = "甲" },
            new NoteRevisionBaseline { Title = "乙" });
        Assert.Equal("展示标题改为「乙」。", HeuristicRevisionSummaryGenerator.Instance.Generate(renamed));

        var cleared = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Title = "甲" },
            new NoteRevisionBaseline());
        Assert.Equal("清空了展示标题。", HeuristicRevisionSummaryGenerator.Instance.Generate(cleared));

        var lead = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Lead = "旧导语" },
            new NoteRevisionBaseline { Lead = "新导语" });
        Assert.Equal("更新了导语。", HeuristicRevisionSummaryGenerator.Instance.Generate(lead));

        var order = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Body = "# 甲\n\n# 乙\n" },
            new NoteRevisionBaseline { Body = "# 乙\n\n# 甲\n" });
        Assert.True(order.HeadingOrderChanged);
        Assert.Empty(order.AddedHeadings);
        Assert.Equal("调整了小节顺序。", HeuristicRevisionSummaryGenerator.Instance.Generate(order));

        var removed = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Images = { "a.png", "b.png" } },
            new NoteRevisionBaseline { Images = { "a.png" } });
        Assert.Equal("移除 1 张配图。", HeuristicRevisionSummaryGenerator.Instance.Generate(removed));

        var reference = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Body = "见图。\n" },
            new NoteRevisionBaseline { Body = "见图。\n\n![](a.png)\n" });
        Assert.Equal("调整了配图引用。", HeuristicRevisionSummaryGenerator.Instance.Generate(reference));
    }

    [Fact]
    public void Generate_SkipsHeadingInsideFenceAndStaysWithinLength()
    {
        var fenced = NoteRevisionBaselineCapture.Compare(
            new NoteRevisionBaseline { Body = "正文\n" },
            new NoteRevisionBaseline { Body = "正文\n\n```\n# 不是标题\n```\n" });
        Assert.Empty(fenced.AddedHeadings);
        Assert.True(fenced.ProseChanged);
        Assert.Equal("更新了正文。", HeuristicRevisionSummaryGenerator.Instance.Generate(fenced));

        var crowded = new NoteRevisionDiff
        {
            TitleChanged = true,
            PreviousTitle = "旧",
            CurrentTitle = new string('题', 40),
            LeadChanged = true,
            ProseChanged = true,
            HeadingOrderChanged = true,
            AddedHeadings = new[] { new string('甲', 40), new string('乙', 40) },
            RemovedHeadings = new[] { new string('丙', 40), new string('丁', 40) },
            AddedImages = new[] { "a.png", "b.png", "c.png" },
            RemovedImages = new[] { "d.png" },
            ImageReferencesChanged = true
        };
        var summary = HeuristicRevisionSummaryGenerator.Instance.Generate(crowded);
        Assert.False(string.IsNullOrWhiteSpace(summary));
        Assert.True(summary.Length <= NoteRevisionSummaries.MaxLength);
        Assert.StartsWith("展示标题改为", summary);
    }
}
