using ImageMagick;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 第 10 步的压缩、恢复和清理编排。窗口只负责把这些结果摆出来。
/// </summary>
public sealed class NoteCompressBatchTests
{
    [Fact]
    public void ForImage_KeepsSettingsAndAppliesThisJobsQuality()
    {
        var settings = new AppSettings
        {
            StripGps = false,
            KeepCaptureParams = false,
            PngquantPath = "pngquant-path",
            OxipngPath = "oxipng-path",
            GifsiclePath = "gifsicle-path"
        };
        var limits = new NoteImageLimits
        {
            ImageMaxBytes = 300 * NoteImageThreshold.BytesPerKilobyte,
            GifMaxBytes = 2 * NoteImageThreshold.BytesPerKilobyte * 1024
        };
        var called = false;

        var options = NoteCompressOptions.FromSettings(settings, limits).ForImage(
            NoteImageGuard.PublishedBlockText,
            _ => called = true,
            95,
            startFromOriginalBackup: true);

        Assert.False(options.StripGps);
        Assert.False(options.KeepCaptureParams);
        Assert.Equal("pngquant-path", options.PngquantPath);
        Assert.Equal("oxipng-path", options.OxipngPath);
        Assert.Equal("gifsicle-path", options.GifsiclePath);
        Assert.Equal(limits.ImageMaxBytes, options.Limits.ImageMaxBytes);
        Assert.Equal(NoteImageGuard.PublishedBlockText, options.PublishedReason);
        Assert.Equal(95, options.ManualQuality);
        Assert.True(options.StartFromOriginalBackup);
        options.OnProgress?.Invoke(new NoteCompressProgress());
        Assert.True(called);
    }

    [Fact]
    public void FormatDetail_ListsSizeQualityShrinkAndTheReason()
    {
        var stillOver = new NoteCompressResult
        {
            Status = NoteCompressStatus.StillOverLimit,
            FileName = "a.jpg",
            RelativePath = "图/a.jpg",
            SourceByteCount = 800 * 1024,
            ResultByteCount = 600 * 1024,
            FinalQualityParameter = "质量 70",
            DidShrinkDimensions = true,
            Message = "压缩后仍超限：a.jpg（600 KB），请自行决定换图或保留"
        };
        var published = new NoteCompressResult
        {
            Status = NoteCompressStatus.SkippedPublished,
            FileName = "b.png",
            RelativePath = "b.png",
            SourceByteCount = 100 * 1024,
            ResultByteCount = 100 * 1024,
            Message = "b.png 已发布，已跳过"
        };
        var locked = new NoteCompressResult
        {
            Status = NoteCompressStatus.SkippedLocked,
            FileName = "c.gif",
            RelativePath = "c.gif",
            SourceByteCount = 20 * 1024,
            ResultByteCount = 20 * 1024,
            Message = "c.gif 正被 照片 占用，已跳过。请关闭该程序后点击「重试」"
        };

        var stillText = NoteCompressBatch.FormatDetail(stillOver);
        Assert.Contains("原大小 800 KB，新大小 600 KB，最终质量参数：质量 70，已缩小尺寸。", stillText, StringComparison.Ordinal);
        Assert.Contains("压缩后仍超限：a.jpg（600 KB），请自行决定换图或保留", stillText, StringComparison.Ordinal);

        var publishedText = NoteCompressBatch.FormatDetail(published);
        Assert.Contains("最终质量参数：无，未缩小尺寸。", publishedText, StringComparison.Ordinal);
        Assert.Contains("b.png 已发布，已跳过", publishedText, StringComparison.Ordinal);

        Assert.Contains("请关闭该程序后点击「重试」", NoteCompressBatch.FormatDetail(locked), StringComparison.Ordinal);

        var batch = NoteCompressBatch.FromFileList(new[] { stillOver, published, locked });
        Assert.Equal(new[] { "c.gif" }, batch.RetryRelativePathList);
        Assert.Contains("压缩后仍超限 1", batch.Summary, StringComparison.Ordinal);
        Assert.Contains("已发布跳过 1", batch.Summary, StringComparison.Ordinal);
        Assert.Contains("占用跳过 1", batch.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Compress_SkipsPublishedBytes_AndReportsQuality_AndWritesLog()
    {
        var note = TempDir();
        File.WriteAllText(Path.Combine(note, NoteRules.BodyFileName), "# 一篇\n");
        var publishedPath = Path.Combine(note, "locked-out.jpg");
        var originalPublished = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(publishedPath, originalPublished);
        var livePath = Path.Combine(note, "live.jpg");
        var source = MakeNoiseJpeg();
        File.WriteAllBytes(livePath, source);

        var progressList = new List<PublishProgress>();
        var options = new NoteCompressOptions
        {
            Limits = new NoteImageLimits
            {
                ImageMaxBytes = 50 * 1024 * 1024,
                GifMaxBytes = 50 * 1024 * 1024
            },
            ToolPathEnvironment = "",
            ManualQuality = 40,
            StripGps = false,
            KeepCaptureParams = true
        };
        var batch = NoteCompressBatch.Compress(
            note,
            new[]
            {
                new NoteCompressTarget
                {
                    RelativePath = "locked-out.jpg",
                    PublishedReason = NoteImageGuard.PublishedBlockText
                },
                new NoteCompressTarget { RelativePath = "live.jpg" }
            },
            options,
            new ListProgress(progressList));

        Assert.Equal(originalPublished, File.ReadAllBytes(publishedPath));
        Assert.Equal(NoteCompressStatus.SkippedPublished, batch.FileList[0].Status);
        Assert.Equal("locked-out.jpg 已发布，已跳过", batch.FileList[0].Message);
        Assert.Contains("正在尝试：质量 40", progressList.Select(item => item.Detail), StringComparer.Ordinal);
        Assert.Contains(progressList, item => item.CurrentStep == 2 && item.TotalStep == 2 && item.Title == "live.jpg");
        Assert.True(batch.FileList[1].Status is NoteCompressStatus.Succeeded or NoteCompressStatus.StillOverLimit or NoteCompressStatus.AbandonedLarger);
        if (batch.FileList[1].Status == NoteCompressStatus.Succeeded)
        {
            Assert.Contains("质量 40", batch.FileList[1].FinalQualityParameter, StringComparison.Ordinal);
        }

        var logPath = Path.Combine(note, "run.log");
        using (RunLog.UsePath(logPath))
        {
            NoteCompressBatch.WriteLog(batch);
        }

        var log = File.ReadAllText(logPath);
        Assert.Contains("locked-out.jpg 已发布，已跳过", log, StringComparison.Ordinal);
        Assert.Contains("压缩配图", log, StringComparison.Ordinal);
    }

    [Fact]
    public void Compress_SecondPassStartsFromTheOriginalBackup()
    {
        var note = TempDir();
        var image = Path.Combine(note, "again.jpg");
        File.WriteAllBytes(image, MakeNoiseJpeg());
        var limits = new NoteImageLimits
        {
            ImageMaxBytes = 50 * 1024 * 1024,
            GifMaxBytes = 50 * 1024 * 1024
        };
        var firstOptions = new NoteCompressOptions
        {
            Limits = limits,
            ToolPathEnvironment = "",
            ManualQuality = 40
        };
        var first = NoteCompressBatch.Compress(
            note,
            new[] { new NoteCompressTarget { RelativePath = "again.jpg" } },
            firstOptions);
        Assert.Equal(NoteCompressStatus.Succeeded, first.FileList[0].Status);
        var backup = File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image));

        var second = NoteCompressBatch.Compress(
            note,
            new[] { new NoteCompressTarget { RelativePath = "again.jpg" } },
            firstOptions.ForImage(null, null, 40, startFromOriginalBackup: true));

        Assert.Equal(NoteCompressStatus.Succeeded, second.FileList[0].Status);
        Assert.False(second.FileList[0].WroteOriginalBackup);
        Assert.Contains("原有备份未覆盖", second.FileList[0].Message, StringComparison.Ordinal);
        Assert.Equal(backup, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
    }

    [Fact]
    public void RetryText_NamesTheHolderAndTellsTheUserToRetry()
    {
        var named = NoteImageBackupResult.Locked("图/a.png", new[] { "照片" });
        var unnamed = NoteImageBackupResult.Locked("b.jpg", Array.Empty<string>());

        Assert.Equal("a.png 正被 照片 占用，已跳过。请关闭该程序后点击「重试」", NoteBackupJob.RetryText(named));
        Assert.Equal("b.jpg 正被其他程序占用，已跳过。请关闭该程序后点击「重试」", NoteBackupJob.RetryText(unnamed));
        Assert.Equal(
            "将把 3 张原图（共 500 KB）送进回收站，清理后只能从回收站找回。确定继续吗？",
            NoteBackupJob.FormatPurgeConfirm(new NoteImageBackupMeasure(3, 500 * 1024)));
    }

    [Fact]
    public void Restore_SkipsPublishedAndPutsTheBackupBack()
    {
        var note = TempDir();
        var image = Path.Combine(note, "图", "a.png");
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        var original = new byte[] { 9, 8, 7, 6, 5 };
        var compressed = new byte[] { 1, 1, 1 };
        File.WriteAllBytes(image, original);
        var replaced = NoteImageBackup.Replace(note, image, compressed);
        Assert.Equal(NoteImageBackupKind.Replaced, replaced.Kind);

        var published = Path.Combine(note, "keep.png");
        File.WriteAllBytes(published, new byte[] { 4, 4 });
        var report = NoteBackupJob.Restore(
            note,
            new[]
            {
                new NoteImageRestoreTarget
                {
                    RelativePath = "keep.png",
                    PublishedReason = NoteImageGuard.PublishedBlockText
                },
                new NoteImageRestoreTarget { RelativePath = "图/a.png" }
            });

        Assert.Equal(new byte[] { 4, 4 }, File.ReadAllBytes(published));
        Assert.Equal("keep.png 已发布，已跳过", report.FileList[0].Message);
        Assert.Equal(original, File.ReadAllBytes(image));
        Assert.False(File.Exists(NoteImageBackup.BackupFilePath(note, image)));
        Assert.Contains("已恢复 1", report.Summary, StringComparison.Ordinal);
        Assert.Contains("已发布跳过 1", report.Summary, StringComparison.Ordinal);
        Assert.False(report.CanRetry);
    }

    [Fact]
    public void Purge_ConfirmsChannelScopeAndRecyclesOnlyOriginals()
    {
        var channel = TempDir();
        var first = Path.Combine(channel, "20261017_010101");
        var second = Path.Combine(channel, "20261017_020202");
        var ignored = Path.Combine(channel, ".drafts");
        var noBody = Path.Combine(channel, "loose");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        Directory.CreateDirectory(ignored);
        Directory.CreateDirectory(noBody);
        File.WriteAllText(Path.Combine(first, NoteRules.BodyFileName), "# 一\n");
        File.WriteAllText(Path.Combine(second, NoteRules.BodyFileName), "# 二\n");
        Directory.CreateDirectory(Path.Combine(ignored, NoteRules.OriginalsDirName));
        File.WriteAllBytes(Path.Combine(ignored, NoteRules.BodyFileName), new byte[] { 1 });
        var picture = Path.Combine(first, "a.png");
        File.WriteAllBytes(picture, new byte[] { 1, 2, 3, 4, 5 });
        Assert.Equal(NoteImageBackupKind.Replaced, NoteImageBackup.Replace(first, picture, new byte[] { 9 }).Kind);
        File.WriteAllBytes(Path.Combine(second, "b.png"), new byte[] { 7, 7 });
        Assert.Equal(NoteImageBackupKind.Replaced, NoteImageBackup.Replace(second, Path.Combine(second, "b.png"), new byte[] { 8 }).Kind);
        File.WriteAllBytes(Path.Combine(ignored, NoteRules.OriginalsDirName, "secret.png"), new byte[] { 3 });

        var folders = NoteBackupJob.ListNoteFolders(channel);
        Assert.Equal(2, folders.Count);
        Assert.Equal(2, NoteBackupJob.MeasureChannel(channel).FileCount);

        var recycled = new List<string>();
        var report = NoteBackupJob.PurgeChannel(channel, sendToRecycle: path =>
        {
            recycled.Add(path);
            File.Delete(path);
        });

        Assert.Equal(2, recycled.Count);
        Assert.Equal(2, report.FileList.Count);
        Assert.All(report.FileList, item => Assert.True(item.Ok));
        Assert.Contains("已送进回收站 2", report.Summary, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(first, NoteRules.OriginalsDirName)));
        Assert.True(File.Exists(Path.Combine(ignored, NoteRules.OriginalsDirName, "secret.png")));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(picture));

        var logPath = Path.Combine(channel, "run.log");
        using (RunLog.UsePath(logPath))
        {
            NoteBackupJob.WriteLog("清理原图备份", report);
        }

        Assert.Contains("清理原图备份", File.ReadAllText(logPath), StringComparison.Ordinal);
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] MakeNoiseJpeg()
    {
        using var image = new MagickImage(MagickColors.White, 96, 64);
        image.AddNoise(NoiseType.Gaussian, 1.5);
        image.Quality = 95;
        image.Settings.SetDefine(MagickFormat.Jpeg, "optimize-coding", "true");
        return image.ToByteArray(MagickFormat.Jpeg);
    }

    private sealed class ListProgress : IProgress<PublishProgress>
    {
        private readonly List<PublishProgress> mList;

        public ListProgress(List<PublishProgress> list)
        {
            mList = list;
        }

        public void Report(PublishProgress value)
        {
            mList.Add(value);
        }
    }
}
