using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteImagePromptTests
{
    [Fact]
    public void Footer_CountsOverLimitMissingAndOutsideReferences()
    {
        var folder = TempDir();
        var limit = NoteImageThreshold.DefaultImageMaxBytes;
        WriteSized(Path.Combine(folder, "exact.jpg"), limit);
        WriteSized(Path.Combine(folder, "over.png"), limit + 1);
        WriteBody(folder, """
            # 一篇

            ![缺](gone.png)
            ![外](../other/a.png)
            ![根](/other/b.png)
            """);

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal("本篇有 1 张配图超限\n本篇有 1 张配图缺失\n本篇有 2 处引用超出笔记文件夹", NoteImagePrompt.FormatFooter(report));
        Assert.Equal("缺失配图：gone.png\n引用超出笔记文件夹：../other/a.png、/other/b.png", NoteImagePrompt.FormatIssueText(report));
        Assert.Equal("本篇有 1 张配图缺失，本篇有 2 处引用超出笔记文件夹，不能上页。", NoteImagePrompt.PublishBlockReason(report));
        Assert.Equal("", NoteImagePrompt.FormatFooter(new NoteImageAuditReport()));
        Assert.Equal("", NoteImagePrompt.PublishBlockReason(new NoteImageAuditReport()));
    }

    [Fact]
    public void WarningAndBadge_UseStrictSizeLimit()
    {
        var folder = TempDir();
        var limit = 500 * NoteImageThreshold.BytesPerKilobyte;
        WriteSized(Path.Combine(folder, "exact.jpg"), limit);
        WriteSized(Path.Combine(folder, "over.jpg"), limit + NoteImageThreshold.BytesPerKilobyte);
        WriteBody(folder, "# 一篇\n");

        var report = NoteImageAudit.Inspect(folder, new NoteImageLimits
        {
            ImageMaxBytes = limit,
            GifMaxBytes = NoteImageThreshold.DefaultGifMaxBytes
        });

        Assert.Equal("超出上限 1 KB（上限 500 KB）", NoteImagePrompt.WarningFor(report, "over.jpg"));
        Assert.Equal("", NoteImagePrompt.WarningFor(report, "exact.jpg"));
        Assert.Equal(NoteImagePrompt.OverLimitBadge, NoteImagePrompt.LimitBadgeFor(report, "心得（notes）/20261005/over.jpg", "20261005"));
        Assert.Equal("", NoteImagePrompt.LimitBadgeFor(report, "心得（notes）/20261005/exact.jpg", "20261005"));
        Assert.Equal("本篇有 1 张配图超限", NoteImagePrompt.FormatFooter(report));
        Assert.Equal("", NoteImagePrompt.PublishBlockReason(report));
    }

    [Fact]
    public void HasOriginalBackup_FollowsTheImageRelativePath()
    {
        var folder = TempDir();
        WriteSized(Path.Combine(folder, NoteRules.OriginalsDirName, "图", "a.png"), 1);

        Assert.True(NoteImagePrompt.HasOriginalBackup(folder, "图/a.png"));
        Assert.False(NoteImagePrompt.HasOriginalBackup(folder, "图/b.png"));
        Assert.False(NoteImagePrompt.HasOriginalBackup(folder, ""));
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-prompt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteBody(string folder, string markdown)
    {
        File.WriteAllText(Path.Combine(folder, NoteRules.BodyFileName), markdown);
    }

    private static void WriteSized(string path, long bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.SetLength(bytes);
    }
}
