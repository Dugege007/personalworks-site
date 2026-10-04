namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 文案未填判定与输入框回填。
/// </summary>
public static class CopyText
{
    /// <summary>
    /// 历史占位句，与空串同等视为未填。
    /// </summary>
    public const string Placeholder = "待填写描述";

    /// <summary>
    /// 去空白后有字，且不是占位句。
    /// </summary>
    public static bool IsFilled(string? text)
    {
        var trimmed = (text ?? "").Trim();
        return trimmed.Length > 0
            && !string.Equals(trimmed, Placeholder, StringComparison.Ordinal);
    }

    /// <summary>
    /// 装载到输入框：未填显示空串，已填显示原文。
    /// </summary>
    public static string ForEditor(string? published)
    {
        return IsFilled(published) ? published!.Trim() : "";
    }
}
