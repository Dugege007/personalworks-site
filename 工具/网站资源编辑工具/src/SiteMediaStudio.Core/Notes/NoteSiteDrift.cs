namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 判断已发布心得的中转站是否还有没写进站点稿的修改。
/// 夹内 <c>revisionBaseline</c> 只在发布上线时前进，用来生成修订摘要，并不代表上次写入 <c>content/notes</c> 的内容。
/// 若只拿它和当前夹比较，更新已经写入站点稿、尚未发布上线时会一直显示未同步。
/// 因此这里复用 <see cref="NoteRevisionBaselineCapture"/> 的归一化与比较，对照物改为从站点稿还原的快照。
/// 更新成功后站点稿与中转站一致，提示会自行消失，不必另记一份脏标记。
/// </summary>
public static class NoteSiteDrift
{
    /// <summary>
    /// 选中已发布篇、且中转站相对站点稿有未写入修改时的提示。
    /// </summary>
    public const string UnsyncedNotice = "本地有未同步到站点的修改";

    /// <summary>
    /// 按索引条目和站点正文判断。未发布、缺夹时返回假，那两种状态另有文案。
    /// 站点正文文件不存在时视为尚未写入站点稿。
    /// </summary>
    public static bool HasUnsyncedSiteChanges(string folderFullPath, NoteEntry? entry, string? siteMarkdown)
    {
        if (entry == null || entry.Draft || string.IsNullOrWhiteSpace(entry.Slug))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(folderFullPath) || !Directory.Exists(folderFullPath))
        {
            return false;
        }

        if (siteMarkdown == null)
        {
            return true;
        }

        var folderName = Path.GetFileName(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var config = LoadConfig(folderFullPath);
        var stagingBody = ReadBody(folderFullPath);
        if (!string.IsNullOrWhiteSpace(config.Slug)
            && !string.Equals(config.Slug.Trim(), entry.Slug.Trim(), StringComparison.Ordinal))
        {
            return true;
        }

        var current = new NoteRevisionBaseline
        {
            Title = ResolveDisplayTitle(folderName, config, stagingBody),
            Lead = (config.Summary ?? "").Trim(),
            Body = StagingPublishShape(stagingBody),
            Images = NoteRevisionBaselineCapture.Capture(folderFullPath, config).Images
        };
        var site = new NoteRevisionBaseline
        {
            Title = (entry.Title ?? "").Trim(),
            // 回填稿常把导语只写在索引里，夹内摘要仍为空。空摘要不算本地新修改。
            Lead = current.Lead.Length == 0 ? "" : (entry.Summary ?? "").Trim(),
            Body = SitePublishShape(siteMarkdown),
            Images = SiteImageList(entry, folderName)
        };
        return NoteRevisionBaselineCapture.Compare(site, current).HasMeaningfulChange;
    }

    /// <summary>
    /// 从工作区里的站点稿目录读取 <c>{slug}.md</c> 再比较。目录或文件缺失视为未写入。
    /// </summary>
    public static bool HasUnsyncedSiteChanges(string folderFullPath, NoteIndexFile? index, string? notesDirectory)
    {
        var folderName = string.IsNullOrWhiteSpace(folderFullPath)
            ? ""
            : Path.GetFileName(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var entry = NotePublishCopy.FindPublished(index, folderName);
        if (entry == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(notesDirectory))
        {
            return true;
        }

        var path = Path.Combine(notesDirectory, entry.Slug.Trim() + ".md");
        if (!File.Exists(path))
        {
            return true;
        }

        return HasUnsyncedSiteChanges(folderFullPath, entry, File.ReadAllText(path));
    }

    /// <summary>
    /// 只读配置。没有文件时按空配置比较，不新建、不回写。
    /// </summary>
    private static NoteConfig LoadConfig(string folderFullPath)
    {
        var path = Path.Combine(folderFullPath, NoteConfigStore.FileName);
        if (!File.Exists(path))
        {
            return new NoteConfig();
        }

        return NoteConfigStore.Load(path);
    }

    /// <summary>
    /// 读取中转站正文。没有文件时按空字符串。
    /// </summary>
    private static string ReadBody(string folderFullPath)
    {
        var path = Path.Combine(folderFullPath, NoteRules.BodyFileName);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    /// <summary>
    /// 与上页相同，去掉第一个一级标题，并把配图地址收成可比较的相对路径。
    /// </summary>
    private static string StagingPublishShape(string markdown)
    {
        var text = StripFrontmatter(markdown);
        text = NoteRules.WithoutFirstHeading(text);
        return CanonicalizeImages(text);
    }

    /// <summary>
    /// 站点稿已经去掉发布时的一级标题。这里只去掉前言并收齐配图地址，避免把后文里的一级标题再删一次。
    /// </summary>
    private static string SitePublishShape(string markdown)
    {
        return CanonicalizeImages(StripFrontmatter(markdown));
    }

    /// <summary>
    /// 去掉正文开头的前言。没有前言时保持原文。
    /// </summary>
    private static string StripFrontmatter(string markdown)
    {
        if (NoteRules.TryReadFrontmatter(markdown, out _, out var body, out _))
        {
            return body;
        }

        return markdown ?? "";
    }

    /// <summary>
    /// 把本地相对路径和站点对象键收成同一形式，扩展名差异不单独算作修改。
    /// </summary>
    private static string CanonicalizeImages(string markdown)
    {
        var text = NoteRules.MarkdownImageRegex.Replace(markdown ?? "", match =>
        {
            var alt = match.Groups[1].Value;
            var raw = match.Groups[2].Value;
            return "![" + alt + "](" + CanonicalImageTarget(raw) + ")";
        });
        return NoteRules.HtmlImageRegex.Replace(text, match =>
            match.Groups[1].Value + CanonicalImageTarget(match.Groups[2].Value) + match.Groups[3].Value);
    }

    /// <summary>
    /// 配图地址的比较形式。网址保持原样；对象键去掉 <c>notes/标识/</c> 和扩展名。
    /// </summary>
    private static string CanonicalImageTarget(string raw)
    {
        var text = (raw ?? "").Trim().Replace('\\', '/');
        if (text.Contains("://", StringComparison.Ordinal))
        {
            return text;
        }

        var cut = text.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0)
        {
            text = text[..cut];
        }

        var extension = Path.GetExtension(text);
        if (extension.Length > 0)
        {
            text = text[..^extension.Length];
        }

        const string objectPrefix = "notes/";
        if (text.StartsWith(objectPrefix, StringComparison.Ordinal))
        {
            var rest = text[objectPrefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                text = rest[(slash + 1)..];
            }
        }

        return text;
    }

    /// <summary>
    /// 展示标题。已保存的主标题按自动拼接规则比较，不写盘。
    /// 还没有主标题时，时间戳夹名在一级标题变化时跟随标题，与保存配置时的规则一致。
    /// </summary>
    private static string ResolveDisplayTitle(string folderName, NoteConfig config, string body)
    {
        if (!string.IsNullOrWhiteSpace(config.MainTitle))
        {
            return NoteRules.ComposeDisplayTitle(config.MainTitle, config.Subtitle);
        }

        if (!NoteRules.IsTimestampFolderName(folderName))
        {
            var parsed = NoteRules.ParseFolderTitles(folderName);
            if (!string.IsNullOrWhiteSpace(parsed.DisplayTitle))
            {
                return parsed.DisplayTitle.Trim();
            }
        }

        var heading = NoteRules.ReadFirstHeading(body).Trim();
        var storedTitle = (config.Title ?? "").Trim();
        if (NoteRules.IsTimestampFolderName(folderName)
            && heading.Length > 0
            && !string.Equals(heading, storedTitle, StringComparison.Ordinal))
        {
            return heading;
        }

        if (storedTitle.Length > 0)
        {
            return storedTitle;
        }

        return heading;
    }

    /// <summary>
    /// 索引里的配图改成相对本夹的路径，便于和当前夹的快照比较。
    /// </summary>
    private static List<string> SiteImageList(NoteEntry entry, string folderName)
    {
        var imageList = new List<string>();
        if (entry.Images == null)
        {
            return imageList;
        }

        foreach (var image in entry.Images)
        {
            if (image == null || string.IsNullOrWhiteSpace(image.StageRel))
            {
                continue;
            }

            var relative = NoteRules.RelativeToNoteFolder(image.StageRel, folderName);
            if (string.IsNullOrWhiteSpace(relative) || MediaPathRules.IsNoteAuxiliaryRel(relative))
            {
                continue;
            }

            imageList.Add(JsonUtil.ToRel(relative));
        }

        return imageList;
    }
}
