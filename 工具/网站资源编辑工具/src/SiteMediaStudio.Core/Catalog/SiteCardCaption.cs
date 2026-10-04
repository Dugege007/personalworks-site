using System.IO;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 站点卡片第一行名称：已填自定义名则用它，未填用原名；都不带后缀。
/// </summary>
public static class SiteCardCaption
{
    /// <summary>
    /// 带序号的整行题名。
    /// </summary>
    public static string Format(SiteItem site)
    {
        return $"({site.Index + 1}/{site.Total}) {Name(site)}";
    }

    /// <summary>
    /// 名称段：已填 <c>displayName</c> 用自定义名，否则用原名去扩展名。
    /// </summary>
    public static string Name(SiteItem site)
    {
        if (CopyText.IsFilled(site.DisplayName))
        {
            return site.DisplayName!.Trim();
        }

        return OriginalStem(site);
    }

    /// <summary>
    /// 原片主文件名（去扩展名）；无原片时用 label，再退到对象主文件名。
    /// </summary>
    public static string OriginalStem(SiteItem site)
    {
        var sourceStem = ReadMediaStem(site.SourceStageRel);
        if (sourceStem != null)
        {
            return sourceStem;
        }

        var stageStem = ReadMediaStem(site.StageRel);
        if (stageStem != null)
        {
            return stageStem;
        }

        if (!string.IsNullOrWhiteSpace(site.Label))
        {
            return StripMediaExtension(site.Label);
        }

        return ReadMediaStem(site.ObjectKey) ?? "";
    }

    /// <summary>
    /// 路径最后一段若为媒体文件则返回去扩展名后的主名，成片目录不算原名。
    /// </summary>
    private static string? ReadMediaStem(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || MediaPathRules.IsDerivedStageRel(path))
        {
            return null;
        }

        var name = Path.GetFileName(JsonUtil.ToRel(path).Replace('/', Path.DirectorySeparatorChar));
        var extension = Path.GetExtension(name);
        if (!MediaPathRules.IsMediaExtension(extension))
        {
            return null;
        }

        return name[..^extension.Length];
    }

    /// <summary>
    /// 若文本末尾是媒体扩展名则去掉，供 label 已带后缀时使用。
    /// </summary>
    private static string StripMediaExtension(string text)
    {
        var extension = Path.GetExtension(text);
        return MediaPathRules.IsMediaExtension(extension)
            ? text[..^extension.Length]
            : text;
    }
}
