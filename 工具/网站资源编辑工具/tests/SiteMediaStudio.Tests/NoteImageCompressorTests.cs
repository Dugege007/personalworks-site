using ImageMagick;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 心得配图压缩。图片都在测试里生成，不提交成品大图。
/// 创建时间能否和修改时间分开保存，取决于文件系统；分得开时才断言创建时间不变。
/// 外部程序默认不搜索 PATH，避免本机装了 pngquant 时改变结果。
/// </summary>
public sealed class NoteImageCompressorTests
{
    private static bool? mCreationTimeStaysDistinct;

    [Fact]
    public void Choose_StopsAtTheFirstAttemptThatFits()
    {
        var chosen = NoteCompressSelection.TryChoose(1000, 450, new long[] { 400, 300 }, out var index, out var stillOverLimit);

        Assert.True(chosen);
        Assert.Equal(0, index);
        Assert.False(stillOverLimit);
    }

    [Fact]
    public void Choose_SkipsAttemptsThatAreNotSmaller_AndAcceptsAnEqualThreshold()
    {
        var chosen = NoteCompressSelection.TryChoose(900, 600, new long[] { 1000, 800, 600 }, out var index, out var stillOverLimit);

        Assert.True(chosen);
        Assert.Equal(2, index);
        Assert.False(stillOverLimit);
    }

    [Fact]
    public void Choose_KeepsTheSmallestAttempt_WhenEverySmallerAttemptIsStillOverLimit()
    {
        var chosen = NoteCompressSelection.TryChoose(1000, 100, new long[] { 900, 850, 700 }, out var index, out var stillOverLimit);

        Assert.True(chosen);
        Assert.Equal(2, index);
        Assert.True(stillOverLimit);
    }

    [Fact]
    public void Choose_Abandons_WhenNoAttemptIsSmallerThanTheOriginal()
    {
        var chosen = NoteCompressSelection.TryChoose(900, 100, new long[] { 1000, 950, 900 }, out var index, out var stillOverLimit);

        Assert.False(chosen);
        Assert.Equal(-1, index);
        Assert.False(stillOverLimit);
    }

    [Fact]
    public void FromSettings_CopiesPhotoSwitchesAndToolPaths()
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
            ImageMaxBytes = 123 * NoteImageThreshold.BytesPerKilobyte,
            GifMaxBytes = 456 * NoteImageThreshold.BytesPerKilobyte
        };

        var options = NoteCompressOptions.FromSettings(settings, limits);

        Assert.False(options.StripGps);
        Assert.False(options.KeepCaptureParams);
        Assert.Equal("pngquant-path", options.PngquantPath);
        Assert.Equal("oxipng-path", options.OxipngPath);
        Assert.Equal("gifsicle-path", options.GifsiclePath);
        Assert.Equal(limits.ImageMaxBytes, options.Limits.ImageMaxBytes);
        Assert.Equal(limits.GifMaxBytes, options.Limits.GifMaxBytes);
    }

    [Fact]
    public void Jpeg_StopsAtQuality85_WhenThatLevelFitsUnderTheThreshold()
    {
        var original = MakeNoise(640, 480, MagickFormat.Jpeg, 100);
        var lossless = NoteImageEncoder.EncodeJpeg(original, null, false, true, true);
        var quality92 = NoteImageEncoder.EncodeJpeg(original, 92, false, true, true);
        var quality88 = NoteImageEncoder.EncodeJpeg(original, 88, false, true, true);
        var quality85 = NoteImageEncoder.EncodeJpeg(original, 85, false, true, true);
        Assert.NotNull(quality92);
        Assert.NotNull(quality88);
        Assert.NotNull(quality85);
        Assert.True(quality88!.Content.Length > quality85!.Content.Length);
        Assert.True(quality92!.Content.Length > quality85.Content.Length);
        Assert.True(lossless == null || lossless.Content.Length > quality85.Content.Length);
        Assert.True(quality85.Content.Length < original.Length);

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, Path.Combine("图", "a.jpg"), original);
        var details = new List<string>();
        var result = NoteImageCompressor.Compress(
            note,
            image,
            Options(quality85.Content.Length, onProgress: progress => details.Add(progress.Detail)));

        Assert.Equal(NoteCompressStatus.Succeeded, result.Status);
        Assert.Equal("图/a.jpg", result.RelativePath);
        Assert.Equal("质量 85", result.FinalQualityParameter);
        Assert.False(result.DidShrinkDimensions);
        Assert.Equal(quality85.Content.Length, result.ResultByteCount);
        Assert.Equal(original.Length, result.SourceByteCount);
        Assert.True(result.WroteOriginalBackup);
        Assert.Equal("", result.FailureReason);
        Assert.Contains("已压到上限以下", result.Message, StringComparison.Ordinal);
        Assert.Equal(quality85.Content, File.ReadAllBytes(image));
        Assert.Equal(original, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
        Assert.Contains("正在尝试：质量 85", details);
        var joined = string.Join("\n", details);
        Assert.DoesNotContain("质量 80", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("质量 75", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("质量 70", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("长边", joined, StringComparison.Ordinal);
        AssertNoLeftover(note);
    }

    [Fact]
    public void Jpeg_ShrinksLongEdgeTo2560_WhenQuality70StillExceedsTheThreshold()
    {
        var original = MakeNoise(6000, 4000, MagickFormat.Jpeg, 100);
        var quality70 = NoteImageEncoder.EncodeJpeg(original, 70, false, true, true);
        var resized = NoteImageEncoder.EncodeJpeg(original, 92, true, true, true);
        Assert.NotNull(quality70);
        Assert.NotNull(resized);
        Assert.True(resized!.DidShrinkDimensions);
        Assert.Equal(NoteImageCompressor.LongEdgePixelLimit, resized.Width);
        Assert.True(quality70!.Content.Length > resized.Content.Length);
        Assert.True(resized.Content.Length < original.Length);
        var threshold = quality70.Content.Length - 1;
        Assert.True(resized.Content.Length <= threshold);

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "wide.jpg", original);

        var result = NoteImageCompressor.Compress(note, image, Options(threshold));

        Assert.Equal(NoteCompressStatus.Succeeded, result.Status);
        Assert.Equal("质量 92，长边 2560", result.FinalQualityParameter);
        Assert.True(result.DidShrinkDimensions);
        Assert.Equal(NoteImageCompressor.LongEdgePixelLimit, result.ResultWidth);
        var expectedHeight = (int)Math.Round(4000.0 * NoteImageCompressor.LongEdgePixelLimit / 6000.0);
        Assert.InRange(result.ResultHeight, expectedHeight - 1, expectedHeight + 1);
        Assert.Equal(resized.Content, File.ReadAllBytes(image));
        Assert.Equal(original, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
        Assert.True(result.WroteOriginalBackup);
    }

    [Fact]
    public void Png_CompressesBelowTheThreshold_WhenExternalToolsAreMissing()
    {
        var original = MakeNoise(420, 300, MagickFormat.Png, 0);
        var threshold = Math.Max(NoteImageThreshold.BytesPerKilobyte, original.Length / 2);

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", original);

        var result = NoteImageCompressor.Compress(note, image, Options(threshold));

        Assert.Equal(NoteCompressStatus.Succeeded, result.Status);
        Assert.True(result.ResultByteCount <= threshold);
        Assert.True(result.ResultByteCount < original.Length);
        Assert.Contains("没有找到 pngquant", result.ExternalToolNote, StringComparison.Ordinal);
        Assert.Contains("没有找到 oxipng", result.ExternalToolNote, StringComparison.Ordinal);
        Assert.Contains("内建", result.ExternalToolNote, StringComparison.Ordinal);
        using var written = new MagickImage(File.ReadAllBytes(image));
        Assert.Equal(MagickFormat.Png, written.Format);
        Assert.Equal(original, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
        AssertNoLeftover(note);
    }

    [Fact]
    public void Webp_CompressesBelowTheThreshold()
    {
        var original = MakeNoise(480, 320, MagickFormat.WebP, 95);
        Assert.True(original.Length > NoteImageThreshold.BytesPerKilobyte);

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.webp", original);

        var result = NoteImageCompressor.Compress(note, image, Options(original.Length - 1));

        Assert.Equal(NoteCompressStatus.Succeeded, result.Status);
        Assert.True(result.ResultByteCount < original.Length);
        Assert.True(result.ResultByteCount <= original.Length - 1);
        using var written = new MagickImage(File.ReadAllBytes(image));
        Assert.Equal(MagickFormat.WebP, written.Format);
        Assert.True(result.WroteOriginalBackup);
    }

    [Fact]
    public void Gif_CompressesBelowTheThreshold_AndKeepsPlaybackDuration()
    {
        var delays = new uint[] { 10, 15, 20, 25, 12, 18 };
        var original = MakeNoisyGif(delays, 96, 72);
        var inputDuration = SumDelays(original);
        var threshold = Math.Max(NoteImageThreshold.BytesPerKilobyte, original.Length / 2);

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "anim.gif", original);

        var result = NoteImageCompressor.Compress(note, image, Options(threshold, gifMaxBytes: threshold));

        Assert.Equal(NoteCompressStatus.Succeeded, result.Status);
        Assert.True(result.ResultByteCount <= threshold);
        Assert.Contains("没有找到 gifsicle", result.ExternalToolNote, StringComparison.Ordinal);
        var outputDuration = SumDelays(File.ReadAllBytes(image));
        Assert.True(Math.Abs(outputDuration - inputDuration) <= delays.Max(), "播放时长变成了 " + outputDuration + "，原来是 " + inputDuration);
        using var written = new MagickImageCollection();
        written.Read(image);
        Assert.True(written.Count >= 1);
    }

    [Fact]
    public void Gif_DroppingEveryOtherFrame_KeepsTheTotalDelay()
    {
        var once = NoteGifFramePolicy.CombineDroppedFrameDelays(new[] { 10, 15, 20, 25 });
        var twice = NoteGifFramePolicy.CombineDroppedFrameDelays(once);
        var odd = NoteGifFramePolicy.CombineDroppedFrameDelays(new[] { 10, 15, 20 });

        Assert.Equal(new[] { 25, 45 }, once);
        Assert.Equal(70, once.Sum());
        Assert.Equal(new[] { 70 }, twice);
        Assert.Equal(new[] { 25, 20 }, odd);
        Assert.Equal(45, odd.Sum());

        var gif = MakeSolidGif(new uint[] { 10, 15, 20, 25 });
        var encoded = NoteImageEncoder.EncodeGif(
            gif,
            "64 色，并隔一帧丢弃",
            colorCount: 64,
            dropRounds: 1,
            scalePercent: 100,
            optimizeOnly: false,
            stripGps: true,
            keepCaptureParams: true,
            gifsiclePath: null);

        Assert.NotNull(encoded);
        var difference = Math.Abs(SumDelays(encoded!.Content) - SumDelays(gif));
        Assert.True(difference <= 25, "播放时长相差 " + difference);
    }

    [Fact]
    public void ManualQuality_WhenTheResultIsLarger_KeepsTheOriginal()
    {
        var original = MakeNoise(160, 120, MagickFormat.Jpeg, 30);
        var larger = NoteImageEncoder.EncodeJpeg(original, 95, false, true, true);
        Assert.NotNull(larger);
        Assert.True(larger!.Content.Length > original.Length);

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.jpg", original);

        var result = NoteImageCompressor.Compress(note, image, Options(50L * 1024 * 1024, manualQuality: 95));

        Assert.Equal(NoteCompressStatus.AbandonedLarger, result.Status);
        Assert.Equal("a.jpg 压缩后比原图大，已保留原图", result.Message);
        Assert.Equal(result.Message, result.FailureReason);
        Assert.False(result.WroteOriginalBackup);
        Assert.Equal(original, File.ReadAllBytes(image));
        Assert.False(Directory.Exists(Path.Combine(note, NoteRules.OriginalsDirName)));
    }

    [Fact]
    public void Compress_WhenStillOverTheLimit_WritesTheSmallerFileAndKeepsTheBackup()
    {
        var original = MakeNoise(200, 140, MagickFormat.Jpeg, 95);
        const long threshold = 1024;

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "tight.jpg", original);

        var result = NoteImageCompressor.Compress(note, image, Options(threshold));

        Assert.Equal(NoteCompressStatus.StillOverLimit, result.Status);
        Assert.True(result.ResultByteCount > threshold);
        Assert.True(result.ResultByteCount < original.Length);
        Assert.True(result.WroteOriginalBackup);
        Assert.Equal(original, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
        var expected = "压缩后仍超限：tight.jpg（" + FileSizeFormat.Format(result.ResultByteCount) + "），请自行决定换图或保留";
        Assert.Equal(expected, result.Message);
        Assert.Equal(expected, result.FailureReason);
        Assert.NotEqual(NoteCompressStatus.Succeeded, result.Status);
    }

    [Fact]
    public void Compress_StripsGps_KeepsCaptureMetadata_AndPreservesCreationTimeWhenSupported()
    {
        var original = MakePhotoJpeg();
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "shot.jpeg", original);
        StampOldTimes(image);

        var result = NoteImageCompressor.Compress(note, image, Options(50L * 1024 * 1024, manualQuality: 70));

        Assert.Equal(NoteCompressStatus.Succeeded, result.Status);
        Assert.True(result.WroteOriginalBackup);
        Assert.Equal(original, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
        using (var backup = new MagickImage(original))
        {
            Assert.NotNull(backup.GetExifProfile()?.GetValue(ExifTag.GPSLatitude));
        }

        using var written = new MagickImage(File.ReadAllBytes(image));
        var exif = written.GetExifProfile();
        Assert.NotNull(exif);
        Assert.Equal("SampleCamera", exif!.GetValue(ExifTag.Make)?.Value);
        Assert.Equal("SampleModel", exif.GetValue(ExifTag.Model)?.Value);
        Assert.Equal("2019:05:06 07:08:09", exif.GetValue(ExifTag.DateTimeOriginal)?.Value);
        Assert.Equal((ushort)6, exif.GetValue(ExifTag.Orientation)?.Value);
        Assert.Equal(Orientation.RightTop, written.Orientation);
        Assert.NotNull(exif.GetValue(ExifTag.FNumber));
        Assert.Null(exif.GetValue(ExifTag.GPSLatitude));
        Assert.Null(exif.GetValue(ExifTag.GPSLongitude));
        Assert.DoesNotContain(exif.Values, value => (value.Tag.ToString() ?? "").StartsWith("GPS", StringComparison.Ordinal));
        AssertWriteTimeIsNew(image);
        AssertCreationTimeWhenSupported(image);
    }

    [Fact]
    public void Compress_WhenCaptureParamsAreDisabled_RemovesCameraMetadata()
    {
        var original = MakePhotoJpeg();
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "shot.jpg", original);

        var result = NoteImageCompressor.Compress(
            note,
            image,
            Options(50L * 1024 * 1024, manualQuality: 70, stripGps: true, keepCaptureParams: false));

        Assert.Equal(NoteCompressStatus.Succeeded, result.Status);
        using var written = new MagickImage(File.ReadAllBytes(image));
        var exif = written.GetExifProfile();
        Assert.True(exif == null || exif.GetValue(ExifTag.Make) == null);
        Assert.True(exif == null || exif.GetValue(ExifTag.DateTimeOriginal) == null);
        Assert.True(exif == null || exif.GetValue(ExifTag.FNumber) == null);
        Assert.True(exif == null || exif.GetValue(ExifTag.GPSLatitude) == null);
        Assert.Equal(Orientation.Undefined, written.Orientation);
    }

    [Fact]
    public void Compress_SecondPass_DoesNotOverwriteTheFirstBackup()
    {
        var original = MakeNoise(180, 120, MagickFormat.Jpeg, 92);
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.jpg", original);

        var first = NoteImageCompressor.Compress(note, image, Options(50L * 1024 * 1024, manualQuality: 60));
        var backup = NoteImageBackup.BackupFilePath(note, image);
        Assert.Equal(NoteCompressStatus.Succeeded, first.Status);
        Assert.True(first.WroteOriginalBackup);
        Assert.Equal(original, File.ReadAllBytes(backup));

        var second = NoteImageCompressor.Compress(note, image, Options(50L * 1024 * 1024, manualQuality: 50));

        Assert.Equal(NoteCompressStatus.Succeeded, second.Status);
        Assert.False(second.WroteOriginalBackup);
        Assert.Contains("未覆盖", second.Message, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(backup));
    }

    [Fact]
    public void Compress_StartFromOriginalBackup_UsesTheBackupPhotoInsteadOfTheCurrentFile()
    {
        var original = MakePhotoJpeg();
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "shot.jpg", original);
        var first = NoteImageCompressor.Compress(note, image, Options(50L * 1024 * 1024, manualQuality: 75));
        Assert.Equal(NoteCompressStatus.Succeeded, first.Status);
        var backup = NoteImageBackup.BackupFilePath(note, image);
        var backupBytes = File.ReadAllBytes(backup);

        File.WriteAllBytes(image, MakePhotoJpegWithDate("2024:01:02 03:04:05", "OtherCamera"));
        var again = NoteImageCompressor.Compress(
            note,
            image,
            Options(50L * 1024 * 1024, manualQuality: 70, startFromBackup: true));

        Assert.Equal(NoteCompressStatus.Succeeded, again.Status);
        Assert.False(again.WroteOriginalBackup);
        Assert.Equal(backupBytes, File.ReadAllBytes(backup));
        using var written = new MagickImage(File.ReadAllBytes(image));
        var exif = written.GetExifProfile();
        Assert.Equal("2019:05:06 07:08:09", exif?.GetValue(ExifTag.DateTimeOriginal)?.Value);
        Assert.Equal("SampleCamera", exif?.GetValue(ExifTag.Make)?.Value);
        Assert.Null(exif?.GetValue(ExifTag.GPSLatitude));
    }

    [Fact]
    public void Compress_WhenPublished_SkipsWithoutChangingTheFile()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);

        var result = NoteImageCompressor.Compress(
            note,
            image,
            Options(50L * 1024 * 1024, publishedReason: NoteImageGuard.PublishedBlockText));

        Assert.Equal(NoteCompressStatus.SkippedPublished, result.Status);
        Assert.Equal("a.png 已发布，已跳过", result.Message);
        Assert.Equal(NoteImageGuard.PublishedBlockText, result.FailureReason);
        Assert.False(result.WroteOriginalBackup);
        Assert.Equal(MinimalPng, File.ReadAllBytes(image));
        Assert.False(Directory.Exists(Path.Combine(note, NoteRules.OriginalsDirName)));
    }

    [Fact]
    public void Compress_WhenTheFileIsLocked_SkipsWithoutABackup()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);
        NoteCompressResult result;
        using (var stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            _ = stream;
            result = NoteImageCompressor.Compress(note, image, Options(50L * 1024 * 1024));
        }

        Assert.Equal(NoteCompressStatus.SkippedLocked, result.Status);
        Assert.Contains("重试", result.Message, StringComparison.Ordinal);
        Assert.Contains("占用", result.Message, StringComparison.Ordinal);
        Assert.False(result.WroteOriginalBackup);
        Assert.Equal(MinimalPng, File.ReadAllBytes(image));
        Assert.False(Directory.Exists(Path.Combine(note, NoteRules.OriginalsDirName)));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Empty(result.HolderNameList);
            Assert.Equal("a.png 正被其他程序占用，已跳过。请关闭该程序后点击「重试」", result.Message);
        }
    }

    [Fact]
    public void Png_WhenPngquantStandInExists_UsesThatOutput()
    {
        if (OperatingSystem.IsWindows())
        {
            // 替身是 shell 脚本。Windows 上不执行这一条，缺失程序时的降级由另一条测试覆盖。
            return;
        }

        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var tiny = MakeTinyPng();
        var tinyPath = Path.Combine(tree.Root, "tiny.png");
        File.WriteAllBytes(tinyPath, tiny);
        var scriptPath = Path.Combine(tree.Root, "pngquant");
        File.WriteAllText(
            scriptPath,
            "#!/bin/sh\n"
            + "if [ \"$1\" = \"--version\" ]; then echo pngquant-stand-in; exit 0; fi\n"
            + "out=\nprev=\n"
            + "for arg in \"$@\"; do\n"
            + "  if [ \"$prev\" = \"--output\" ]; then out=$arg; fi\n"
            + "  prev=$arg\n"
            + "done\n"
            + "cp \"" + tinyPath + "\" \"$out\"\n"
            + "exit 0\n");
        File.SetUnixFileMode(
            scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var original = MakeNoise(360, 240, MagickFormat.Png, 0);
        var image = tree.Write(note, "a.png", original);
        var result = NoteImageCompressor.Compress(note, image, Options(1024, pngquantPath: scriptPath));

        Assert.True(result.Status == NoteCompressStatus.Succeeded, result.Status + "：" + result.Message + " " + result.ExternalToolNote);
        Assert.Contains("pngquant", result.FinalQualityParameter, StringComparison.Ordinal);
        Assert.Equal(tiny, File.ReadAllBytes(image));
        Assert.True(result.WroteOriginalBackup);
        Assert.Equal(original, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
    }

    #region 造图与时间

    /// <summary>
    /// 一张很小的有效 PNG，给外部程序替身当作输出。手写的最小 PNG 校验和不能被图像库读入。
    /// </summary>
    private static byte[] MakeTinyPng()
    {
        using var image = new MagickImage(MagickColors.Red, 8, 8);
        return image.ToByteArray(MagickFormat.Png);
    }

    /// <summary>
    /// 生成一张带噪声的图，避免纯色图小到无法区分质量级。
    /// </summary>
    private static byte[] MakeNoise(int width, int height, MagickFormat format, uint quality)
    {
        using var image = new MagickImage(MagickColors.White, (uint)width, (uint)height);
        image.AddNoise(NoiseType.Gaussian, 1.5);
        image.Quality = quality;
        if (format == MagickFormat.Jpeg)
        {
            image.Settings.SetDefine(MagickFormat.Jpeg, "optimize-coding", "true");
        }

        if (format == MagickFormat.WebP)
        {
            image.Settings.SetDefine(MagickFormat.WebP, "lossless", "false");
            image.Settings.SetDefine(MagickFormat.WebP, "method", "6");
        }

        return image.ToByteArray(format);
    }

    /// <summary>
    /// 带拍摄参数、方向和全球定位系统的 JPEG，供照片信息测试使用。
    /// </summary>
    private static byte[] MakePhotoJpeg()
    {
        return MakePhotoJpegWithDate("2019:05:06 07:08:09", "SampleCamera");
    }

    /// <summary>
    /// 按指定拍摄时间和相机型号生成 JPEG，并写入一组全球定位系统坐标。
    /// </summary>
    private static byte[] MakePhotoJpegWithDate(string captureTime, string cameraMake)
    {
        using var image = new MagickImage(MagickColors.SkyBlue, 240, 160);
        image.AddNoise(NoiseType.Gaussian, 0.4);
        image.Orientation = Orientation.RightTop;
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.Make, cameraMake);
        profile.SetValue(ExifTag.Model, "SampleModel");
        profile.SetValue(ExifTag.DateTimeOriginal, captureTime);
        profile.SetValue(ExifTag.Orientation, (ushort)6);
        profile.SetValue(ExifTag.FNumber, new Rational(28, 10));
        profile.SetValue(ExifTag.GPSLatitudeRef, "N");
        profile.SetValue(ExifTag.GPSLatitude, new[] { new Rational(30, 1), new Rational(15, 1), new Rational(0, 1) });
        profile.SetValue(ExifTag.GPSLongitudeRef, "E");
        profile.SetValue(ExifTag.GPSLongitude, new[] { new Rational(120, 1), new Rational(10, 1), new Rational(0, 1) });
        image.SetProfile(profile);
        image.Quality = 92;
        return image.ToByteArray(MagickFormat.Jpeg);
    }

    /// <summary>
    /// 纯色帧的 GIF，用来检查丢帧后的总延时。
    /// </summary>
    private static byte[] MakeSolidGif(uint[] delayList)
    {
        var colorList = new[]
        {
            MagickColors.Red,
            MagickColors.Green,
            MagickColors.Blue,
            MagickColors.Yellow
        };
        using var frames = new MagickImageCollection();
        for (var index = 0; index < delayList.Length; index++)
        {
            var frame = new MagickImage(colorList[index % colorList.Length], 48, 36);
            frame.AnimationDelay = delayList[index];
            frame.GifDisposeMethod = GifDisposeMethod.None;
            frames.Add(frame);
        }

        using var stream = new MemoryStream();
        frames.Write(stream, MagickFormat.Gif);
        return stream.ToArray();
    }

    /// <summary>
    /// 带噪声的 GIF，用来确认减色后能压到阈值以下。
    /// </summary>
    private static byte[] MakeNoisyGif(uint[] delayList, int width, int height)
    {
        using var frames = new MagickImageCollection();
        for (var index = 0; index < delayList.Length; index++)
        {
            var frame = new MagickImage(MagickColors.Black, (uint)width, (uint)height);
            frame.AddNoise(NoiseType.Gaussian, 1.2);
            frame.AnimationDelay = delayList[index];
            frame.GifDisposeMethod = GifDisposeMethod.None;
            frames.Add(frame);
        }

        using var stream = new MemoryStream();
        frames.Write(stream, MagickFormat.Gif);
        return stream.ToArray();
    }

    /// <summary>
    /// 把每一帧的延时加总。单位是图像文件里的帧延时。
    /// </summary>
    private static long SumDelays(byte[] gif)
    {
        using var frames = new MagickImageCollection();
        frames.Read(gif);
        long sum = 0;
        foreach (var frame in frames)
        {
            sum += frame.AnimationDelay;
        }

        return sum;
    }

    /// <summary>
    /// 测试用的压缩参数。PATH 置空，避免误用本机安装的外部程序。
    /// </summary>
    private static NoteCompressOptions Options(
        long imageMaxBytes,
        long? gifMaxBytes = null,
        int? manualQuality = null,
        bool startFromBackup = false,
        bool stripGps = true,
        bool keepCaptureParams = true,
        string? publishedReason = null,
        string? pngquantPath = null,
        Action<NoteCompressProgress>? onProgress = null)
    {
        return new NoteCompressOptions
        {
            Limits = new NoteImageLimits
            {
                ImageMaxBytes = imageMaxBytes,
                GifMaxBytes = gifMaxBytes ?? Math.Max(imageMaxBytes, NoteImageThreshold.DefaultGifMaxBytes)
            },
            ManualQuality = manualQuality,
            StartFromOriginalBackup = startFromBackup,
            StripGps = stripGps,
            KeepCaptureParams = keepCaptureParams,
            PublishedReason = publishedReason,
            PngquantPath = pngquantPath,
            ToolPathEnvironment = "",
            OnProgress = onProgress
        };
    }

    /// <summary>
    /// 把创建时间和修改时间都拨到一个足够旧的时刻。
    /// </summary>
    private static void StampOldTimes(string path)
    {
        var created = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetCreationTimeUtc(path, created);
        File.SetLastWriteTimeUtc(path, created);
    }

    /// <summary>
    /// 修改时间必须比替换前新。
    /// </summary>
    private static void AssertWriteTimeIsNew(string path)
    {
        var old = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        Assert.True(File.GetLastWriteTimeUtc(path) > old.AddMinutes(1));
    }

    /// <summary>
    /// 文件系统能把创建时间和修改时间分开保存时，创建时间必须仍是替换前的值。
    /// </summary>
    private static void AssertCreationTimeWhenSupported(string path)
    {
        if (!CreationTimeStaysDistinct())
        {
            return;
        }

        var expected = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var actual = File.GetCreationTimeUtc(path);
        Assert.True(Math.Abs((actual - expected).TotalSeconds) < 2, "创建时间变成了 " + actual.ToString("o"));
    }

    /// <summary>
    /// 先写创建时间再写修改时间。若读回来的创建时间被改掉，说明当前系统分不开这两个时间。
    /// </summary>
    private static bool CreationTimeStaysDistinct()
    {
        if (mCreationTimeStaysDistinct.HasValue)
        {
            return mCreationTimeStaysDistinct.Value;
        }

        var path = Path.Combine(Path.GetTempPath(), "sms-ctime-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, new byte[] { 1 });
            var created = new DateTime(2018, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            File.SetCreationTimeUtc(path, created);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            mCreationTimeStaysDistinct = Math.Abs((File.GetCreationTimeUtc(path) - created).TotalSeconds) < 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            mCreationTimeStaysDistinct = false;
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return mCreationTimeStaysDistinct.Value;
    }

    /// <summary>
    /// 目录里不应留下替换用的临时文件。
    /// </summary>
    private static void AssertNoLeftover(string note)
    {
        var leftover = Directory.EnumerateFiles(note, "*", SearchOption.AllDirectories).Any(path =>
        {
            var name = Path.GetFileName(path);
            return name.Contains(".replacing-", StringComparison.Ordinal)
                || name.EndsWith(NoteImageBackup.CompressingSuffix, StringComparison.Ordinal);
        });
        Assert.False(leftover);
    }

    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
        0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0xD4, 0xEF, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };

    /// <summary>
    /// 临时笔记文件夹。测试结束时整棵删掉。
    /// </summary>
    private sealed class TempTree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "sms-note-compress-" + Guid.NewGuid().ToString("N"));

        public TempTree()
        {
            Directory.CreateDirectory(Root);
        }

        /// <summary>
        /// 建一个子目录。
        /// </summary>
        public string MakeDirectory(string name)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// 在笔记文件夹里写一张图。相对路径里的子目录会一并创建。
        /// </summary>
        public string Write(string directory, string relative, byte[] content)
        {
            var path = Path.Combine(directory, relative);
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    #endregion
}
