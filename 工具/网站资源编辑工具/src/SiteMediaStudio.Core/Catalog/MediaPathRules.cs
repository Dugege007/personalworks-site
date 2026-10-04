namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 投放箱扫描与对象键的共用规则。
/// </summary>
public static class MediaPathRules
{
    public static readonly HashSet<string> ImageExtSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
    };

    public static readonly HashSet<string> VideoExtSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".webm", ".mkv", ".avi", ".wmv", ".mpeg", ".mpg"
    };

    /// <summary>
    /// 音频扩展名，仅供执行队列分类；本轮不进编目扫描。
    /// </summary>
    public static readonly HashSet<string> AudioExtSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma"
    };

    public static readonly HashSet<string> SkipNameSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gitkeep",
        ".ds_store",
        "thumbs.db",
        "desktop.ini",
        "readme.md",
        "术语.txt",
        "profile.json"
    };

    /// <summary>
    /// 发布包目录名，与 <c>sitemedia</c> 的 <c>PACK_DIR_NAMES</c> 同一集合。
    /// </summary>
    public static readonly HashSet<string> PackDirNameSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "webgl",
        "build"
    };

    /// <summary>
    /// 中转站游戏包模板夹，不入库、不进未登记。
    /// </summary>
    public const string GamePackTemplateFolder = "_id";

    /// <summary>
    /// 判断相对路径是否落在 stock 常驻位。
    /// </summary>
    public static bool IsStock(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return false;
        }

        var normalized = JsonUtil.ToRel(relative);
        return normalized.Contains("/stock/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("stock/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/stock", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否作为可预览图像或视频收入编目。
    /// </summary>
    public static bool IsCatalogFile(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var name = Path.GetFileName(fullPath);
        if (SkipNameSet.Contains(name) || IsDerivedStageRel(fullPath) || IsInsidePackDirectory(fullPath))
        {
            return false;
        }

        var ext = Path.GetExtension(fullPath);
        return ImageExtSet.Contains(ext) || VideoExtSet.Contains(ext);
    }

    /// <summary>
    /// 文件夹名是否为发布包目录。
    /// </summary>
    public static bool IsPackDirName(string? folderName)
    {
        return !string.IsNullOrWhiteSpace(folderName) && PackDirNameSet.Contains(folderName);
    }

    /// <summary>
    /// 路径最后一段是否为发布包目录。
    /// </summary>
    public static bool IsPackDirectory(string? pathOrRel)
    {
        if (string.IsNullOrWhiteSpace(pathOrRel))
        {
            return false;
        }

        var rel = JsonUtil.ToRel(pathOrRel).TrimEnd('/');
        var slash = rel.LastIndexOf('/');
        var name = slash < 0 ? rel : rel[(slash + 1)..];
        return IsPackDirName(name);
    }

    /// <summary>
    /// 路径是否落在发布包目录内部（不含包目录本身）。
    /// </summary>
    public static bool IsInsidePackDirectory(string? pathOrRel)
    {
        if (string.IsNullOrWhiteSpace(pathOrRel))
        {
            return false;
        }

        var rel = JsonUtil.ToRel(pathOrRel);
        var partList = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < partList.Length - 1; i++)
        {
            if (IsPackDirName(partList[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 是否为游戏包模板夹 <c>_id</c>。
    /// </summary>
    public static bool IsGamePackTemplate(string? folderId)
    {
        return string.Equals(folderId, GamePackTemplateFolder, StringComparison.Ordinal);
    }

    /// <summary>
    /// 是否为压码输出目录，不当作独立投放箱条目。
    /// </summary>
    public static bool IsDerivedStageRel(string? pathOrRel)
    {
        if (string.IsNullOrWhiteSpace(pathOrRel))
        {
            return false;
        }

        var normalized = JsonUtil.ToRel(pathOrRel);
        return normalized.Contains("/.site-ready/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".site-ready/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/.site-ready", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 路径或文件名是否为编目内的视频。
    /// </summary>
    public static bool IsVideoFile(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return false;
        }

        return VideoExtSet.Contains(Path.GetExtension(pathOrName));
    }

    /// <summary>
    /// 路径或文件名是否为预留音频槽；不表示已编目。
    /// </summary>
    public static bool IsAudioFile(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return false;
        }

        return AudioExtSet.Contains(Path.GetExtension(pathOrName));
    }

    /// <summary>
    /// 扩展名是否为编目内的图像或视频。
    /// </summary>
    public static bool IsMediaExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension)
            && (ImageExtSet.Contains(extension) || VideoExtSet.Contains(extension));
    }

    /// <summary>
    /// 可用宽度能放下整名、或尚未量出宽度时，不必把扩展名钉到右侧。
    /// </summary>
    public static bool FitsWholeCaption(double availableWidth, double fullTextWidth)
    {
        return availableWidth <= 0
            || double.IsInfinity(availableWidth)
            || fullTextWidth <= availableWidth;
    }

    /// <summary>
    /// 把显示名拆成可省略的主体与始终保留的媒体扩展名；名称本身无扩展名时回退到路径。
    /// </summary>
    public static void SplitStickyExtension(string? text, string? fallbackPath, out string stem, out string extension)
    {
        var source = text ?? "";
        if (TryTakeMediaExtension(source, out stem, out extension))
        {
            return;
        }

        if (TryTakeMediaExtension(Path.GetFileName(fallbackPath ?? ""), out _, out extension))
        {
            stem = source;
            return;
        }

        stem = source;
        extension = "";
    }

    /// <summary>
    /// 文本末尾是否带可保留的媒体扩展名。
    /// </summary>
    private static bool TryTakeMediaExtension(string text, out string stem, out string extension)
    {
        extension = Path.GetExtension(text);
        stem = text;
        if (!IsMediaExtension(extension))
        {
            extension = "";
            return false;
        }

        stem = text[..^extension.Length];
        return true;
    }
}
