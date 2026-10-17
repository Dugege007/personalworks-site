namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一张心得配图的压缩结果。成功、比原图大而放弃、压缩后仍超限、已发布或被占用而跳过，都用不同状态表示。
/// </summary>
public enum NoteCompressStatus
{
    /// <summary>
    /// 已写回，并且新文件没有超过对应阈值。
    /// </summary>
    Succeeded,

    /// <summary>
    /// 没有得到比原图更小的结果，正式文件保持原样，也不写备份。
    /// 与原图一样大时同样放弃，避免只为了重编码而替换文件。
    /// </summary>
    AbandonedLarger,

    /// <summary>
    /// 已写回比原图更小的文件，但仍然超过阈值。这不是成功。
    /// </summary>
    StillOverLimit,

    /// <summary>
    /// 这张配图已发布到网站，调用方要求跳过。文件没有改动。
    /// </summary>
    SkippedPublished,

    /// <summary>
    /// 文件被占用，已跳过。文件没有改动。
    /// </summary>
    SkippedLocked,

    /// <summary>
    /// 没有完成。正式文件保持调用前的样子。
    /// </summary>
    Failed
}

/// <summary>
/// 压缩进行到哪一级时的进度。第 10 步可以把它写进进度窗。
/// </summary>
public sealed class NoteCompressProgress
{
    /// <summary>
    /// 相对笔记文件夹的路径，使用正斜杠。
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 当前正在尝试的说明，例如「正在尝试：质量 85」。
    /// </summary>
    public string Detail { get; init; } = "";
}

/// <summary>
/// 一次压缩使用的阈值、照片信息开关、外部程序路径，以及可选的手动质量。
/// 不读取本机设置文件。第 10 步用 <see cref="FromSettings"/> 把设置交进来。
/// </summary>
public sealed class NoteCompressOptions
{
    /// <summary>
    /// 普通配图和 GIF 的大小上限。默认 500 KB 和 2 MB。
    /// </summary>
    public NoteImageLimits Limits { get; init; } = NoteImageLimits.CreateDefault();

    /// <summary>
    /// 为真时去掉全球定位系统位置。默认去掉。
    /// </summary>
    public bool StripGps { get; init; } = true;

    /// <summary>
    /// 为真时保留拍摄参数、拍摄时间和方向信息。默认保留。
    /// </summary>
    public bool KeepCaptureParams { get; init; } = true;

    /// <summary>
    /// pngquant 的路径。空白表示按外部程序查找规则自己找。
    /// </summary>
    public string? PngquantPath { get; init; }

    /// <summary>
    /// oxipng 的路径。空白表示按外部程序查找规则自己找。
    /// </summary>
    public string? OxipngPath { get; init; }

    /// <summary>
    /// gifsicle 的路径。空白表示按外部程序查找规则自己找。
    /// </summary>
    public string? GifsiclePath { get; init; }

    /// <summary>
    /// 查找外部程序时使用的 PATH 文本。null 表示当前进程的 PATH。
    /// 空字符串表示不搜索 PATH，测试用它模拟程序不存在。
    /// </summary>
    public string? ToolPathEnvironment { get; init; }

    /// <summary>
    /// 调用方已经判定的已发布原因。不是空白时直接跳过，压缩器自己不读心得索引和媒体台账。
    /// 应传入 <see cref="NoteImageGuard.PublishedBlockText"/> 或同一句说明。
    /// </summary>
    public string? PublishedReason { get; init; }

    /// <summary>
    /// 手动质量，1 到 100。为空时按默认阶梯从高到低尝试。
    /// 有值时只按这一级编码，不再缩边、丢帧。结果仍超限时也允许写回。
    /// </summary>
    public int? ManualQuality { get; init; }

    /// <summary>
    /// 为真并且 .originals 里已有备份时，从那份最早的原图重新压缩，不覆盖备份。
    /// 没有备份时仍压缩当前文件。
    /// </summary>
    public bool StartFromOriginalBackup { get; init; }

    /// <summary>
    /// 每一级尝试开始时调用。为空则不报告进度。
    /// </summary>
    public Action<NoteCompressProgress>? OnProgress { get; init; }

    /// <summary>
    /// 用本机设置和仓库里的阈值组成一份压缩参数。
    /// </summary>
    public static NoteCompressOptions FromSettings(AppSettings settings, NoteImageLimits limits)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (limits == null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        return new NoteCompressOptions
        {
            Limits = limits,
            StripGps = settings.StripGpsEnabled,
            KeepCaptureParams = settings.KeepCaptureParamsEnabled,
            PngquantPath = settings.PngquantPath,
            OxipngPath = settings.OxipngPath,
            GifsiclePath = settings.GifsiclePath
        };
    }
}

/// <summary>
/// 一张配图的压缩结果，字段用完整词语，方便第 10 步写进结果框和 run.log。
/// </summary>
public sealed class NoteCompressResult
{
    /// <summary>
    /// 结果状态。
    /// </summary>
    public NoteCompressStatus Status { get; init; }

    /// <summary>
    /// 相对笔记文件夹的路径，使用正斜杠。
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 文件名，不含文件夹。
    /// </summary>
    public string FileName { get; init; } = "";

    /// <summary>
    /// 参与「比原图大」比较的字节数。从原图备份重新压缩时，这是备份的大小；否则是替换前正式文件的大小。
    /// </summary>
    public long SourceByteCount { get; init; }

    /// <summary>
    /// 替换前正式文件的字节数。文件不存在时为 0。
    /// </summary>
    public long PreviousByteCount { get; init; }

    /// <summary>
    /// 写回后的字节数。放弃、跳过或失败时与替换前正式文件相同。
    /// </summary>
    public long ResultByteCount { get; init; }

    /// <summary>
    /// 最终采用的质量参数，例如「质量 85」「256 色」「无损优化，长边 2560」。没有写回时为空。
    /// </summary>
    public string FinalQualityParameter { get; init; } = "";

    /// <summary>
    /// 是否缩小了尺寸。JPG、PNG、WebP 指长边缩到 2560 像素；GIF 指缩到原来的 75% 或 50%。
    /// </summary>
    public bool DidShrinkDimensions { get; init; }

    /// <summary>
    /// 写回后的宽度，像素。没有写回时为 0。
    /// </summary>
    public int ResultWidth { get; init; }

    /// <summary>
    /// 写回后的高度，像素。没有写回时为 0。
    /// </summary>
    public int ResultHeight { get; init; }

    /// <summary>
    /// 这次是否新写了一份原图备份。已经有备份时为假，表示没有覆盖第一次的原图。
    /// </summary>
    public bool WroteOriginalBackup { get; init; }

    /// <summary>
    /// 给人看的一句说明，可直接放进结果框。
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// 没有完全成功时的原因。成功时为空。
    /// 仍超限、比原图大而放弃、已发布、被占用和写盘失败都把说明放在这里。
    /// 已发布时这里是调用方传入的原因，应与 <see cref="NoteImageGuard.PublishedBlockText"/> 一致。
    /// </summary>
    public string FailureReason { get; init; } = "";

    /// <summary>
    /// 占用程序的名称。没有查到时为空列表。
    /// </summary>
    public IReadOnlyList<string> HolderNameList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 外部程序缺失或单级失败时的降级说明。没有降级时为空。
    /// </summary>
    public string ExternalToolNote { get; init; } = "";
}

/// <summary>
/// 按从高到低的顺序，从一组已经压好的大小里选出要写回的一级。
/// 某一级不比原图小，就放弃这一级。第一次不超过阈值、并且比原图小的尝试立即采用。
/// 全部尝试都超限时，采用比原图小的那一级里最小的。没有任何更小的结果就整体放弃。
/// </summary>
public static class NoteCompressSelection
{
    /// <summary>
    /// 选出要写回的尝试。返回假表示放弃，正式文件应保持原样。
    /// </summary>
    public static bool TryChoose(
        long originalByteCount,
        long maxByteCount,
        IReadOnlyList<long> attemptByteCountList,
        out int chosenIndex,
        out bool stillOverLimit)
    {
        if (attemptByteCountList == null)
        {
            throw new ArgumentNullException(nameof(attemptByteCountList));
        }

        if (originalByteCount < 0 || maxByteCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(originalByteCount), "原图大小不能为负，阈值必须大于 0。");
        }

        chosenIndex = -1;
        stillOverLimit = false;
        long bestOverByteCount = long.MaxValue;
        var bestOverIndex = -1;
        for (var index = 0; index < attemptByteCountList.Count; index++)
        {
            var byteCount = attemptByteCountList[index];
            if (byteCount >= originalByteCount)
            {
                continue;
            }

            if (byteCount <= maxByteCount)
            {
                chosenIndex = index;
                stillOverLimit = false;
                return true;
            }

            if (byteCount < bestOverByteCount)
            {
                bestOverByteCount = byteCount;
                bestOverIndex = index;
            }
        }

        if (bestOverIndex < 0)
        {
            return false;
        }

        chosenIndex = bestOverIndex;
        stillOverLimit = true;
        return true;
    }
}
