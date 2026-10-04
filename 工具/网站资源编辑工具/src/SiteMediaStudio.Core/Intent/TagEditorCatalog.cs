namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 标签组件里的一类词。类型来自冻结表，自由标签来自栏目词库与内容层。
/// </summary>
public enum TagEditorKind
{
    /// <summary>
    /// 冻结类型键。
    /// </summary>
    Type,

    /// <summary>
    /// 自由标签。
    /// </summary>
    Free
}

/// <summary>
/// 一枚已选芯片或一条下拉预设。
/// </summary>
public sealed class TagEditorItem
{
    /// <summary>
    /// 类型用冻结键，自由标签用词本身。
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// 界面上的文字。
    /// </summary>
    public string Label { get; init; } = "";

    /// <summary>
    /// 类型或自由标签。
    /// </summary>
    public TagEditorKind Kind { get; init; }

    /// <summary>
    /// 该栏目当前使用次数。类型按帧计，自由标签按帧加作品级。
    /// </summary>
    public int UseCount { get; init; }

    /// <summary>
    /// 下拉项可否删除预设。类型为假。
    /// </summary>
    public bool AllowPresetDelete { get; init; }

    /// <summary>
    /// 项目汇总里当前点中的那一枚。资源编辑芯片不用。
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// 一次标签写入的目标。对象键为空时写作品级自由标签。
/// </summary>
public sealed class TagFrameWrite
{
    /// <summary>
    /// 作品编号。
    /// </summary>
    public required string WorkId { get; init; }

    /// <summary>
    /// 帧对象键。空则写作品级。
    /// </summary>
    public string? ObjectKey { get; init; }

    /// <summary>
    /// 写入后的类型键。作品级忽略。
    /// </summary>
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 写入后的自由标签。
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 删除预设前，某个作品还带着该词的情况。
/// </summary>
public sealed class TagUsageHit
{
    /// <summary>
    /// 作品编号。
    /// </summary>
    public required string WorkId { get; init; }

    /// <summary>
    /// 作品题名。
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 带该词的帧数。
    /// </summary>
    public int FrameCount { get; init; }

    /// <summary>
    /// 作品级标签是否带着该词。
    /// </summary>
    public bool OnWork { get; init; }
}

/// <summary>
/// 标签组件的候选、提交与按帧改写。不读写磁盘。
/// </summary>
public static class TagEditorCatalog
{
    /// <summary>
    /// 生成下拉预设。类型在前且不可删；自由标签按使用次数从多到少。
    /// </summary>
    public static List<TagEditorItem> BuildSuggestions(
        IEnumerable<WorkCatalogItem> channelWorkList,
        IEnumerable<string>? vocabList,
        bool includeTypes)
    {
        var works = channelWorkList.ToList();
        var freeDict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var raw in vocabList ?? Array.Empty<string>())
        {
            var tag = (raw ?? "").Trim();
            if (tag.Length == 0 || !IsFreeTag(tag))
            {
                continue;
            }

            freeDict.TryAdd(tag, 0);
        }

        foreach (var work in works)
        {
            CountFree(freeDict, work.Tags);
            foreach (var media in work.Media)
            {
                if (string.IsNullOrWhiteSpace(media.Src))
                {
                    continue;
                }

                CountFree(freeDict, media.Tags);
            }
        }

        var list = new List<TagEditorItem>();
        if (includeTypes)
        {
            foreach (var facet in PhotoFactRules.PrimaryFacetList)
            {
                if (facet.IsType)
                {
                    var theme = PhotoFactRules.ThemeList.First(item => item.Key == facet.Id);
                    list.Add(new TagEditorItem
                    {
                        Id = theme.Key,
                        Label = theme.Zh,
                        Kind = TagEditorKind.Type,
                        UseCount = CountTheme(works, theme.Key),
                        AllowPresetDelete = false
                    });
                    continue;
                }

                list.Add(new TagEditorItem
                {
                    Id = facet.Id,
                    Label = facet.Id,
                    Kind = TagEditorKind.Free,
                    UseCount = freeDict.GetValueOrDefault(facet.Id),
                    AllowPresetDelete = false
                });
            }
        }

        foreach (var pair in freeDict
            .Where(item => !PhotoFactRules.IsDefaultTag(item.Key) || !includeTypes)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.Ordinal))
        {
            list.Add(new TagEditorItem
            {
                Id = pair.Key,
                Label = pair.Key,
                Kind = TagEditorKind.Free,
                UseCount = pair.Value,
                AllowPresetDelete = true
            });
        }

        return list;
    }

    /// <summary>
    /// 按显示文字的包含关系过滤。不看技术键。空查询返回原顺序的副本。
    /// </summary>
    public static List<TagEditorItem> Filter(IReadOnlyList<TagEditorItem> source, string? query)
    {
        var text = (query ?? "").Trim();
        if (text.Length == 0)
        {
            return source.ToList();
        }

        return source.Where(item =>
                item.Label.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// 把输入收成已有预设或一条新的自由标签。空文本返回假且无错误。
    /// </summary>
    public static bool TryCommitText(
        string? text,
        IReadOnlyList<TagEditorItem> suggestions,
        out TagEditorItem? existing,
        out string? newFreeLabel,
        out string? error)
    {
        existing = null;
        newFreeLabel = null;
        error = null;
        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var hit = suggestions.FirstOrDefault(item =>
            string.Equals(item.Label, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Id, trimmed, StringComparison.OrdinalIgnoreCase));
        if (hit != null)
        {
            existing = hit;
            return true;
        }

        if (!PhotoFactRules.TryNormalizeTags(new[] { trimmed }, out _, out error))
        {
            return false;
        }

        if (PhotoFactRules.ThemeList.Any(theme => string.Equals(theme.Zh, trimmed, StringComparison.Ordinal)))
        {
            error = "类型不进自由标签：" + trimmed;
            return false;
        }

        newFreeLabel = trimmed;
        return true;
    }

    /// <summary>
    /// 在现有集合上加上一枚。已有则原样返回。
    /// </summary>
    public static (List<string> Themes, List<string> Tags) WithAdded(
        IEnumerable<string>? themes,
        IEnumerable<string>? tags,
        TagEditorItem item)
    {
        var themeList = PhotoFactRules.NormalizeThemes(themes);
        PhotoFactRules.TryNormalizeTags(tags, out var tagList, out _);
        if (item.Id == PhotoFactRules.UntaggedId)
        {
            return (themeList, tagList);
        }

        if (item.Kind == TagEditorKind.Type)
        {
            if (!themeList.Contains(item.Id, StringComparer.Ordinal))
            {
                themeList.Add(item.Id);
                themeList = PhotoFactRules.NormalizeThemes(themeList);
            }
        }
        else if (!tagList.Contains(item.Label, StringComparer.Ordinal))
        {
            tagList.Add(item.Label);
        }

        return (themeList, tagList);
    }

    /// <summary>
    /// 在现有集合上去掉一枚。
    /// </summary>
    public static (List<string> Themes, List<string> Tags) WithRemoved(
        IEnumerable<string>? themes,
        IEnumerable<string>? tags,
        TagEditorItem item)
    {
        var themeList = PhotoFactRules.NormalizeThemes(themes);
        PhotoFactRules.TryNormalizeTags(tags, out var tagList, out _);
        if (item.Kind == TagEditorKind.Type)
        {
            themeList = themeList.Where(key => !string.Equals(key, item.Id, StringComparison.Ordinal)).ToList();
        }
        else
        {
            tagList = tagList.Where(tag => !string.Equals(tag, item.Label, StringComparison.Ordinal)).ToList();
        }

        return (themeList, tagList);
    }

    /// <summary>
    /// 多帧都带着的词才显示。顺序跟预设列表一致。
    /// </summary>
    public static List<TagEditorItem> ChipsForFrames(
        IReadOnlyList<TagEditorItem> suggestions,
        IReadOnlyList<(IReadOnlyList<string> Themes, IReadOnlyList<string> Tags)> frames)
    {
        if (frames.Count == 0)
        {
            return new List<TagEditorItem>();
        }

        HashSet<string>? sharedThemes = null;
        HashSet<string>? sharedTags = null;
        foreach (var frame in frames)
        {
            var themeSet = new HashSet<string>(PhotoFactRules.NormalizeThemes(frame.Themes), StringComparer.Ordinal);
            var tagSet = new HashSet<string>(
                frame.Tags.Select(tag => (tag ?? "").Trim()).Where(tag => tag.Length > 0),
                StringComparer.Ordinal);
            if (sharedThemes == null || sharedTags == null)
            {
                sharedThemes = themeSet;
                sharedTags = tagSet;
                continue;
            }

            sharedThemes.IntersectWith(themeSet);
            sharedTags.IntersectWith(tagSet);
        }

        var chips = new List<TagEditorItem>();
        foreach (var item in suggestions)
        {
            if (item.Kind == TagEditorKind.Type && sharedThemes!.Contains(item.Id))
            {
                chips.Add(item);
            }
            else if (item.Kind == TagEditorKind.Free && sharedTags!.Contains(item.Label))
            {
                chips.Add(item);
            }
        }

        foreach (var tag in sharedTags!)
        {
            if (chips.Any(item => item.Kind == TagEditorKind.Free && string.Equals(item.Label, tag, StringComparison.Ordinal)))
            {
                continue;
            }

            chips.Add(new TagEditorItem
            {
                Id = tag,
                Label = tag,
                Kind = TagEditorKind.Free,
                AllowPresetDelete = true
            });
        }

        return chips;
    }

    /// <summary>
    /// 该项目已上页资源用过的标签。次数是带着该词的资源数。
    /// 类型按冻结顺序，自由标签按次数从多到少、再按文字。作品级词不计。
    /// </summary>
    public static List<TagEditorItem> SummarizeWork(WorkCatalogItem work)
    {
        return SummarizeWorks(new[] { work });
    }

    /// <summary>
    /// 多部作品已上页资源用过的标签。次数跨这些作品相加。
    /// 没有未打标签的已上页资源时，不出现「无标签」。
    /// </summary>
    public static List<TagEditorItem> SummarizeWorks(IEnumerable<WorkCatalogItem> works)
    {
        var themeCountDict = new Dictionary<string, int>(StringComparer.Ordinal);
        var tagCountDict = new Dictionary<string, int>(StringComparer.Ordinal);
        var untagged = 0;
        foreach (var work in works)
        {
            CountWorkTags(work, themeCountDict, tagCountDict);
            foreach (var media in work.Media)
            {
                if (string.IsNullOrWhiteSpace(media.Src))
                {
                    continue;
                }

                if (PhotoFactRules.IsUntagged(media.Themes, media.Tags))
                {
                    untagged++;
                }
            }
        }

        var chips = ChipsFromCounts(themeCountDict, tagCountDict);
        if (untagged == 0)
        {
            return chips;
        }

        chips.Add(new TagEditorItem
        {
            Id = PhotoFactRules.UntaggedId,
            Label = "无标签",
            Kind = TagEditorKind.Free,
            UseCount = untagged,
            AllowPresetDelete = false
        });
        return chips;
    }

    /// <summary>
    /// 把一部作品的已上页资源计入类型与自由标签次数。
    /// </summary>
    private static void CountWorkTags(
        WorkCatalogItem work,
        Dictionary<string, int> themeCountDict,
        Dictionary<string, int> tagCountDict)
    {
        foreach (var media in work.Media)
        {
            if (string.IsNullOrWhiteSpace(media.Src))
            {
                continue;
            }

            foreach (var key in PhotoFactRules.NormalizeThemes(media.Themes))
            {
                themeCountDict[key] = themeCountDict.GetValueOrDefault(key) + 1;
            }

            var seenSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in media.Tags)
            {
                var tag = (raw ?? "").Trim();
                if (tag.Length == 0 || !seenSet.Add(tag) || !IsFreeTag(tag))
                {
                    continue;
                }

                tagCountDict[tag] = tagCountDict.GetValueOrDefault(tag) + 1;
            }
        }
    }

    /// <summary>
    /// 按冻结类型顺序和自由标签次数排成芯片。
    /// </summary>
    private static List<TagEditorItem> ChipsFromCounts(
        Dictionary<string, int> themeCountDict,
        Dictionary<string, int> tagCountDict)
    {
        var chips = new List<TagEditorItem>();
        foreach (var theme in PhotoFactRules.ThemeList)
        {
            if (!themeCountDict.TryGetValue(theme.Key, out var count) || count == 0)
            {
                continue;
            }

            chips.Add(new TagEditorItem
            {
                Id = theme.Key,
                Label = theme.Zh,
                Kind = TagEditorKind.Type,
                UseCount = count
            });
        }

        foreach (var pair in tagCountDict
                     .OrderByDescending(item => item.Value)
                     .ThenBy(item => item.Key, StringComparer.Ordinal))
        {
            chips.Add(new TagEditorItem
            {
                Id = pair.Key,
                Label = pair.Key,
                Kind = TagEditorKind.Free,
                UseCount = pair.Value
            });
        }

        return chips;
    }

    /// <summary>
    /// 该作品里带着这枚标签、且已有对象键的资源。
    /// </summary>
    public static HashSet<string> ObjectKeysWithTag(WorkCatalogItem work, TagEditorItem item)
    {
        return ObjectKeysWithTag(new[] { work }, item);
    }

    /// <summary>
    /// 这些作品里带着这枚标签、且已有对象键的资源。
    /// </summary>
    public static HashSet<string> ObjectKeysWithTag(IEnumerable<WorkCatalogItem> works, TagEditorItem item)
    {
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in works)
        {
            CollectObjectKeys(work, item, keySet);
        }

        return keySet;
    }

    /// <summary>
    /// 把一部作品里命中的对象键并入集合。
    /// </summary>
    private static void CollectObjectKeys(WorkCatalogItem work, TagEditorItem item, HashSet<string> keySet)
    {
        foreach (var media in work.Media)
        {
            if (string.IsNullOrWhiteSpace(media.Src))
            {
                continue;
            }

            if (item.Id == PhotoFactRules.UntaggedId)
            {
                if (PhotoFactRules.IsUntagged(media.Themes, media.Tags))
                {
                    keySet.Add(JsonUtil.ToRel(media.Src));
                }

                continue;
            }

            if (FrameHas(media.Themes, media.Tags, item))
            {
                keySet.Add(JsonUtil.ToRel(media.Src));
            }
        }
    }

    /// <summary>
    /// 当前帧是否已经带着这枚。
    /// </summary>
    public static bool FrameHas(IReadOnlyList<string> themes, IReadOnlyList<string> tags, TagEditorItem item)
    {
        if (item.Kind == TagEditorKind.Type)
        {
            return PhotoFactRules.NormalizeThemes(themes).Contains(item.Id, StringComparer.Ordinal);
        }

        return tags.Any(tag => string.Equals((tag ?? "").Trim(), item.Label, StringComparison.Ordinal));
    }

    /// <summary>
    /// 按对象键读取这一张的类型与自由标签。找不到则空。
    /// </summary>
    public static (IReadOnlyList<string> Themes, IReadOnlyList<string> Tags) ReadFrame(WorkCatalogItem work, string objectKey)
    {
        var rel = JsonUtil.ToRel(objectKey);
        var media = work.Media.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Src)
            && string.Equals(JsonUtil.ToRel(item.Src), rel, StringComparison.Ordinal));
        return (media?.Themes ?? Array.Empty<string>(), media?.Tags ?? Array.Empty<string>());
    }

    /// <summary>
    /// 列出本栏目还带着该自由标签的作品。
    /// </summary>
    public static List<TagUsageHit> FindFreeTagUsages(IEnumerable<WorkCatalogItem> channelWorkList, string label)
    {
        var hits = new List<TagUsageHit>();
        foreach (var work in channelWorkList)
        {
            var onWork = work.Tags.Any(tag => string.Equals(tag, label, StringComparison.Ordinal));
            var frames = work.Media.Count(media =>
                !string.IsNullOrWhiteSpace(media.Src)
                && media.Tags.Any(tag => string.Equals(tag, label, StringComparison.Ordinal)));
            if (!onWork && frames == 0)
            {
                continue;
            }

            hits.Add(new TagUsageHit
            {
                WorkId = work.Id,
                Title = string.IsNullOrWhiteSpace(work.Title) ? work.Id : work.Title,
                FrameCount = frames,
                OnWork = onWork
            });
        }

        return hits;
    }

    /// <summary>
    /// 删除预设前的确认文案。
    /// </summary>
    public static string DescribePresetDelete(string label, IReadOnlyList<TagUsageHit> hits)
    {
        if (hits.Count == 0)
        {
            return "没有作品在使用「" + label + "」。将从预设中删除。";
        }

        var lines = hits.Select(hit =>
        {
            var where = hit.OnWork && hit.FrameCount == 0
                ? "作品"
                : hit.OnWork
                    ? "作品，" + hit.FrameCount + " 张"
                    : hit.FrameCount + " 张";
            return hit.Title + "（" + where + "）";
        });
        return "以下作品还在使用「" + label + "」：\n"
            + string.Join("\n", lines)
            + "\n确认后从这些作品去掉，并删除预设。";
    }

    /// <summary>
    /// 从这些作品上去掉一个自由标签，生成写入条目。
    /// </summary>
    public static List<TagFrameWrite> StripFreeTag(IEnumerable<WorkCatalogItem> channelWorkList, string label)
    {
        var writes = new List<TagFrameWrite>();
        foreach (var work in channelWorkList)
        {
            if (work.Tags.Any(tag => string.Equals(tag, label, StringComparison.Ordinal)))
            {
                writes.Add(new TagFrameWrite
                {
                    WorkId = work.Id,
                    Tags = work.Tags.Where(tag => !string.Equals(tag, label, StringComparison.Ordinal)).ToList()
                });
            }

            foreach (var media in work.Media)
            {
                if (string.IsNullOrWhiteSpace(media.Src)
                    || !media.Tags.Any(tag => string.Equals(tag, label, StringComparison.Ordinal)))
                {
                    continue;
                }

                writes.Add(new TagFrameWrite
                {
                    WorkId = work.Id,
                    ObjectKey = JsonUtil.ToRel(media.Src),
                    Themes = PhotoFactRules.NormalizeThemes(media.Themes),
                    Tags = media.Tags.Where(tag => !string.Equals(tag, label, StringComparison.Ordinal)).ToList()
                });
            }
        }

        return writes;
    }

    /// <summary>
    /// 是否可作为自由标签。年份与类型键排除。
    /// </summary>
    private static bool IsFreeTag(string tag)
    {
        return PhotoFactRules.TryNormalizeTags(new[] { tag }, out _, out _);
    }

    /// <summary>
    /// 统计该栏目已上页帧里的类型次数。
    /// </summary>
    private static int CountTheme(IReadOnlyList<WorkCatalogItem> works, string key)
    {
        var count = 0;
        foreach (var work in works)
        {
            foreach (var media in work.Media)
            {
                if (string.IsNullOrWhiteSpace(media.Src))
                {
                    continue;
                }

                if (PhotoFactRules.NormalizeThemes(media.Themes).Contains(key, StringComparer.Ordinal))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 自由标签按出现次数累加。非法词跳过。
    /// </summary>
    private static void CountFree(Dictionary<string, int> freeDict, IReadOnlyList<string> tags)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in tags)
        {
            var tag = (raw ?? "").Trim();
            if (!IsFreeTag(tag) || !seen.Add(tag))
            {
                continue;
            }

            freeDict[tag] = freeDict.TryGetValue(tag, out var count) ? count + 1 : 1;
        }
    }
}
