using ImageMagick;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把一篇笔记里的一张配图压到阈值以下。输出格式和文件名不变。
/// 写回只调用 <see cref="NoteImageBackup.Replace"/>：临时文件、备份到 .originals、失败回退和创建时间都由备份层负责。
/// 已发布的判断由调用方做好后放进 <see cref="NoteCompressOptions.PublishedReason"/>，这里不读索引。
/// </summary>
public static class NoteImageCompressor
{
    /// <summary>
    /// JPG、PNG、WebP 在质量阶梯用尽后，长边仍超过这个像素数就等比缩小到该边长。
    /// </summary>
    public const int LongEdgePixelLimit = 2560;

    /// <summary>
    /// JPG 和 WebP 的质量阶梯，从高到低。第一次不超过阈值就停止。
    /// </summary>
    public static readonly int[] JpegWebpQualityList = { 92, 88, 85, 80, 75, 70 };

    private static readonly int[] GifColorCountList = { 256, 128, 64 };

    private static readonly (int Minimum, int Maximum, int ColorCount)[] PngQuantLadder =
    {
        (90, 100, 256),
        (80, 95, 192),
        (70, 85, 128),
        (60, 80, 96),
        (50, 70, 64),
        (40, 60, 32)
    };

    /// <summary>
    /// 压缩笔记文件夹里的一张配图。笔记文件夹不存在时抛出 <see cref="DirectoryNotFoundException"/>。
    /// 路径为空或手动质量不在 1 到 100 之间时抛出参数异常。
    /// </summary>
    public static NoteCompressResult Compress(string noteFolder, string imagePath, NoteCompressOptions? options = null)
    {
        options ??= new NoteCompressOptions();
        ValidateOptions(options);

        var backupPath = NoteImageBackup.BackupFilePath(noteFolder, imagePath);
        var relativePath = RelativePathOf(noteFolder, imagePath);
        var fileName = FileNameOf(relativePath);
        var imageFull = FullPathOf(noteFolder, imagePath);

        if (!string.IsNullOrWhiteSpace(options.PublishedReason))
        {
            var previous = File.Exists(imageFull) ? new FileInfo(imageFull).Length : 0;
            return new NoteCompressResult
            {
                Status = NoteCompressStatus.SkippedPublished,
                RelativePath = relativePath,
                FileName = fileName,
                SourceByteCount = previous,
                PreviousByteCount = previous,
                ResultByteCount = previous,
                Message = fileName + " 已发布，已跳过",
                FailureReason = options.PublishedReason.Trim()
            };
        }

        if (!File.Exists(imageFull))
        {
            return Failed(relativePath, fileName, 0, 0, "找不到要替换的配图。");
        }

        var locked = LockIfBusy(imageFull, relativePath, fileName);
        if (locked != null)
        {
            return locked;
        }

        var sourcePath = imageFull;
        if (options.StartFromOriginalBackup && File.Exists(backupPath))
        {
            var backupLocked = LockIfBusy(backupPath, relativePath, fileName);
            if (backupLocked != null)
            {
                return backupLocked;
            }

            sourcePath = backupPath;
        }

        byte[] source;
        try
        {
            source = File.ReadAllBytes(sourcePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(relativePath, fileName, 0, new FileInfo(imageFull).Length, "读不到要压缩的配图：" + WithPeriod(exception.Message));
        }

        var previousByteCount = new FileInfo(imageFull).Length;
        var extension = Path.GetExtension(imageFull);
        if (!NoteRules.StoredImageExtSet.Contains(extension))
        {
            return Failed(relativePath, fileName, source.Length, previousByteCount, "这种配图格式不能压缩。");
        }

        var maxByteCount = NoteImageThreshold.MaxBytesFor(extension, options.Limits.ImageMaxBytes, options.Limits.GifMaxBytes);
        var kind = KindOf(extension);
        var tools = LocateTools(kind, options);
        var noteList = new List<string>();
        if (tools.Note.Length > 0)
        {
            noteList.Add(tools.Note);
        }

        var attemptByteCountList = new List<long>();
        var encodedList = new List<EncodedPicture>();
        string? lastError = null;
        var anyLarger = false;
        var succeededEarly = false;

        try
        {
            succeededEarly = RunPlans(
                BuildPlans(kind, options, tools, shrinkLongEdge: false),
                source,
                options,
                relativePath,
                maxByteCount,
                tools,
                attemptByteCountList,
                encodedList,
                noteList,
                ref anyLarger,
                ref lastError);

            if (!succeededEarly && options.ManualQuality == null && kind != PictureKind.Gif)
            {
                var longEdge = NoteImageEncoder.ReadLongEdge(source);
                if (longEdge > LongEdgePixelLimit)
                {
                    succeededEarly = RunPlans(
                        BuildPlans(kind, options, tools, shrinkLongEdge: true),
                        source,
                        options,
                        relativePath,
                        maxByteCount,
                        tools,
                        attemptByteCountList,
                        encodedList,
                        noteList,
                        ref anyLarger,
                        ref lastError);
                }
            }
        }
        catch (MagickException exception)
        {
            lastError = exception.Message;
        }

        var toolNote = JoinNotes(noteList);
        if (encodedList.Count == 0)
        {
            var reason = string.IsNullOrWhiteSpace(lastError)
                ? "没有产生可用的压缩结果。"
                : "压缩失败：" + WithPeriod(lastError);
            return Failed(relativePath, fileName, source.Length, previousByteCount, reason, toolNote);
        }

        if (!NoteCompressSelection.TryChoose(source.Length, maxByteCount, attemptByteCountList, out var chosenIndex, out var stillOverLimit))
        {
            var message = anyLarger
                ? fileName + " 压缩后比原图大，已保留原图"
                : fileName + " 压缩后没有比原图更小，已保留原图";
            return new NoteCompressResult
            {
                Status = NoteCompressStatus.AbandonedLarger,
                RelativePath = relativePath,
                FileName = fileName,
                SourceByteCount = source.Length,
                PreviousByteCount = previousByteCount,
                ResultByteCount = previousByteCount,
                Message = message,
                FailureReason = message,
                ExternalToolNote = toolNote
            };
        }

        var chosen = encodedList[chosenIndex];
        var replaced = NoteImageBackup.Replace(noteFolder, imagePath, chosen.Content);
        if (replaced.Kind == NoteImageBackupKind.Locked)
        {
            return Locked(relativePath, fileName, source.Length, previousByteCount, replaced.HolderNameList, toolNote);
        }

        if (replaced.Kind != NoteImageBackupKind.Replaced)
        {
            return Failed(relativePath, fileName, source.Length, previousByteCount, replaced.Message, toolNote);
        }

        var resultByteCount = chosen.Content.Length;
        var qualityParameter = chosen.QualityParameter;
        if (stillOverLimit)
        {
            var message = "压缩后仍超限：" + fileName + "（" + FileSizeFormat.Format(resultByteCount) + "），请自行决定换图或保留";
            return new NoteCompressResult
            {
                Status = NoteCompressStatus.StillOverLimit,
                RelativePath = relativePath,
                FileName = fileName,
                SourceByteCount = source.Length,
                PreviousByteCount = previousByteCount,
                ResultByteCount = resultByteCount,
                FinalQualityParameter = qualityParameter,
                DidShrinkDimensions = chosen.DidShrinkDimensions,
                ResultWidth = chosen.Width,
                ResultHeight = chosen.Height,
                WroteOriginalBackup = replaced.CreatedBackup,
                Message = message,
                FailureReason = message,
                ExternalToolNote = toolNote
            };
        }

        var success = fileName + " 已压到上限以下（"
            + FileSizeFormat.Format(previousByteCount) + " → " + FileSizeFormat.Format(resultByteCount)
            + "，" + qualityParameter + "）。";
        if (!replaced.CreatedBackup)
        {
            success += "原有备份未覆盖。";
        }
        return new NoteCompressResult
        {
            Status = NoteCompressStatus.Succeeded,
            RelativePath = relativePath,
            FileName = fileName,
            SourceByteCount = source.Length,
            PreviousByteCount = previousByteCount,
            ResultByteCount = resultByteCount,
            FinalQualityParameter = qualityParameter,
            DidShrinkDimensions = chosen.DidShrinkDimensions,
            ResultWidth = chosen.Width,
            ResultHeight = chosen.Height,
            WroteOriginalBackup = replaced.CreatedBackup,
            Message = success,
            ExternalToolNote = toolNote
        };
    }

    #region 尝试阶梯

    /// <summary>
    /// 跑完一批尝试。某一级已经不超过阈值并且比原图小时返回真，调用方就不再做更低的一级。
    /// </summary>
    private static bool RunPlans(
        IReadOnlyList<Plan> planList,
        byte[] source,
        NoteCompressOptions options,
        string relativePath,
        long maxByteCount,
        LocatedTools tools,
        List<long> attemptByteCountList,
        List<EncodedPicture> encodedList,
        List<string> noteList,
        ref bool anyLarger,
        ref string? lastError)
    {
        foreach (var plan in planList)
        {
            options.OnProgress?.Invoke(new NoteCompressProgress
            {
                RelativePath = relativePath,
                Detail = "正在尝试：" + plan.Label
            });

            EncodedPicture? encoded;
            try
            {
                encoded = EncodePlan(plan, source, options, tools);
            }
            catch (MagickException exception)
            {
                lastError = exception.Message;
                continue;
            }

            if (encoded == null || encoded.Content.Length == 0)
            {
                continue;
            }

            if (encoded.FallbackNote.Length > 0)
            {
                noteList.Add(encoded.FallbackNote);
            }

            if (encoded.Content.Length > source.Length)
            {
                anyLarger = true;
            }

            attemptByteCountList.Add(encoded.Content.Length);
            encodedList.Add(encoded);
            if (NoteCompressSelection.TryChoose(source.Length, maxByteCount, attemptByteCountList, out _, out var stillOverLimit)
                && !stillOverLimit)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 按格式和是否缩边，生成从高到低的尝试。手动质量只生成一级。
    /// </summary>
    private static List<Plan> BuildPlans(PictureKind kind, NoteCompressOptions options, LocatedTools tools, bool shrinkLongEdge)
    {
        var planList = new List<Plan>();
        var manual = options.ManualQuality;
        if (manual.HasValue)
        {
            if (shrinkLongEdge)
            {
                return planList;
            }

            planList.Add(ManualPlan(kind, manual.Value, tools.PngquantPath != null));
            return planList;
        }

        switch (kind)
        {
            case PictureKind.Jpeg:
            case PictureKind.Webp:
                if (!shrinkLongEdge)
                {
                    planList.Add(new Plan
                    {
                        Kind = kind == PictureKind.Jpeg ? PlanKind.Jpeg : PlanKind.Webp,
                        Label = "无损优化"
                    });
                }

                foreach (var quality in JpegWebpQualityList)
                {
                    planList.Add(new Plan
                    {
                        Kind = kind == PictureKind.Jpeg ? PlanKind.Jpeg : PlanKind.Webp,
                        Quality = quality,
                        ShrinkLongEdge = shrinkLongEdge,
                        Label = shrinkLongEdge
                            ? "质量 " + quality + "，长边 " + LongEdgePixelLimit
                            : "质量 " + quality
                    });
                }

                break;
            case PictureKind.Png:
                planList.Add(new Plan
                {
                    Kind = PlanKind.PngLossless,
                    ShrinkLongEdge = shrinkLongEdge,
                    Label = shrinkLongEdge ? "无损优化，长边 " + LongEdgePixelLimit : "无损优化"
                });
                foreach (var step in PngQuantLadder)
                {
                    var usePngquant = tools.PngquantPath != null;
                    var label = usePngquant
                        ? "pngquant 质量 " + step.Minimum + "-" + step.Maximum
                        : step.ColorCount + " 色";
                    if (shrinkLongEdge)
                    {
                        label += "，长边 " + LongEdgePixelLimit;
                    }

                    planList.Add(new Plan
                    {
                        Kind = PlanKind.PngQuantized,
                        ShrinkLongEdge = shrinkLongEdge,
                        ColorCount = step.ColorCount,
                        PngQualityMinimum = step.Minimum,
                        PngQualityMaximum = step.Maximum,
                        UsePngquant = usePngquant,
                        Label = label
                    });
                }

                break;
            case PictureKind.Gif:
                if (!shrinkLongEdge)
                {
                    planList.AddRange(GifPlans());
                }

                break;
        }

        return planList;
    }

    /// <summary>
    /// 手动质量只编码一次。PNG 和 GIF 把质量换算成颜色数量或 pngquant 的质量区间。
    /// </summary>
    private static Plan ManualPlan(PictureKind kind, int quality, bool pngquantAvailable)
    {
        switch (kind)
        {
            case PictureKind.Png:
                var minimum = Math.Max(0, quality - 5);
                var maximum = Math.Min(100, quality + 5);
                var colorCount = ColorCountFromQuality(quality);
                return new Plan
                {
                    Kind = PlanKind.PngQuantized,
                    Quality = quality,
                    ColorCount = colorCount,
                    PngQualityMinimum = minimum,
                    PngQualityMaximum = maximum,
                    UsePngquant = pngquantAvailable,
                    Label = pngquantAvailable
                        ? "pngquant 质量 " + minimum + "-" + maximum
                        : colorCount + " 色"
                };
            case PictureKind.Gif:
                var gifColors = ColorCountFromQuality(quality);
                return new Plan
                {
                    Kind = PlanKind.Gif,
                    Quality = quality,
                    ColorCount = gifColors,
                    GifScalePercent = 100,
                    Label = gifColors + " 色"
                };
            case PictureKind.Webp:
                return new Plan
                {
                    Kind = PlanKind.Webp,
                    Quality = quality,
                    Label = "质量 " + quality
                };
            default:
                return new Plan
                {
                    Kind = PlanKind.Jpeg,
                    Quality = quality,
                    Label = "质量 " + quality
                };
        }
    }

    /// <summary>
    /// GIF 先无损优化和减色，仍超限的后续级别才丢帧或缩小。丢帧可以做两轮。
    /// </summary>
    private static IEnumerable<Plan> GifPlans()
    {
        yield return new Plan
        {
            Kind = PlanKind.Gif,
            GifOptimizeOnly = true,
            GifScalePercent = 100,
            Label = "无损优化"
        };

        foreach (var colorCount in GifColorCountList)
        {
            yield return GifPlan(colorCount, 0, 100, colorCount + " 色");
        }

        yield return GifPlan(128, 1, 100, "128 色，并隔一帧丢弃");
        yield return GifPlan(64, 1, 100, "64 色，并隔一帧丢弃");
        yield return GifPlan(64, 2, 100, "64 色，并再次隔一帧丢弃");
        yield return GifPlan(128, 0, 75, "128 色，长边缩小到原来的 75%");
        yield return GifPlan(64, 0, 50, "64 色，长边缩小到原来的 50%");
        yield return GifPlan(64, 1, 50, "64 色，长边缩小到原来的 50%，并隔一帧丢弃");
    }

    /// <summary>
    /// 组装一级 GIF 尝试。
    /// </summary>
    private static Plan GifPlan(int colorCount, int dropRounds, int scalePercent, string label)
    {
        return new Plan
        {
            Kind = PlanKind.Gif,
            ColorCount = colorCount,
            GifDropRounds = dropRounds,
            GifScalePercent = scalePercent,
            Label = label
        };
    }

    /// <summary>
    /// 把 1 到 100 的质量换成 2 到 256 的颜色数量。
    /// </summary>
    private static int ColorCountFromQuality(int quality)
    {
        var colorCount = (int)Math.Round(quality * 256 / 100.0);
        if (colorCount < 2)
        {
            colorCount = 2;
        }

        if (colorCount > 256)
        {
            colorCount = 256;
        }

        return colorCount;
    }

    /// <summary>
    /// 执行一级编码。质量参数优先用这一级的说明；内建回退时改用编码器写回的说明。
    /// </summary>
    private static EncodedPicture? EncodePlan(Plan plan, byte[] source, NoteCompressOptions options, LocatedTools tools)
    {
        EncodedPicture? encoded = plan.Kind switch
        {
            PlanKind.Jpeg => NoteImageEncoder.EncodeJpeg(source, plan.Quality, plan.ShrinkLongEdge, options.StripGps, options.KeepCaptureParams),
            PlanKind.Webp => NoteImageEncoder.EncodeWebp(source, plan.Quality, plan.ShrinkLongEdge, options.StripGps, options.KeepCaptureParams),
            PlanKind.PngLossless => NoteImageEncoder.EncodePngLossless(source, plan.ShrinkLongEdge, options.StripGps, options.KeepCaptureParams, tools.OxipngPath),
            PlanKind.PngQuantized => NoteImageEncoder.EncodePngQuantized(
                source,
                plan.ShrinkLongEdge,
                plan.ColorCount,
                plan.PngQualityMinimum,
                plan.PngQualityMaximum,
                plan.UsePngquant,
                plan.Label,
                options.StripGps,
                options.KeepCaptureParams,
                tools.PngquantPath),
            PlanKind.Gif => NoteImageEncoder.EncodeGif(
                source,
                plan.Label,
                plan.ColorCount,
                plan.GifDropRounds,
                plan.GifScalePercent,
                plan.GifOptimizeOnly,
                options.StripGps,
                options.KeepCaptureParams,
                tools.GifsiclePath),
            _ => null
        };

        if (encoded == null)
        {
            return null;
        }

        if (encoded.FallbackNote.Length == 0)
        {
            encoded = new EncodedPicture
            {
                Content = encoded.Content,
                Width = encoded.Width,
                Height = encoded.Height,
                DidShrinkDimensions = encoded.DidShrinkDimensions,
                QualityParameter = plan.Label,
                FallbackNote = ""
            };
        }

        return encoded;
    }

    #endregion

    #region 外部程序

    /// <summary>
    /// 按格式查找可选的外部程序。找不到时记下降级说明，不失败。
    /// </summary>
    private static LocatedTools LocateTools(PictureKind kind, NoteCompressOptions options)
    {
        var noteList = new List<string>();
        string? pngquantPath = null;
        string? oxipngPath = null;
        string? gifsiclePath = null;
        if (kind == PictureKind.Png)
        {
            pngquantPath = LocateOne(
                "pngquant",
                options.PngquantPath,
                options.ToolPathEnvironment,
                ExternalToolLocator.LocatePngquant,
                "PNG 减色已改用内建压缩。",
                noteList);
            oxipngPath = LocateOne(
                "oxipng",
                options.OxipngPath,
                options.ToolPathEnvironment,
                ExternalToolLocator.LocateOxipng,
                "PNG 无损优化已改用内建压缩。",
                noteList);
        }
        else if (kind == PictureKind.Gif)
        {
            gifsiclePath = LocateOne(
                "gifsicle",
                options.GifsiclePath,
                options.ToolPathEnvironment,
                ExternalToolLocator.LocateGifsicle,
                "GIF 优化已改用内建压缩。",
                noteList);
        }

        return new LocatedTools(pngquantPath, oxipngPath, gifsiclePath, JoinNotes(noteList));
    }

    /// <summary>
    /// 查找一个外部程序。填写的路径不存在时不再改用其他位置，和查找器的规则一致。
    /// </summary>
    private static string? LocateOne(
        string displayName,
        string? configuredPath,
        string? pathEnvironment,
        Func<string?, string?, int, ExternalToolStatus> locate,
        string missingAction,
        List<string> noteList)
    {
        var status = locate(configuredPath, pathEnvironment, ExternalToolLocator.DefaultVersionTimeoutMs);
        if (status.Found && !string.IsNullOrWhiteSpace(status.FilePath))
        {
            return status.FilePath;
        }

        noteList.Add(status.ConfiguredPathMissing
            ? "填写的 " + displayName + " 路径不存在，已改用内建压缩。"
            : "没有找到 " + displayName + "，" + missingAction);
        return null;
    }

    #endregion

    #region 路径与结果

    /// <summary>
    /// 检查手动质量和阈值。空白路径留给备份层抛出。
    /// </summary>
    private static void ValidateOptions(NoteCompressOptions options)
    {
        if (options.Limits == null || options.Limits.ImageMaxBytes < 1 || options.Limits.GifMaxBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "配图大小上限必须大于 0。");
        }

        if (options.ManualQuality.HasValue && (options.ManualQuality.Value < 1 || options.ManualQuality.Value > 100))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "手动质量必须在 1 到 100 之间。");
        }
    }

    /// <summary>
    /// 文件被占用时返回跳过结果，否则返回空。
    /// </summary>
    private static NoteCompressResult? LockIfBusy(string fullPath, string relativePath, string fileName)
    {
        var report = FileLockProbe.Probe(fullPath);
        if (!report.IsLocked)
        {
            return null;
        }

        var length = new FileInfo(fullPath).Length;
        return Locked(relativePath, fileName, length, length, report.HolderNameList, "");
    }

    /// <summary>
    /// 被占用的结果。说明里带上重试，和结果框的文案一致。
    /// </summary>
    private static NoteCompressResult Locked(
        string relativePath,
        string fileName,
        long sourceByteCount,
        long previousByteCount,
        IReadOnlyList<string> holderNameList,
        string toolNote)
    {
        var names = holderNameList ?? Array.Empty<string>();
        var message = names.Count == 0
            ? fileName + " 正被其他程序占用，已跳过。请关闭该程序后点击「重试」"
            : fileName + " 正被 " + string.Join("、", names) + " 占用，已跳过。请关闭该程序后点击「重试」";
        return new NoteCompressResult
        {
            Status = NoteCompressStatus.SkippedLocked,
            RelativePath = relativePath,
            FileName = fileName,
            SourceByteCount = sourceByteCount,
            PreviousByteCount = previousByteCount,
            ResultByteCount = previousByteCount,
            Message = message,
            FailureReason = message,
            HolderNameList = names,
            ExternalToolNote = toolNote
        };
    }

    /// <summary>
    /// 没有写回的失败结果。
    /// </summary>
    private static NoteCompressResult Failed(
        string relativePath,
        string fileName,
        long sourceByteCount,
        long previousByteCount,
        string reason,
        string toolNote = "")
    {
        var message = string.IsNullOrWhiteSpace(reason) ? "压缩失败。" : reason.Trim();
        return new NoteCompressResult
        {
            Status = NoteCompressStatus.Failed,
            RelativePath = relativePath,
            FileName = fileName,
            SourceByteCount = sourceByteCount,
            PreviousByteCount = previousByteCount,
            ResultByteCount = previousByteCount,
            Message = message,
            FailureReason = message,
            ExternalToolNote = toolNote
        };
    }

    /// <summary>
    /// 相对笔记文件夹的路径。备份层已经确认它落在文件夹里面。
    /// </summary>
    private static string RelativePathOf(string noteFolder, string imagePath)
    {
        var noteRoot = Path.GetFullPath(noteFolder);
        var full = FullPathOf(noteFolder, imagePath);
        return JsonUtil.ToRel(Path.GetRelativePath(noteRoot, full));
    }

    /// <summary>
    /// 配图的绝对路径。相对路径相对于笔记文件夹。
    /// </summary>
    private static string FullPathOf(string noteFolder, string imagePath)
    {
        var noteRoot = Path.GetFullPath(noteFolder);
        return Path.IsPathRooted(imagePath)
            ? Path.GetFullPath(imagePath)
            : Path.GetFullPath(Path.Combine(noteRoot, imagePath));
    }

    /// <summary>
    /// 从相对路径取出文件名。
    /// </summary>
    private static string FileNameOf(string relativePath)
    {
        var name = Path.GetFileName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "这张配图" : name;
    }

    /// <summary>
    /// 给说明补上句号，避免两句粘在一起。
    /// </summary>
    private static string WithPeriod(string? text)
    {
        var detail = string.IsNullOrWhiteSpace(text) ? "没有说明原因" : text.Trim();
        if (detail.EndsWith('。') || detail.EndsWith('.'))
        {
            return detail.EndsWith('.') ? detail[..^1] + "。" : detail;
        }

        return detail + "。";
    }

    /// <summary>
    /// 把降级说明合成一段。重复的句子只留一次。
    /// </summary>
    private static string JoinNotes(IReadOnlyList<string> noteList)
    {
        var uniqueList = new List<string>();
        foreach (var note in noteList)
        {
            if (string.IsNullOrWhiteSpace(note) || uniqueList.Contains(note))
            {
                continue;
            }

            uniqueList.Add(note.Trim());
        }

        return string.Join("", uniqueList);
    }

    /// <summary>
    /// 按扩展名区分四种配图。调用前已经确认扩展名在允许存放的集合里。
    /// </summary>
    private static PictureKind KindOf(string extension)
    {
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return PictureKind.Jpeg;
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return PictureKind.Png;
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return PictureKind.Webp;
        }

        return PictureKind.Gif;
    }

    #endregion

    #region 内部类型

    private enum PictureKind
    {
        Jpeg,
        Png,
        Webp,
        Gif
    }

    private enum PlanKind
    {
        Jpeg,
        Webp,
        PngLossless,
        PngQuantized,
        Gif
    }

    /// <summary>
    /// 一级压缩尝试。手动质量和自动阶梯都用它描述。
    /// </summary>
    private sealed class Plan
    {
        public PlanKind Kind { get; init; }

        public string Label { get; init; } = "";

        public int? Quality { get; init; }

        public bool ShrinkLongEdge { get; init; }

        public int ColorCount { get; init; }

        public int PngQualityMinimum { get; init; }

        public int PngQualityMaximum { get; init; }

        public bool UsePngquant { get; init; }

        public int GifDropRounds { get; init; }

        public int GifScalePercent { get; init; } = 100;

        public bool GifOptimizeOnly { get; init; }
    }

    /// <summary>
    /// 这次压缩实际用到的外部程序路径。为空表示改用内建能力。
    /// </summary>
    private readonly record struct LocatedTools(string? PngquantPath, string? OxipngPath, string? GifsiclePath, string Note);

    #endregion
}
