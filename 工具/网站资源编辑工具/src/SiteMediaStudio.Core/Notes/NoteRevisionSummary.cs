using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把一次内容差异写成中文短说明。
/// 资源编辑工具默认使用 <see cref="HeuristicRevisionSummaryGenerator"/>，完全在本机完成，不调用网络模型。
/// 若要换成更强的生成器：实现本接口，并在程序启动时赋给 <see cref="NoteConfigStore.SummaryGenerator"/>，
/// 或在单次 <see cref="NoteConfigStore.AppendModified"/> 调用时传入。修订条目仍然只保存 at、summary、source，不必改 JSON 形状。
/// 生成器可以返回空字符串或抛出异常；调用方会改用「内容已更新。」并继续保存，不得因此中断发布。
/// Git 提交说明日后可以抄这段摘要，但网页只读 <c>心得.json</c> 与 <c>index.json</c>，不读 Git。
/// </summary>
public interface IRevisionSummaryGenerator
{
    /// <summary>
    /// 根据已经整理好的差异生成短说明。没有可写的变化时返回空字符串。
    /// </summary>
    string Generate(NoteRevisionDiff diff);
}

/// <summary>
/// 自动摘要的长度与失败回退。调用方负责截断，生成器不必各自实现。
/// </summary>
public static class NoteRevisionSummaries
{
    /// <summary>
    /// 生成失败、或没有上一版可对照时写入的固定说明。
    /// </summary>
    public const string Fallback = "内容已更新。";

    /// <summary>
    /// 自动摘要的最长字数。加长只改这一处，不必改修订字段。
    /// </summary>
    public const int MaxLength = 80;

    /// <summary>
    /// 收成单行。空结果返回空字符串，由调用方决定是否改用回退句。超出长度时保留前段并加上省略号。
    /// </summary>
    public static string Fit(string? summary)
    {
        var text = (summary ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace('\n', ' ').Trim();
        if (text.Length == 0)
        {
            return "";
        }

        if (text.Length <= MaxLength)
        {
            return text;
        }

        return text[..(MaxLength - 1)] + "…";
    }
}

/// <summary>
/// 上一版与当前版之间的差异。只存在于生成期间，不写入 <c>心得.json</c>。
/// 启发式可以只读整理好的标题、小节和配图计数；日后的模型实现可以直接读正文全文。
/// </summary>
public sealed class NoteRevisionDiff
{
    public bool TitleChanged { get; init; }
    public string PreviousTitle { get; init; } = "";
    public string CurrentTitle { get; init; } = "";

    /// <summary>
    /// 列表导语 <see cref="NoteConfig.Summary"/> 是否变化。不是修订条目自己的 summary。
    /// </summary>
    public bool LeadChanged { get; init; }
    public string PreviousLead { get; init; } = "";
    public string CurrentLead { get; init; } = "";

    public bool ProseChanged { get; init; }
    public bool HeadingOrderChanged { get; init; }
    public IReadOnlyList<string> AddedHeadings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RemovedHeadings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AddedImages { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RemovedImages { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 正文里的图片地址有增删。配图文件已经有增删时，启发式不再单列这一项，避免同一件事说两遍。
    /// </summary>
    public bool ImageReferencesChanged { get; init; }

    public string PreviousBody { get; init; } = "";
    public string CurrentBody { get; init; } = "";

    public bool HasMeaningfulChange =>
        TitleChanged
        || LeadChanged
        || ProseChanged
        || HeadingOrderChanged
        || AddedHeadings.Count > 0
        || RemovedHeadings.Count > 0
        || AddedImages.Count > 0
        || RemovedImages.Count > 0
        || ImageReferencesChanged;
}

/// <summary>
/// 上次记下修订时的内容快照。只留在夹内 <c>心得.json</c>，不上页，也不进 <c>index.json</c>。
/// </summary>
public sealed class NoteRevisionBaseline
{
    public string Title { get; set; } = "";

    /// <summary>
    /// 当时的列表导语，对应 <see cref="NoteConfig.Summary"/>。
    /// </summary>
    public string Lead { get; set; } = "";

    /// <summary>
    /// 归一化后的正文。换行符种类和行尾空白不保留。
    /// </summary>
    public string Body { get; set; } = "";

    /// <summary>
    /// 夹内配图的相对路径，含 GIF。不含成片目录和原图备份。
    /// </summary>
    public List<string> Images { get; set; } = new();
}

/// <summary>
/// 本机确定性摘要。按展示标题、小节、配图数量和正文是否改动拼成一句中文。
/// 不要在这里接入网络模型；替换时另写 <see cref="IRevisionSummaryGenerator"/>。
/// </summary>
public sealed class HeuristicRevisionSummaryGenerator : IRevisionSummaryGenerator
{
    public static readonly HeuristicRevisionSummaryGenerator Instance = new();

    private const int NamePreviewLength = 24;

    /// <summary>
    /// 拼一句不超过 <see cref="NoteRevisionSummaries.MaxLength"/> 字的说明。没有实质差异时返回空字符串。
    /// </summary>
    public string Generate(NoteRevisionDiff diff)
    {
        if (!diff.HasMeaningfulChange)
        {
            return "";
        }

        var named = BuildParts(diff, useHeadingNames: true);
        var text = Join(named);
        if (text.Length <= NoteRevisionSummaries.MaxLength)
        {
            return text;
        }

        var counted = BuildParts(diff, useHeadingNames: false);
        text = FitParts(counted);
        return text;
    }

    /// <summary>
    /// 按具体程度从高到低排列。超长时从句末丢掉正文、导语等较泛的部分。
    /// </summary>
    private static List<string> BuildParts(NoteRevisionDiff diff, bool useHeadingNames)
    {
        var parts = new List<string>();
        Add(parts, TitlePart(diff));
        Add(parts, HeadingPart("新增", diff.AddedHeadings, useHeadingNames));
        Add(parts, HeadingPart("移除", diff.RemovedHeadings, useHeadingNames));
        if (diff.HeadingOrderChanged)
        {
            parts.Add("调整了小节顺序");
        }

        if (diff.AddedImages.Count > 0)
        {
            parts.Add("新增 " + diff.AddedImages.Count + " 张配图");
        }

        if (diff.RemovedImages.Count > 0)
        {
            parts.Add("移除 " + diff.RemovedImages.Count + " 张配图");
        }

        if (diff.AddedImages.Count == 0 && diff.RemovedImages.Count == 0 && diff.ImageReferencesChanged)
        {
            parts.Add("调整了配图引用");
        }

        if (diff.LeadChanged)
        {
            parts.Add("更新了导语");
        }

        if (diff.ProseChanged)
        {
            parts.Add("更新了正文");
        }

        return parts;
    }

    private static string FitParts(List<string> parts)
    {
        while (parts.Count > 1 && Join(parts).Length > NoteRevisionSummaries.MaxLength)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        var text = Join(parts);
        if (text.Length <= NoteRevisionSummaries.MaxLength)
        {
            return text;
        }

        return NoteRevisionSummaries.Fit(text);
    }

    private static string Join(IReadOnlyList<string> parts)
    {
        if (parts.Count == 0)
        {
            return "";
        }

        return string.Join("，", parts) + "。";
    }

    private static void Add(List<string> parts, string part)
    {
        if (part.Length > 0)
        {
            parts.Add(part);
        }
    }

    private static string TitlePart(NoteRevisionDiff diff)
    {
        if (!diff.TitleChanged)
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(diff.CurrentTitle))
        {
            return "清空了展示标题";
        }

        if (string.IsNullOrWhiteSpace(diff.PreviousTitle))
        {
            return "设置展示标题" + Quote(diff.CurrentTitle);
        }

        return "展示标题改为" + Quote(diff.CurrentTitle);
    }

    private static string HeadingPart(string verb, IReadOnlyList<string> names, bool useNames)
    {
        if (names.Count == 0)
        {
            return "";
        }

        if (!useNames || names.Count > 2)
        {
            return verb + " " + names.Count + " 个小节";
        }

        var quoted = "";
        foreach (var name in names)
        {
            quoted += Quote(name);
        }

        return verb + "小节" + quoted;
    }

    private static string Quote(string name)
    {
        var text = name.Trim().Replace("\r", "").Replace("\n", " ");
        if (text.Length > NamePreviewLength)
        {
            text = text[..(NamePreviewLength - 1)] + "…";
        }

        return "「" + text + "」";
    }
}

/// <summary>
/// 读取当前夹的内容快照，并比较两份快照。
/// 纯空白与换行符种类的差别在归一化时消掉；段落之间空行数量变化仍视为正文有改动。
/// </summary>
public static class NoteRevisionBaselineCapture
{
    /// <summary>
    /// 按当前正文、配图和配置里的标题、导语拍照。
    /// </summary>
    public static NoteRevisionBaseline Capture(string folderFullPath, NoteConfig config)
    {
        var bodyPath = Path.Combine(folderFullPath, NoteRules.BodyFileName);
        var body = File.Exists(bodyPath) ? File.ReadAllText(bodyPath) : "";
        return new NoteRevisionBaseline
        {
            Title = (config.Title ?? "").Trim(),
            Lead = (config.Summary ?? "").Trim(),
            Body = NoteRevisionText.Normalize(body),
            Images = ListImages(folderFullPath)
        };
    }

    /// <summary>
    /// 标题没有变，或这次保存已经用手改修订记下了标题变更时，把快照里的展示标题对齐。
    /// 还没记下来的标题变化留着，发布时仍会生成一条说明。导语和正文快照不动。
    /// </summary>
    public static void AcknowledgeDisplayedTitle(NoteConfig config, bool recordedByHand)
    {
        if (config.RevisionBaseline == null)
        {
            return;
        }

        var titleNow = (config.Title ?? "").Trim();
        if (!recordedByHand
            && !string.Equals(config.RevisionBaseline.Title ?? "", titleNow, StringComparison.Ordinal))
        {
            return;
        }

        config.RevisionBaseline.Title = titleNow;
    }

    /// <summary>
    /// 比较上一版快照和当前快照。
    /// </summary>
    public static NoteRevisionDiff Compare(NoteRevisionBaseline? previous, NoteRevisionBaseline? current)
    {
        var before = previous ?? new NoteRevisionBaseline();
        var after = current ?? new NoteRevisionBaseline();
        var previousBody = NoteRevisionText.Normalize(before.Body);
        var currentBody = NoteRevisionText.Normalize(after.Body);
        var previousHeadings = NoteRevisionText.Headings(previousBody);
        var currentHeadings = NoteRevisionText.Headings(currentBody);
        var addedHeadings = Extras(currentHeadings, previousHeadings);
        var removedHeadings = Extras(previousHeadings, currentHeadings);
        var previousImages = Sorted(before.Images);
        var currentImages = Sorted(after.Images);
        var addedImages = Extras(currentImages, previousImages);
        var removedImages = Extras(previousImages, currentImages);
        var previousRefs = Sorted(NoteRules.ListImageTargets(previousBody));
        var currentRefs = Sorted(NoteRules.ListImageTargets(currentBody));
        return new NoteRevisionDiff
        {
            TitleChanged = !string.Equals((before.Title ?? "").Trim(), (after.Title ?? "").Trim(), StringComparison.Ordinal),
            PreviousTitle = (before.Title ?? "").Trim(),
            CurrentTitle = (after.Title ?? "").Trim(),
            LeadChanged = !string.Equals((before.Lead ?? "").Trim(), (after.Lead ?? "").Trim(), StringComparison.Ordinal),
            PreviousLead = (before.Lead ?? "").Trim(),
            CurrentLead = (after.Lead ?? "").Trim(),
            ProseChanged = !string.Equals(NoteRevisionText.Prose(previousBody), NoteRevisionText.Prose(currentBody), StringComparison.Ordinal),
            HeadingOrderChanged = addedHeadings.Count == 0
                && removedHeadings.Count == 0
                && !SameSequence(previousHeadings, currentHeadings),
            AddedHeadings = addedHeadings,
            RemovedHeadings = removedHeadings,
            AddedImages = addedImages,
            RemovedImages = removedImages,
            ImageReferencesChanged = !SameSequence(previousRefs, currentRefs),
            PreviousBody = previousBody,
            CurrentBody = currentBody
        };
    }

    private static List<string> ListImages(string folderFullPath)
    {
        var list = new List<string>();
        if (!Directory.Exists(folderFullPath))
        {
            return list;
        }

        foreach (var file in Directory.EnumerateFiles(folderFullPath, "*", SearchOption.AllDirectories))
        {
            var rel = JsonUtil.ToRel(Path.GetRelativePath(folderFullPath, file));
            if (MediaPathRules.IsNoteAuxiliaryRel(rel))
            {
                continue;
            }

            if (!NoteRules.StoredImageExtSet.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            list.Add(rel);
        }

        list.Sort(StringComparer.Ordinal);
        return list;
    }

    private static List<string> Sorted(IReadOnlyList<string>? items)
    {
        var list = new List<string>();
        if (items == null)
        {
            return list;
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            list.Add(item.Trim().Replace('\\', '/'));
        }

        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>
    /// 按出现顺序保留 <paramref name="source"/> 里多出来的项。
    /// </summary>
    private static List<string> Extras(IReadOnlyList<string> source, IReadOnlyList<string> other)
    {
        var bag = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in other)
        {
            bag.TryGetValue(item, out var count);
            bag[item] = count + 1;
        }

        var result = new List<string>();
        foreach (var item in source)
        {
            if (bag.TryGetValue(item, out var count) && count > 0)
            {
                bag[item] = count - 1;
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static bool SameSequence(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// 正文归一化。供快照和差异共用，避免保存路径和测试各写一套。
/// </summary>
public static class NoteRevisionText
{
    private static readonly Regex MarkdownImageLineRegex = new(
        @"^!\[[^\]]*\]\([^)\s]+\)$",
        RegexOptions.Compiled);

    /// <summary>
    /// 统一换行，去掉行尾空白和文末空行。行内文字和空行数量保持原样。
    /// </summary>
    public static string Normalize(string? text)
    {
        var body = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = body.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }

        return string.Join('\n', lines).Trim();
    }

    /// <summary>
    /// 代码块之外的 ATX 标题。要求井号后有空白，与正文一级标题的读法一致。
    /// </summary>
    public static List<string> Headings(string normalized)
    {
        var headings = new List<string>();
        var fence = false;
        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fence = !fence;
                continue;
            }

            if (fence || !TryHeading(line, out var text))
            {
                continue;
            }

            headings.Add(text);
        }

        return headings;
    }

    /// <summary>
    /// 去掉标题行和单独成行的 Markdown 图片后剩下的文字。用来判断除结构以外正文有没有改。
    /// </summary>
    public static string Prose(string normalized)
    {
        var kept = new List<string>();
        var fence = false;
        foreach (var raw in normalized.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fence = !fence;
                kept.Add(raw.TrimEnd());
                continue;
            }

            if (!fence && (TryHeading(line, out _) || MarkdownImageLineRegex.IsMatch(line)))
            {
                continue;
            }

            kept.Add(raw.TrimEnd());
        }

        return Normalize(string.Join('\n', kept));
    }

    private static bool TryHeading(string line, out string text)
    {
        text = "";
        var marks = 0;
        while (marks < line.Length && marks < 6 && line[marks] == '#')
        {
            marks++;
        }

        if (marks == 0 || marks >= line.Length || !char.IsWhiteSpace(line[marks]))
        {
            return false;
        }

        text = line[(marks + 1)..].Trim();
        return text.Length > 0;
    }
}
