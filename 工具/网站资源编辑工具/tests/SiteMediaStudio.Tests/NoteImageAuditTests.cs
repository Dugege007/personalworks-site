using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteImageAuditTests
{
    [Fact]
    public void Inspect_FlagsImagesStrictlyLargerThanTheLimit()
    {
        var folder = CreateNoteFolder();
        var limit = NoteImageThreshold.DefaultImageMaxBytes;
        WriteSized(Path.Combine(folder, "exact.jpg"), limit);
        WriteSized(Path.Combine(folder, "over.png"), limit + 1);
        WriteSized(Path.Combine(folder, "under.webp"), limit - 1);
        WriteSized(Path.Combine(folder, "note.bmp"), limit + 1);
        WriteBody(folder, "# 一篇\n");

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        var over = Assert.Single(report.OverLimitList);
        Assert.Equal("over.png", over.RelativePath);
        Assert.Equal(limit + 1, over.Bytes);
        Assert.Equal(limit, over.MaxBytes);
        Assert.Equal(Path.GetFullPath(Path.Combine(folder, "over.png")), Path.GetFullPath(over.FullPath));
        Assert.Empty(report.MissingList);
        Assert.Equal(1, report.OverLimitCount);
    }

    [Fact]
    public void Inspect_GifUsesItsOwnLimit()
    {
        var folder = CreateNoteFolder();
        var imageLimit = NoteImageThreshold.DefaultImageMaxBytes;
        var gifLimit = NoteImageThreshold.DefaultGifMaxBytes;
        WriteSized(Path.Combine(folder, "still.png"), imageLimit + 1);
        WriteSized(Path.Combine(folder, "small.gif"), imageLimit + 1);
        WriteSized(Path.Combine(folder, "edge.gif"), gifLimit);
        WriteSized(Path.Combine(folder, "big.gif"), gifLimit + 1);
        WriteBody(folder, "# 一篇\n");

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal(new[] { "big.gif", "still.png" }, report.OverLimitList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(gifLimit, report.OverLimitList[0].MaxBytes);
        Assert.Equal(gifLimit + 1, report.OverLimitList[0].Bytes);
        Assert.Equal(imageLimit, report.OverLimitList[1].MaxBytes);
        Assert.Equal(imageLimit + 1, report.OverLimitList[1].Bytes);
    }

    [Fact]
    public void Inspect_ListsEveryMissingImage()
    {
        var folder = CreateNoteFolder();
        WriteSized(Path.Combine(folder, "kept.png"), 1);
        WriteBody(folder, """
            # 一篇

            ![一](gone-a.png)
            ![二](图/gone-b.jpg)
            ![动](gone.gif)
            ![有](kept.png)
            ![网](https://example.com/remote.png)
            ![再](gone-a.png)
            <img src="gone-c.webp" alt="三">
            """);

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal(
            new[] { "gone-a.png", "图/gone-b.jpg", "gone.gif", "gone-c.webp" },
            report.MissingList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(
            new[] { "gone-a.png", "图/gone-b.jpg", "gone.gif", "gone-c.webp" },
            report.MissingList.Select(item => item.Reference).ToArray());
        Assert.Equal(4, report.MissingCount);
        Assert.Empty(report.OverLimitList);
    }

    [Fact]
    public void Inspect_IncludesImagesInSubfolders()
    {
        var folder = CreateNoteFolder();
        var limit = NoteImageThreshold.DefaultImageMaxBytes;
        WriteSized(Path.Combine(folder, "图", "big.png"), limit + 1);
        WriteSized(Path.Combine(folder, "图", "exact.jpg"), limit);
        WriteSized(Path.Combine(folder, "root.png"), limit + 1);
        WriteBody(folder, "# 一篇\n\n![缺](图/gone.jpg)\n");

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal(new[] { "root.png", "图/big.png" }, report.OverLimitList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(new[] { "图/gone.jpg" }, report.MissingList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(limit + 1, report.OverLimitList.Single(item => item.RelativePath == "图/big.png").Bytes);
    }

    [Fact]
    public void Inspect_SkipsOriginalsAndSiteReady()
    {
        var folder = CreateNoteFolder();
        var limit = NoteImageThreshold.DefaultImageMaxBytes;
        WriteSized(Path.Combine(folder, NoteRules.OriginalsDirName, "huge.png"), limit + 1);
        WriteSized(Path.Combine(folder, NoteRules.OriginalsDirName, "图", "huge.jpg"), limit + 1);
        WriteSized(Path.Combine(folder, ".Originals", "legacy.png"), limit + 1);
        WriteSized(Path.Combine(folder, NoteRules.ReadyDirName, "huge.webp"), limit + 1);
        WriteSized(Path.Combine(folder, ".SITE-READY", "legacy.webp"), limit + 1);
        WriteSized(Path.Combine(folder, "visible.png"), limit + 1);
        WriteSized(Path.Combine(folder, "not.originals.png"), limit + 1);
        WriteBody(folder, """
            # 一篇

            ![备](.originals/missing.png)
            ![成](.site-ready/missing.webp)
            ![旧](.Originals/legacy.png)
            ![缺](gone.png)
            """);

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal(
            new[] { "not.originals.png", "visible.png" },
            report.OverLimitList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(new[] { "gone.png" }, report.MissingList.Select(item => item.RelativePath).ToArray());
    }

    [Fact]
    public void Inspect_TreatsUppercaseExtensionsAsImages()
    {
        var folder = CreateNoteFolder();
        var imageLimit = NoteImageThreshold.DefaultImageMaxBytes;
        var gifLimit = NoteImageThreshold.DefaultGifMaxBytes;
        WriteSized(Path.Combine(folder, "Photo.JPG"), imageLimit + 1);
        WriteSized(Path.Combine(folder, "Edge.JPEG"), imageLimit);
        WriteSized(Path.Combine(folder, "Small.GIF"), imageLimit + 1);
        WriteSized(Path.Combine(folder, "Large.GIF"), gifLimit + 1);
        WriteBody(folder, "# 一篇\n\n![缺](Missing.PNG)\n![有](Photo.JPG)\n");

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal(new[] { "Large.GIF", "Photo.JPG" }, report.OverLimitList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(gifLimit, report.OverLimitList.Single(item => item.RelativePath == "Large.GIF").MaxBytes);
        Assert.Equal(imageLimit, report.OverLimitList.Single(item => item.RelativePath == "Photo.JPG").MaxBytes);
        Assert.Equal(new[] { "Missing.PNG" }, report.MissingList.Select(item => item.RelativePath).ToArray());
    }

    [Fact]
    public void Inspect_ListsReferencesOutsideTheNoteFolder()
    {
        var folder = CreateNoteFolder();
        var sibling = Path.Combine(Path.GetDirectoryName(folder)!, "other");
        Directory.CreateDirectory(sibling);
        WriteSized(Path.Combine(sibling, "a.png"), 1);
        WriteSized(Path.Combine(folder, "kept.png"), 1);
        WriteBody(folder, """
            # 一篇

            ![外](../other/a.png)
            ![根](/other/a.png)
            ![折](图/../gone.png)
            ![有](kept.png)
            """);

        var report = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal(new[] { "../other/a.png", "/other/a.png" }, report.OutsideList.Select(item => item.Reference).ToArray());
        Assert.Equal(new[] { "gone.png" }, report.MissingList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(2, report.OutsideCount);
        Assert.True(report.BlocksPublish);
        Assert.Equal("图/../gone.png", Assert.Single(report.MissingList).Reference);
    }

    [Fact]
    public void Inspect_KeepsIncompletePercentEncodingAsALiteralName()
    {
        var folder = CreateNoteFolder();
        WriteSized(Path.Combine(folder, "a%2.png"), 1);
        WriteBody(folder, """
            # 一篇

            ![坏](a%2.png)
            ![缺](gone.png)
            ![再坏](b%2.png)
            """);

        var present = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());

        Assert.Equal(new[] { "gone.png", "b%2.png" }, present.MissingList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(new[] { "gone.png", "b%2.png" }, present.MissingList.Select(item => item.Reference).ToArray());
        Assert.DoesNotContain(present.MissingList, item => item.Reference == "a%2.png");
        Assert.Empty(present.OutsideList);

        File.Delete(Path.Combine(folder, "a%2.png"));
        var absent = NoteImageAudit.Inspect(folder, NoteImageLimits.CreateDefault());
        Assert.Equal(
            new[] { "a%2.png", "gone.png", "b%2.png" },
            absent.MissingList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(3, absent.MissingCount);
    }

    [Fact]
    public void Inspect_ReadsThresholdsFromSharedLimitsFile()
    {
        var directory = TempDir();
        var folder = Path.Combine(directory, "20261005_125603");
        Directory.CreateDirectory(folder);
        var profilePath = Path.Combine(directory, "personalworks.json");
        File.WriteAllText(profilePath, "{}");
        NoteImageLimitsStore.SaveFile(Path.Combine(directory, NoteImageLimitsStore.FileName), new NoteImageLimits
        {
            ImageMaxBytes = 1024,
            GifMaxBytes = 2048
        });
        WriteSized(Path.Combine(folder, "exact.png"), 1024);
        WriteSized(Path.Combine(folder, "over.png"), 1025);
        WriteSized(Path.Combine(folder, "exact.gif"), 2048);
        WriteSized(Path.Combine(folder, "over.GIF"), 2049);
        WriteBody(folder, "# 一篇\n");

        var report = NoteImageAudit.Inspect(folder, new WorkspaceProfile { ProfilePath = profilePath });

        Assert.Equal(new[] { "over.GIF", "over.png" }, report.OverLimitList.Select(item => item.RelativePath).ToArray());
        Assert.Equal(2048, report.OverLimitList[0].MaxBytes);
        Assert.Equal(2049, report.OverLimitList[0].Bytes);
        Assert.Equal(1024, report.OverLimitList[1].MaxBytes);
        Assert.Equal(1025, report.OverLimitList[1].Bytes);
    }

    [Fact]
    public void Inspect_WhenSharedLimitsAreCorrupt_Throws()
    {
        var directory = TempDir();
        var folder = Path.Combine(directory, "20261005_125603");
        Directory.CreateDirectory(folder);
        var profilePath = Path.Combine(directory, "personalworks.json");
        File.WriteAllText(profilePath, "{}");
        File.WriteAllText(Path.Combine(directory, NoteImageLimitsStore.FileName), "<<<<<<< HEAD");
        WriteSized(Path.Combine(folder, "over.png"), NoteImageThreshold.DefaultImageMaxBytes + 1);
        WriteBody(folder, "# 一篇\n");

        var error = Assert.Throws<NoteImageLimitsException>(() =>
            NoteImageAudit.Inspect(folder, new WorkspaceProfile { ProfilePath = profilePath }));

        Assert.Contains(NoteImageLimitsException.DisplayText, error.Message, StringComparison.Ordinal);
    }

    private static string CreateNoteFolder()
    {
        var path = TempDir();
        return path;
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-audit-" + Guid.NewGuid().ToString("N"));
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
