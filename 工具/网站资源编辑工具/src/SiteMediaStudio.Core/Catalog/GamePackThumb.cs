namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 游戏发布包在编辑网格中的缩略图解析，不改访客选单封面。
/// </summary>
public static class GamePackThumb
{
    private static readonly string[] SiblingIconNameList =
    {
        "logo.webp",
        "icon.webp",
        "logo.png",
        "icon.png"
    };

    /// <summary>
    /// 按作品夹 Logo、包内网页图标、同夹第一张截图的顺序取第一张存在的图。
    /// </summary>
    public static string? Resolve(string? workFolderFullPath, string? packDirFullPath)
    {
        var sibling = FirstExistingFile(workFolderFullPath, SiblingIconNameList);
        if (sibling != null)
        {
            return sibling;
        }

        var packIcon = ResolvePackIcon(packDirFullPath);
        if (packIcon != null)
        {
            return packIcon;
        }

        return FirstCatalogImage(workFolderFullPath);
    }

    /// <summary>
    /// 包内 TemplateData / 根目录常见网页图标。
    /// </summary>
    private static string? ResolvePackIcon(string? packDirFullPath)
    {
        if (string.IsNullOrWhiteSpace(packDirFullPath) || !Directory.Exists(packDirFullPath))
        {
            return null;
        }

        var templateDir = Path.Combine(packDirFullPath, "TemplateData");
        var favicon = Path.Combine(templateDir, "favicon.ico");
        if (File.Exists(favicon))
        {
            return favicon;
        }

        if (Directory.Exists(templateDir))
        {
            var icon = Directory.EnumerateFiles(templateDir, "icon-*.png")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(icon))
            {
                return icon;
            }
        }

        var rootFavicon = Path.Combine(packDirFullPath, "favicon.ico");
        return File.Exists(rootFavicon) ? rootFavicon : null;
    }

    /// <summary>
    /// 作品夹根下第一张编目图像，不含包内文件。
    /// </summary>
    private static string? FirstCatalogImage(string? workFolderFullPath)
    {
        if (string.IsNullOrWhiteSpace(workFolderFullPath) || !Directory.Exists(workFolderFullPath))
        {
            return null;
        }

        return Directory.EnumerateFiles(workFolderFullPath)
            .Where(path => MediaPathRules.IsCatalogFile(path)
                && MediaPathRules.ImageExtSet.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// 在目录下按名单取第一张存在的文件。
    /// </summary>
    private static string? FirstExistingFile(string? folderFullPath, IReadOnlyList<string> nameList)
    {
        if (string.IsNullOrWhiteSpace(folderFullPath) || !Directory.Exists(folderFullPath))
        {
            return null;
        }

        foreach (var name in nameList)
        {
            var path = Path.Combine(folderFullPath, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
