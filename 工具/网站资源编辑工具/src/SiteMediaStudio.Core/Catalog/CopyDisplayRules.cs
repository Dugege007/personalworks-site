namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 与访客 <c>copyDisplay.ts</c> 同一套未填显隐，供预演复述。
/// </summary>
public static class CopyDisplayRules
{
    /// <summary>
    /// 资源名称：已填用自定义名，否则用原来的 <c>label</c>。
    /// </summary>
    public static string ResolveMediaDisplayName(string? displayName, string? label)
    {
        return CopyText.IsFilled(displayName) ? displayName!.Trim() : label ?? "";
    }

    /// <summary>
    /// 资源导语：已填用资源描述，否则回退项目描述，都未填则空。
    /// </summary>
    public static string ResolveMediaDescription(string? mediaDescription, string? projectDescription)
    {
        if (CopyText.IsFilled(mediaDescription))
        {
            return mediaDescription!.Trim();
        }

        return CopyText.IsFilled(projectDescription) ? projectDescription!.Trim() : "";
    }

    /// <summary>
    /// 预演里写明网页将走的显隐。
    /// </summary>
    public static string DescribeVisibility(IntentItem item, string? projectDescription)
    {
        var target = item.Target ?? "";
        if (string.Equals(target, "channel", StringComparison.Ordinal))
        {
            return CopyText.IsFilled(item.Description)
                ? "网页将渲染栏目导语。"
                : "栏目导语未填，网页不渲染该段。";
        }

        if (string.Equals(target, "work", StringComparison.Ordinal))
        {
            return CopyText.IsFilled(item.Description)
                ? "网页将渲染项目导语。"
                : "项目导语未填，网页不渲染该段。";
        }

        var name = ResolveMediaDisplayName(item.Title, item.LabelBefore);
        var desc = ResolveMediaDescription(item.Description, projectDescription);
        var nameLine = CopyText.IsFilled(item.Title)
            ? "资源名用自定义名「" + name + "」。"
            : "资源名未填，网页仍用原来的 label。";
        var descLine = CopyText.IsFilled(item.Description)
            ? "灯箱导语用资源描述。"
            : CopyText.IsFilled(projectDescription)
                ? "资源描述未填，灯箱导语回退项目描述。"
                : "资源与项目描述都未填，网页不显示该导语。";
        return nameLine + descLine;
    }
}
