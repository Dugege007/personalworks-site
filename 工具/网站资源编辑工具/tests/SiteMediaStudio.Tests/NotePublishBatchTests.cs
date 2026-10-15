using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NotePublishBatchTests
{
    [Fact]
    public void BlockReason_ListsEveryFailureAndBlocksTheBatch()
    {
        var reason = NotePublishBatch.BlockReason(new[]
        {
            new NotePublishCheck
            {
                Name = "清晨散步",
                HasBodyMark = true,
                ImageBlock = "本篇有 1 张配图缺失，不能上页。"
            },
            new NotePublishCheck
            {
                Name = "控制图",
                HasBodyMark = true,
                ImageBlock = "本篇有 2 处引用超出笔记文件夹，不能上页。"
            }
        });

        Assert.Equal(
            "「清晨散步」本篇有 1 张配图缺失，不能上页。\n「控制图」本篇有 2 处引用超出笔记文件夹，不能上页。",
            reason.Replace("\r\n", "\n"));
    }

    [Fact]
    public void BlockReason_IncludesBodyMarkAndImageProblemsTogether()
    {
        var reason = NotePublishBatch.BlockReason(new[]
        {
            new NotePublishCheck
            {
                Name = "只有配图",
                HasBodyMark = false,
                ImageBlock = ""
            },
            new NotePublishCheck
            {
                Name = "文件夹外",
                HasBodyMark = true,
                ImageBlock = "本篇有 1 处引用超出笔记文件夹，不能上页。"
            },
            new NotePublishCheck
            {
                Name = "可以上页",
                HasBodyMark = true,
                ImageBlock = ""
            }
        });

        Assert.Contains("「只有配图」须标记正文.md 才能上页这篇心得，不能只标记配图。", reason, StringComparison.Ordinal);
        Assert.Contains("「文件夹外」本篇有 1 处引用超出笔记文件夹，不能上页。", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("可以上页", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockReason_IsEmptyWhenEveryNoteCanPublish()
    {
        var reason = NotePublishBatch.BlockReason(new[]
        {
            new NotePublishCheck { Name = "清晨散步", HasBodyMark = true },
            new NotePublishCheck { Name = "控制图", HasBodyMark = true, ImageBlock = "   " }
        });

        Assert.Equal("", reason);
    }

    [Fact]
    public void BlockReason_NamesAnUnrecognizedMark()
    {
        var reason = NotePublishBatch.BlockReason(new[]
        {
            new NotePublishCheck { Name = "未识别的笔记", Recognized = false, HasBodyMark = false }
        });

        Assert.Equal("「未识别的笔记」有标记无法对应到笔记文件夹，不能上页。", reason);
    }
}
