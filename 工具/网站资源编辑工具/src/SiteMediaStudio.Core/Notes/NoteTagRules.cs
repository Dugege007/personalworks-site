namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 心得自由标签。栏目键只用 <c>notes</c>，不并入摄影类型与默认词。
/// </summary>
public static class NoteTagRules
{
    /// <summary>
    /// 一期没有默认推荐词。预设只来自本机栏目 <c>notes</c>。
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultTagList = Array.Empty<string>();

    /// <summary>
    /// 已上页心得的检视说明。标签权威在发布索引。
    /// </summary>
    public const string IndexAuthorityHint = "标签写在正文上，记在上页后的索引里，以站点为准。";

    /// <summary>
    /// 尚未写入索引时的检视说明。
    /// </summary>
    public const string PublishFirstHint = "上页后才能写入正文标签。标签在索引里，以站点为准。";

    /// <summary>
    /// 去空白、去重，并拒绝四位年份、摄影类型键、「无标签」与 <c>untagged</c>。
    /// </summary>
    public static bool TryNormalizeTags(IEnumerable<string>? tagList, out List<string> normalized, out string? error)
    {
        return PhotoFactRules.TryNormalizeTags(tagList, out normalized, out error);
    }

    /// <summary>
    /// 心得词库可否记住该词。摄影默认词与类型键不进入栏目 <c>notes</c>。
    /// </summary>
    public static bool CanRemember(string? tag)
    {
        var text = (tag ?? "").Trim();
        return text.Length > 0
            && TryNormalizeTags(new[] { text }, out _, out _)
            && !IsPhotoSuggestionLeak(text);
    }

    /// <summary>
    /// 心得下拉。不带摄影类型，也不带「展馆」「随拍」。次数只统计正文标签。
    /// </summary>
    public static List<TagEditorItem> BuildSuggestions(
        IEnumerable<WorkCatalogItem> noteWorkList,
        IEnumerable<string>? vocabList)
    {
        var safeVocab = (vocabList ?? Array.Empty<string>())
            .Select(item => (item ?? "").Trim())
            .Where(item => item.Length > 0 && !IsPhotoSuggestionLeak(item));
        var bodyOnly = noteWorkList.Select(work => new WorkCatalogItem
        {
            Id = work.Id,
            Channel = NoteRules.ChannelKey,
            Title = work.Title,
            SourceKind = "note",
            Tags = work.Tags,
            Media = Array.Empty<WorkMediaItem>()
        });
        return TagEditorCatalog.BuildSuggestions(bodyOnly, safeVocab, includeTypes: false)
            .Where(item =>
                item.Kind == TagEditorKind.Free
                && !IsPhotoSuggestionLeak(item.Id)
                && !IsPhotoSuggestionLeak(item.Label))
            .ToList();
    }

    /// <summary>
    /// 意图是否指向正文，而不是某一张配图。
    /// </summary>
    public static bool IsBodyTarget(IntentItem item)
    {
        var rel = item.StageRel ?? item.Object ?? "";
        return rel.EndsWith(NoteRules.BodyFileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 正文投放路径。缺夹路径时用夹名接上正文文件名。
    /// </summary>
    public static string BodyStageRel(string? stageFolder, string folderName)
    {
        var folder = JsonUtil.ToRel(stageFolder ?? "");
        if (folder.Length == 0)
        {
            folder = JsonUtil.ToRel(folderName);
        }

        return folder + "/" + NoteRules.BodyFileName;
    }

    /// <summary>
    /// 哪些心得正文还带着该词。不看配图。
    /// </summary>
    public static List<TagUsageHit> FindBodyUsages(IEnumerable<WorkCatalogItem> noteWorkList, string label)
    {
        var hits = new List<TagUsageHit>();
        foreach (var work in noteWorkList)
        {
            if (!work.Tags.Any(tag => string.Equals(tag, label, StringComparison.Ordinal)))
            {
                continue;
            }

            hits.Add(new TagUsageHit
            {
                WorkId = work.Id,
                Title = string.IsNullOrWhiteSpace(work.Title) ? work.Id : work.Title,
                OnWork = true
            });
        }

        return hits;
    }

    /// <summary>
    /// 从正文去掉一个自由标签。不改配图。
    /// </summary>
    public static List<TagFrameWrite> StripBodyTag(IEnumerable<WorkCatalogItem> noteWorkList, string label)
    {
        var writes = new List<TagFrameWrite>();
        foreach (var work in noteWorkList)
        {
            if (!work.Tags.Any(tag => string.Equals(tag, label, StringComparison.Ordinal)))
            {
                continue;
            }

            writes.Add(new TagFrameWrite
            {
                WorkId = work.Id,
                Tags = work.Tags.Where(tag => !string.Equals(tag, label, StringComparison.Ordinal)).ToList()
            });
        }

        return writes;
    }

    /// <summary>
    /// 删除心得预设前的确认文案。
    /// </summary>
    public static string DescribeBodyPresetDelete(string label, IReadOnlyList<TagUsageHit> hits)
    {
        if (hits.Count == 0)
        {
            return "没有心得在使用「" + label + "」。将从预设中删除。";
        }

        return "以下心得的正文还在使用「" + label + "」：\n"
            + string.Join("\n", hits.Select(hit => hit.Title))
            + "\n确认后从这些正文去掉，并删除预设。配图标签不动。";
    }

    /// <summary>
    /// 摄影类型、默认自由标签和筛选字面，不得出现在心得建议里。
    /// </summary>
    private static bool IsPhotoSuggestionLeak(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (PhotoFactRules.IsDefaultTag(text) || text == "无标签" || text == PhotoFactRules.UntaggedId)
        {
            return true;
        }

        return PhotoFactRules.ThemeList.Any(theme =>
            string.Equals(theme.Key, text, StringComparison.Ordinal)
            || string.Equals(theme.Zh, text, StringComparison.Ordinal));
    }
}
