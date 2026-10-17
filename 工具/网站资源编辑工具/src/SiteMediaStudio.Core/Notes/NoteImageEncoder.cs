using System.Diagnostics;
using System.Xml.Linq;
using ImageMagick;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次编码的像素结果。压缩器再用它决定写回还是放弃。临时文件只出现在系统临时目录，不进入笔记文件夹。
/// </summary>
internal sealed class EncodedPicture
{
    /// <summary>
    /// 编码后的文件内容。
    /// </summary>
    public byte[] Content { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// 宽度，像素。
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// 高度，像素。
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// 是否缩小了尺寸。
    /// </summary>
    public bool DidShrinkDimensions { get; init; }

    /// <summary>
    /// 这一级实际采用的质量参数。
    /// </summary>
    public string QualityParameter { get; init; } = "";

    /// <summary>
    /// 这一级从外部程序退回内建压缩时的一句说明。没有退回时为空。
    /// </summary>
    public string FallbackNote { get; init; } = "";
}

/// <summary>
/// 用 Magick.NET 编码 JPG、PNG、WebP 和 GIF。pngquant、oxipng、gifsicle 找到时参与对应步骤，失败或缺失时退回内建编码。
/// </summary>
internal static class NoteImageEncoder
{
    private const int ToolTimeoutMs = 60_000;

    /// <summary>
    /// 读取长边像素。调用方用它决定要不要进入缩边阶段。
    /// </summary>
    internal static uint ReadLongEdge(byte[] source)
    {
        using var image = new MagickImage(source);
        return Math.Max(image.Width, image.Height);
    }

    /// <summary>
    /// 按指定质量重编码 JPEG。质量为空时，用文件里读到的原质量做一次编码表优化；读不到质量就返回空，表示跳过这一级。
    /// </summary>
    internal static EncodedPicture? EncodeJpeg(
        byte[] source,
        int? quality,
        bool shrinkLongEdge,
        bool stripGps,
        bool keepCaptureParams)
    {
        using var image = Open(source, stripGps, keepCaptureParams);
        var writeQuality = quality ?? (int)image.Quality;
        if (writeQuality < 1 || writeQuality > 100)
        {
            return null;
        }

        var didShrink = ShrinkLongEdge(image, shrinkLongEdge);
        image.Format = MagickFormat.Jpeg;
        image.Quality = (uint)writeQuality;
        image.Settings.SetDefine(MagickFormat.Jpeg, "optimize-coding", "true");
        return Picture(image.ToByteArray(MagickFormat.Jpeg), didShrink, quality.HasValue ? "" : "无损优化", "");
    }

    /// <summary>
    /// 按指定质量重编码 WebP。质量为空时沿用读到的原质量；读不到就返回空。
    /// </summary>
    internal static EncodedPicture? EncodeWebp(
        byte[] source,
        int? quality,
        bool shrinkLongEdge,
        bool stripGps,
        bool keepCaptureParams)
    {
        using var image = Open(source, stripGps, keepCaptureParams);
        var writeQuality = quality ?? (int)image.Quality;
        if (writeQuality < 1 || writeQuality > 100)
        {
            return null;
        }

        var didShrink = ShrinkLongEdge(image, shrinkLongEdge);
        image.Format = MagickFormat.WebP;
        image.Quality = (uint)writeQuality;
        image.Settings.SetDefine(MagickFormat.WebP, "lossless", "false");
        image.Settings.SetDefine(MagickFormat.WebP, "method", "6");
        var label = quality.HasValue ? "" : "无损优化";
        return Picture(image.ToByteArray(MagickFormat.WebP), didShrink, label, "");
    }

    /// <summary>
    /// PNG 无损压缩。找到 oxipng 时再跑一遍，不传去掉元数据的参数。oxipng 失败时保留内建结果。
    /// </summary>
    internal static EncodedPicture? EncodePngLossless(
        byte[] source,
        bool shrinkLongEdge,
        bool stripGps,
        bool keepCaptureParams,
        string? oxipngPath)
    {
        using var image = Open(source, stripGps, keepCaptureParams);
        var didShrink = ShrinkLongEdge(image, shrinkLongEdge);
        ApplyPngSettings(image);
        var content = image.ToByteArray(MagickFormat.Png);
        if (!string.IsNullOrWhiteSpace(oxipngPath))
        {
            var optimized = RunOxipng(oxipngPath, content);
            if (optimized != null && optimized.Length > 0 && optimized.Length < content.Length)
            {
                content = RestorePhotoIfNeeded(image, optimized, MagickFormat.Png);
            }
        }

        return Picture(content, didShrink, "无损优化", "");
    }

    /// <summary>
    /// PNG 减色。usePngquant 为真时先调用 pngquant；没有产出文件时改用内建颜色数量。
    /// </summary>
    internal static EncodedPicture? EncodePngQuantized(
        byte[] source,
        bool shrinkLongEdge,
        int colorCount,
        int pngQualityMinimum,
        int pngQualityMaximum,
        bool usePngquant,
        string qualityParameter,
        bool stripGps,
        bool keepCaptureParams,
        string? pngquantPath)
    {
        using var image = Open(source, stripGps, keepCaptureParams);
        var didShrink = ShrinkLongEdge(image, shrinkLongEdge);
        var fallbackNote = "";
        byte[]? content = null;
        if (usePngquant && !string.IsNullOrWhiteSpace(pngquantPath))
        {
            ApplyPngSettings(image);
            var prepared = image.ToByteArray(MagickFormat.Png);
            content = RunPngquant(pngquantPath, prepared, pngQualityMinimum, pngQualityMaximum);
            if (content != null)
            {
                try
                {
                    content = RestorePhotoIfNeeded(image, content, MagickFormat.Png);
                    if (!CanRead(content))
                    {
                        content = null;
                    }
                }
                catch (MagickException)
                {
                    content = null;
                }
            }
        }

        if (content == null)
        {
            if (usePngquant)
            {
                fallbackNote = "pngquant 有一级没有成功，已改用内建减色。";
                qualityParameter = ColorLabel(colorCount, didShrink);
            }

            image.Quantize(new QuantizeSettings
            {
                Colors = (uint)colorCount,
                DitherMethod = DitherMethod.FloydSteinberg
            });
            ApplyPngSettings(image);
            content = image.ToByteArray(MagickFormat.Png);
        }

        return Picture(content, didShrink, qualityParameter, fallbackNote);
    }

    /// <summary>
    /// 编码 GIF。丢帧时把被丢弃帧的延时加到保留帧上。找到 gifsicle 时再优化一次，并采用更小的那一份。
    /// </summary>
    internal static EncodedPicture? EncodeGif(
        byte[] source,
        string qualityParameter,
        int colorCount,
        int dropRounds,
        int scalePercent,
        bool optimizeOnly,
        bool stripGps,
        bool keepCaptureParams,
        string? gifsiclePath)
    {
        var builtin = RenderGif(source, colorCount, dropRounds, scalePercent, optimizeOnly, quantize: colorCount > 0, stripGps, keepCaptureParams);
        if (builtin == null)
        {
            return null;
        }

        var content = builtin;
        if (!string.IsNullOrWhiteSpace(gifsiclePath))
        {
            var prepared = colorCount > 0
                ? RenderGif(source, 0, dropRounds, scalePercent, optimizeOnly: false, quantize: false, stripGps, keepCaptureParams)
                : source;
            var toolContent = prepared == null
                ? null
                : RunGifsicle(gifsiclePath, prepared, colorCount);
            if (toolContent != null && toolContent.Length > 0 && toolContent.Length < content.Length && CanRead(toolContent))
            {
                content = toolContent;
            }
        }

        var didShrink = scalePercent is > 0 and < 100;
        return Picture(content, didShrink, qualityParameter, "");
    }

    #region 照片信息

    /// <summary>
    /// 打开图片并按开关处理全球定位系统和拍摄参数。不自动旋转像素，避免方向标记被清掉。
    /// </summary>
    private static MagickImage Open(byte[] source, bool stripGps, bool keepCaptureParams)
    {
        var image = new MagickImage(source);
        NotePhotoPolicy.Apply(image, stripGps, keepCaptureParams);
        return image;
    }

    #endregion

    #region 栅格图

    /// <summary>
    /// 长边超过 2560 像素时等比缩小，使长边变成 2560。图片本来不大于该边长时不改尺寸。
    /// </summary>
    private static bool ShrinkLongEdge(MagickImage image, bool shrinkLongEdge)
    {
        if (!shrinkLongEdge || Math.Max(image.Width, image.Height) <= NoteImageCompressor.LongEdgePixelLimit)
        {
            return false;
        }

        image.Resize(new MagickGeometry(NoteImageCompressor.LongEdgePixelLimit, NoteImageCompressor.LongEdgePixelLimit)
        {
            Greater = true
        });
        return true;
    }

    /// <summary>
    /// PNG 使用最高压缩级别。这是内建的无损优化。
    /// </summary>
    private static void ApplyPngSettings(MagickImage image)
    {
        image.Format = MagickFormat.Png;
        image.Settings.SetDefine(MagickFormat.Png, "compression-level", "9");
        image.Settings.SetDefine(MagickFormat.Png, "compression-filter", "5");
    }

    /// <summary>
    /// 外部程序可能丢掉照片信息。原图按策略处理后仍有 EXIF 时，把那份配置写回结果。PNG 写回是无损的。
    /// </summary>
    private static byte[] RestorePhotoIfNeeded(MagickImage policyImage, byte[] encoded, MagickFormat format)
    {
        var profile = policyImage.GetExifProfile();
        if (profile == null || !profile.Values.Any())
        {
            return encoded;
        }

        using var result = new MagickImage(encoded);
        result.SetProfile(profile);
        result.Orientation = policyImage.Orientation;
        if (format == MagickFormat.Png)
        {
            ApplyPngSettings(result);
        }
        else
        {
            result.Format = format;
        }

        return result.ToByteArray(format);
    }

    #endregion

    #region GIF

    /// <summary>
    /// 按丢帧、缩放和减色生成一份 GIF。优化只去掉相邻帧里重复的部分，不改总时长的计算方式。
    /// </summary>
    private static byte[]? RenderGif(
        byte[] source,
        int colorCount,
        int dropRounds,
        int scalePercent,
        bool optimizeOnly,
        bool quantize,
        bool stripGps,
        bool keepCaptureParams)
    {
        using var frames = new MagickImageCollection();
        frames.Read(source);
        if (frames.Count == 0)
        {
            return null;
        }

        if (!optimizeOnly && (dropRounds > 0 || (scalePercent > 0 && scalePercent < 100) || quantize))
        {
            frames.Coalesce();
        }

        for (var round = 0; round < dropRounds; round++)
        {
            DropEveryOtherFrame(frames);
        }

        if (scalePercent is > 0 and < 100)
        {
            var percentage = new Percentage(scalePercent);
            foreach (var frame in frames)
            {
                frame.Resize(percentage);
            }
        }

        if (quantize && colorCount > 0)
        {
            var delayList = frames.Select(frame => frame.AnimationDelay).ToArray();
            frames.Quantize(new QuantizeSettings
            {
                Colors = (uint)colorCount,
                DitherMethod = DitherMethod.FloydSteinberg
            });
            if (frames.Count == delayList.Length)
            {
                for (var index = 0; index < frames.Count; index++)
                {
                    frames[index].AnimationDelay = delayList[index];
                }
            }
        }

        foreach (var frame in frames)
        {
            NotePhotoPolicy.Apply(frame, stripGps, keepCaptureParams);
        }

        frames.Optimize();
        try
        {
            frames.OptimizeTransparency();
        }
        catch (MagickException)
        {
            // 有些动图无法再去掉透明边缘。前面的优化结果仍然可用。
        }

        using var stream = new MemoryStream();
        frames.Write(stream, MagickFormat.Gif);
        return stream.ToArray();
    }

    /// <summary>
    /// 隔一帧丢弃，并把延时加到保留下来的前一帧。克隆后再清空集合，避免和原帧一起被释放。
    /// </summary>
    private static void DropEveryOtherFrame(MagickImageCollection frames)
    {
        if (frames.Count < 2)
        {
            return;
        }

        var combinedDelayList = NoteGifFramePolicy.CombineDroppedFrameDelays(
            frames.Select(frame => (int)frame.AnimationDelay).ToArray());
        var keptFrameList = new List<IMagickImage<byte>>();
        for (var index = 0; index < frames.Count; index += 2)
        {
            var clone = frames[index].Clone();
            clone.AnimationDelay = (uint)combinedDelayList[index / 2];
            keptFrameList.Add(clone);
        }

        frames.Clear();
        foreach (var frame in keptFrameList)
        {
            frames.Add(frame);
        }
    }

    #endregion

    #region 外部程序

    /// <summary>
    /// 调用 oxipng 做无损优化。失败时返回空，由调用方保留内建结果。
    /// </summary>
    private static byte[]? RunOxipng(string filePath, byte[] content)
    {
        return RunTool(filePath, content, ".png", (inputPath, outputPath) => new[]
        {
            "-o", "2", "--out", outputPath, inputPath
        });
    }

    /// <summary>
    /// 调用 pngquant 按质量区间减色。质量达不到下限时程序会失败，调用方再改用内建减色。
    /// </summary>
    private static byte[]? RunPngquant(string filePath, byte[] content, int qualityMinimum, int qualityMaximum)
    {
        return RunTool(filePath, content, ".png", (inputPath, outputPath) => new[]
        {
            "--quality=" + qualityMinimum + "-" + qualityMaximum,
            "--speed", "1",
            "--force",
            "--output", outputPath,
            inputPath
        });
    }

    /// <summary>
    /// 调用 gifsicle 优化。指定了颜色数量时同时减色。
    /// </summary>
    private static byte[]? RunGifsicle(string filePath, byte[] content, int colorCount)
    {
        return RunTool(filePath, content, ".gif", (inputPath, outputPath) =>
        {
            var argumentList = new List<string> { "--optimize=3" };
            if (colorCount > 0)
            {
                argumentList.Add("--colors");
                argumentList.Add(colorCount.ToString());
            }

            argumentList.Add("--output");
            argumentList.Add(outputPath);
            argumentList.Add(inputPath);
            return argumentList;
        });
    }

    /// <summary>
    /// 在系统临时目录运行外部程序，读回输出文件。不写笔记文件夹，超时或失败时返回空。
    /// </summary>
    private static byte[]? RunTool(
        string filePath,
        byte[] content,
        string extension,
        Func<string, string, IReadOnlyList<string>> buildArgumentList)
    {
        var directory = Path.Combine(Path.GetTempPath(), "sms-note-compress-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var inputPath = Path.Combine(directory, "input" + extension);
        var outputPath = Path.Combine(directory, "output" + extension);
        try
        {
            File.WriteAllBytes(inputPath, content);
            var argumentList = buildArgumentList(inputPath, outputPath);
            if (!TryRunProcess(filePath, argumentList) || !File.Exists(outputPath))
            {
                return null;
            }

            var output = File.ReadAllBytes(outputPath);
            return output.Length == 0 ? null : output;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// 启动外部程序并等待结束。不使用命令行外壳，参数按列表传入。
    /// </summary>
    private static bool TryRunProcess(string filePath, IReadOnlyList<string> argumentList)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return false;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ToolTimeoutMs))
            {
                TryKill(process);
                Observe(outputTask);
                Observe(errorTask);
                return false;
            }

            process.WaitForExit();
            Observe(outputTask);
            Observe(errorTask);
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 结束后观察输出任务，避免进程退出时留下未观察异常。
    /// </summary>
    private static void Observe(Task task)
    {
        _ = task.ContinueWith(
            finished =>
            {
                _ = finished.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 结束超时的外部程序。进程已经退出时忽略。
    /// </summary>
    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    #endregion

    /// <summary>
    /// 外部程序的输出必须仍是一张能打开的图。打不开就当这一级没有产出。
    /// </summary>
    private static bool CanRead(byte[] content)
    {
        try
        {
            using var image = new MagickImage(content);
            return image.Width > 0 && image.Height > 0;
        }
        catch (MagickException)
        {
            return false;
        }
    }

    /// <summary>
    /// 读回宽高并装成一次编码结果。内容为空时返回空。
    /// </summary>
    private static EncodedPicture? Picture(byte[] content, bool didShrink, string qualityParameter, string fallbackNote)
    {
        if (content.Length == 0)
        {
            return null;
        }

        using var image = new MagickImage(content);
        return new EncodedPicture
        {
            Content = content,
            Width = (int)image.Width,
            Height = (int)image.Height,
            DidShrinkDimensions = didShrink,
            QualityParameter = qualityParameter,
            FallbackNote = fallbackNote
        };
    }

    /// <summary>
    /// 内建减色的质量参数。已经缩边时写上长边。
    /// </summary>
    private static string ColorLabel(int colorCount, bool didShrink)
    {
        var label = colorCount + " 色";
        if (didShrink)
        {
            label += "，长边 " + NoteImageCompressor.LongEdgePixelLimit;
        }

        return label;
    }
}

/// <summary>
/// 按设置改写 EXIF 和 XMP 里的全球定位系统字段，并决定是否保留拍摄参数。
/// </summary>
internal static class NotePhotoPolicy
{
    /// <summary>
    /// 就地修改这张图的照片信息。保留拍摄参数时，同时把方向写回图像属性，避免写出时被清成 0。
    /// </summary>
    internal static void Apply(IMagickImage<byte> image, bool stripGps, bool keepCaptureParams)
    {
        var profile = image.GetExifProfile();
        if (!keepCaptureParams)
        {
            image.Orientation = Orientation.Undefined;
        }

        if (profile != null)
        {
            var edited = new ExifProfile(profile.ToByteArray());
            var removeTagList = edited.Values
                .Where(value => ShouldRemove(value.Tag.ToString(), stripGps, keepCaptureParams))
                .Select(value => value.Tag)
                .ToList();
            foreach (var tag in removeTagList)
            {
                edited.RemoveValue(tag);
            }

            if (keepCaptureParams)
            {
                var orientation = image.Orientation;
                if (edited.GetValue(ExifTag.Orientation) == null && orientation != Orientation.Undefined)
                {
                    edited.SetValue(ExifTag.Orientation, (ushort)orientation);
                }
            }

            if (edited.Values.Any())
            {
                image.SetProfile(edited);
            }
            else
            {
                image.RemoveProfile("exif");
            }
        }

        if (stripGps)
        {
            StripGpsFromXmp(image);
        }
    }

    /// <summary>
    /// 不保留拍摄参数时，除了还要留下的全球定位系统字段，其余 EXIF 都去掉。
    /// 保留拍摄参数并且要去掉全球定位系统时，只去掉名称以 GPS 开头的字段。
    /// </summary>
    private static bool ShouldRemove(string? tagName, bool stripGps, bool keepCaptureParams)
    {
        var name = tagName ?? "";
        var isGps = name.StartsWith("GPS", StringComparison.Ordinal);
        if (!keepCaptureParams)
        {
            return stripGps || !isGps;
        }

        return stripGps && isGps;
    }

    /// <summary>
    /// 删除 XMP 里名称包含 GPS 的元素和属性。没有 XMP 时不做任何事。
    /// </summary>
    private static void StripGpsFromXmp(IMagickImage<byte> image)
    {
        var xmp = image.GetXmpProfile();
        if (xmp == null)
        {
            return;
        }

        var document = xmp.ToXDocument();
        if (document?.Root == null)
        {
            return;
        }

        var changed = false;
        foreach (var element in document.Descendants().ToList())
        {
            foreach (var attribute in element.Attributes().ToList())
            {
                if (!IsGpsName(attribute.Name.LocalName))
                {
                    continue;
                }

                attribute.Remove();
                changed = true;
            }

            if (!IsGpsName(element.Name.LocalName))
            {
                continue;
            }

            element.Remove();
            changed = true;
        }

        if (changed)
        {
            image.SetProfile(new XmpProfile(document));
        }
    }

    /// <summary>
    /// 本地名称里带有 GPS 的，视为全球定位系统字段。
    /// </summary>
    private static bool IsGpsName(string? name)
    {
        return !string.IsNullOrEmpty(name) && name.Contains("GPS", StringComparison.OrdinalIgnoreCase);
    }
}
