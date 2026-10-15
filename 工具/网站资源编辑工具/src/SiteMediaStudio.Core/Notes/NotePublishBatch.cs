namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一篇待上页笔记的上页前检查。
/// </summary>
public sealed class NotePublishCheck
{
    /// <summary>
    /// 提示里使用的笔记名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 标记能否对应到一篇笔记。对不上时不再检查正文和配图。
    /// </summary>
    public bool Recognized { get; init; } = true;

    /// <summary>
    /// 是否标记了这篇的正文。只标记配图不能上页。
    /// </summary>
    public bool HasBodyMark { get; init; }

    /// <summary>
    /// 缺失配图或引用超出笔记文件夹时的说明。可以上页时为空。
    /// </summary>
    public string ImageBlock { get; init; } = "";
}

/// <summary>
/// 批量上页前先汇总每一篇的检查结果。返回非空说明时，一篇都不能上页。
/// </summary>
public static class NotePublishBatch
{
    /// <summary>
    /// 列出全部不能上页的笔记和原因。全部可以上页时返回空字符串。
    /// </summary>
    public static string BlockReason(IReadOnlyList<NotePublishCheck> checkList)
    {
        if (checkList == null)
        {
            throw new ArgumentNullException(nameof(checkList));
        }

        var lineList = new List<string>();
        foreach (var check in checkList)
        {
            var name = string.IsNullOrWhiteSpace(check.Name) ? "未识别的笔记" : check.Name;
            if (!check.Recognized)
            {
                lineList.Add("「" + name + "」有标记无法对应到笔记文件夹，不能上页。");
                continue;
            }

            if (!check.HasBodyMark)
            {
                lineList.Add("「" + name + "」须标记正文.md 才能上页这篇心得，不能只标记配图。");
            }

            if (!string.IsNullOrWhiteSpace(check.ImageBlock))
            {
                lineList.Add("「" + name + "」" + check.ImageBlock);
            }
        }

        return string.Join(Environment.NewLine, lineList);
    }
}
