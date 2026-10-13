using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 心得夹、正文与上页预演。不调用压图，也不写发布稿。
/// </summary>
public static class NoteRules
{
    public const string ChannelKey = "notes";
    public const string BodyFileName = "正文.md";
    public const string ReadyDirName = ".site-ready";

    /// <summary>
    /// 原图备份文件夹名。不进入编目，也不参与上页。
    /// </summary>
    public const string OriginalsDirName = ".originals";

    public const string SketchesSlug = "sketches";

    /// <summary>
    /// 上页预演遇到 GIF 配图时返回的说明。
    /// </summary>
    public const string GifPublishRejectedMessage = "GIF 配图暂不支持上页";

    /// <summary>
    /// GIF 拒绝说明后面接具体文件时使用的前缀，含中文冒号。
    /// </summary>
    public const string GifPublishRejectedPrefix = GifPublishRejectedMessage + "：";

    /// <summary>
    /// 夹内有禁止上页的文件时，原因文字的前缀。
    /// </summary>
    public const string BlockedInFolderPrefix = "夹内有不能上页的文件：";

    /// <summary>
    /// 正文引用落到心得夹外时，原因文字的前缀。
    /// </summary>
    public const string OutsideFolderPrefix = "图片落在心得夹外：";

    /// <summary>
    /// 正文引用的图片不存在时，原因文字的前缀。
    /// </summary>
    public const string MissingImagePrefix = "找不到引用的图片：";

    /// <summary>
    /// 正文引用了不允许上页的文件时，原因文字的前缀。
    /// </summary>
    public const string UnpublishableReferencePrefix = "正文引用了不能上页的文件：";

    /// <summary>
    /// 正文引用了这次没有选中的图片时，原因文字的前缀。
    /// </summary>
    public const string NotChosenPrefix = "正文引用了这次没有上页的图片：";

    /// <summary>
    /// 正文里的 Markdown 图片。第 1 组是说明文字，第 2 组是地址。
    /// 读取引用和上页改写共用这一条，避免两处规则各写一遍。
    /// </summary>
    public static readonly Regex MarkdownImageRegex = new(
        @"!\[([^\]]*)\]\(([^)\s]+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// 正文里的 HTML 图片。第 1 组是地址前面的原文，第 2 组是地址，第 3 组是结束引号。比较时不区分大小写。
    /// 读取引用和上页改写共用这一条，避免两处规则各写一遍。
    /// </summary>
    public static readonly Regex HtmlImageRegex = new(
        "(<img[^>]+src=[\"'])([^\"']+)([\"'])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SlugRegex = new(
        @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 心得夹内允许存放的配图扩展名。包含 GIF。比较时不区分大小写。
    /// </summary>
    public static readonly HashSet<string> StoredImageExtSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    /// <summary>
    /// 心得允许上页的配图扩展名。不含 GIF。比较时不区分大小写。
    /// </summary>
    public static readonly HashSet<string> PublishImageExtSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> BlockedExtSet = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".webm", ".mp3", ".wav", ".svg"
    };

    /// <summary>
    /// 是否为心得栏目。
    /// </summary>
    public static bool IsNotesChannel(string? channelKey)
    {
        return string.Equals(channelKey, ChannelKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// 是否为该栏目下的正文文件。
    /// </summary>
    public static bool IsBodyFile(string? channelKey, string? fullPath)
    {
        return IsNotesChannel(channelKey)
            && string.Equals(Path.GetFileName(fullPath), BodyFileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 投放路径是不是这篇的正文。
    /// </summary>
    public static bool IsBodyStageRel(string? stageRel)
    {
        return string.Equals(Path.GetFileName(stageRel?.Replace('\\', '/')), BodyFileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 从投放路径取出心得夹名。不是心得正文或配图则空。
    /// </summary>
    public static string? NoteFolderFromStageRel(string? stageRel, string channelFolder)
    {
        var rel = JsonUtil.ToRel(stageRel ?? "");
        var prefix = JsonUtil.ToRel(channelFolder) + "/";
        if (!rel.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = rel[prefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0)
        {
            return null;
        }

        return rest[..slash];
    }

    /// <summary>
    /// 从投放路径取出相对这篇笔记文件夹的路径。子文件夹里的文件保留目录，例如 <c>sub/deep.gif</c>。
    /// 夹名对不上时退回文件名。网址保持原样。
    /// </summary>
    public static string RelativeToNoteFolder(string? stageRel, string? folderName)
    {
        var rel = JsonUtil.ToRel(stageRel ?? "");
        if (rel.Contains("://", StringComparison.Ordinal))
        {
            return stageRel?.Trim() ?? "";
        }

        if (!string.IsNullOrWhiteSpace(folderName))
        {
            var marker = "/" + JsonUtil.ToRel(folderName) + "/";
            var index = rel.LastIndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                var rest = rel[(index + marker.Length)..];
                if (rest.Length > 0)
                {
                    return rest;
                }
            }
        }

        var name = Path.GetFileName(rel.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? rel : name;
    }

    /// <summary>
    /// 分配不冲突的夹名。同一秒已存在则秒数加一，最多再试 60 秒。
    /// </summary>
    public static string? AllocateFolderName(string channelDir, DateTime localNow)
    {
        for (var step = 0; step < 60; step++)
        {
            var name = localNow.AddSeconds(step).ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            if (!Directory.Exists(Path.Combine(channelDir, name)))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// 新建夹、正文与空的成片目录。
    /// </summary>
    public static string CreateFolder(string channelDir, DateTime localNow)
    {
        Directory.CreateDirectory(channelDir);
        var name = AllocateFolderName(channelDir, localNow);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IOException("这一分钟内的心得夹名都已占用。");
        }

        var folder = Path.Combine(channelDir, name);
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, ReadyDirName));
        File.WriteAllText(Path.Combine(folder, BodyFileName), "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = NoteConfigStore.CreatedFromFolder(name),
            Slug = NoteConfigStore.DefaultSlug(name)
        });
        return folder;
    }

    /// <summary>
    /// 正文里第一个一级标题。没有则空。
    /// </summary>
    public static string ReadFirstHeading(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            var title = line[2..].Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }

        return "";
    }

    /// <summary>
    /// 发布稿去掉第一个一级标题，避免和页面标题重复。
    /// </summary>
    public static string WithoutFirstHeading(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        var lines = text.Split('\n').ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].TrimStart().StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            lines.RemoveAt(i);
            if (i < lines.Count && lines[i].Trim().Length == 0)
            {
                lines.RemoveAt(i);
            }

            break;
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 预演一篇上页。不改中转站正文。
    /// </summary>
    public static NotePublishPlan Plan(
        string folderFullPath,
        string stageRoot,
        NoteIndexFile index,
        IReadOnlySet<string>? markedStageRels,
        IReadOnlyDictionary<string, int>? starByStageRel)
    {
        var folderName = Path.GetFileName(folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var bodyPath = Path.Combine(folderFullPath, BodyFileName);
        if (!File.Exists(bodyPath))
        {
            return Fail("没有正文.md。");
        }

        var config = NoteConfigStore.SyncTitle(folderFullPath, folderName);
        var source = File.ReadAllText(bodyPath);
        var body = source;
        if (TryReadFrontmatter(source, out _, out var stripped, out _))
        {
            body = stripped;
        }

        var title = ReadFirstHeading(body);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = config.Title;
        }

        var slug = config.Slug;
        var summary = config.Summary;
        if (string.IsNullOrWhiteSpace(title))
        {
            return Fail("正文还没有一级标题。");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return Fail("配置里的 slug 不能为空。");
        }

        if (!SlugRegex.IsMatch(slug))
        {
            return Fail("slug 只允许小写英文、数字和短横线。");
        }

        var previous = index.Notes.FirstOrDefault(item =>
            string.Equals(item.Folder, folderName, StringComparison.Ordinal));
        if (previous?.Hidden == true)
        {
            return Fail("这篇处于隐藏，须先恢复显示。");
        }

        if (index.Notes.Any(item =>
                !string.Equals(item.Folder, folderName, StringComparison.Ordinal)
                && string.Equals(item.Slug, slug, StringComparison.Ordinal)))
        {
            return Fail("slug 与已有心得重复。");
        }

        if (string.Equals(slug, SketchesSlug, StringComparison.Ordinal)
            && index.Notes.Any(item =>
                !string.Equals(item.Folder, folderName, StringComparison.Ordinal)
                && string.Equals(item.Slug, SketchesSlug, StringComparison.Ordinal)))
        {
            return Fail("sketches 已被手绘篇占用。");
        }

        var blocked = FindBlockedFile(folderFullPath);
        if (blocked != null)
        {
            return Fail(BlockedInFolderPrefix + blocked, blocked);
        }

        var gif = FindGifFile(folderFullPath);
        if (gif != null)
        {
            return Fail(GifPublishRejectedPrefix + gif, gif);
        }

        List<NoteImageRef> referenced;
        try
        {
            referenced = ReadImageRefs(folderFullPath, body);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(ex);
        }

        var locals = ListLocalImages(folderFullPath);
        var chosen = ChooseImages(locals, referenced, markedStageRels, stageRoot);
        if (chosen.Error != null)
        {
            return Fail(chosen.Error);
        }

        var jobs = new List<NoteImageJob>();
        foreach (var image in chosen.Images)
        {
            var stageRel = JsonUtil.ToRel(Path.GetRelativePath(stageRoot, image.FullPath));
            var relative = Path.GetRelativePath(folderFullPath, image.FullPath);
            var readyRelative = Path.Combine(ReadyDirName, Path.ChangeExtension(relative, ".webp"));
            var readyFull = Path.Combine(folderFullPath, readyRelative);
            var readyStage = JsonUtil.ToRel(Path.GetRelativePath(stageRoot, readyFull));
            var objectKey = ChannelKey + "/" + slug + "/" + JsonUtil.ToRel(Path.ChangeExtension(relative, ".webp"));
            var prior = previous?.Images.FirstOrDefault(item =>
                string.Equals(item.StageRel, stageRel, StringComparison.Ordinal));
            var stars = prior?.Stars ?? 0;
            if (starByStageRel != null && starByStageRel.TryGetValue(stageRel, out var drafted))
            {
                stars = drafted;
            }

            jobs.Add(new NoteImageJob
            {
                SourceFullPath = image.FullPath,
                SourceStageRel = stageRel,
                ReadyStageRel = readyStage,
                ObjectKey = objectKey,
                AlreadyWebp = image.Extension.Equals(".webp", StringComparison.OrdinalIgnoreCase),
                Meta = new NoteImageMeta
                {
                    StageRel = stageRel,
                    ObjectKey = objectKey,
                    Stars = stars,
                    Tags = prior?.Tags ?? new List<string>(),
                    DisplayName = prior?.DisplayName ?? "",
                    Description = prior?.Description ?? "",
                    Hidden = false
                }
            });
        }

        var objectBySource = jobs.ToDictionary(item => item.SourceFullPath, item => item.ObjectKey, StringComparer.OrdinalIgnoreCase);
        string rewrittenBody;
        List<string> preview;
        try
        {
            rewrittenBody = RewriteBody(folderFullPath, body, objectBySource, out preview);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(ex);
        }

        var retired = new List<string>();
        if (previous != null)
        {
            var keep = new HashSet<string>(jobs.Select(item => item.ObjectKey), StringComparer.Ordinal);
            foreach (var image in previous.Images)
            {
                if (!string.IsNullOrWhiteSpace(image.ObjectKey) && !keep.Contains(image.ObjectKey))
                {
                    retired.Add(image.ObjectKey);
                }
            }
        }

        var date = config.CreatedAt.Length >= 10 ? config.CreatedAt[..10] : "";
        var rewritten = WithoutFirstHeading(rewrittenBody);
        return new NotePublishPlan
        {
            Ok = true,
            FolderName = folderName,
            Slug = slug,
            Title = title,
            Date = string.IsNullOrWhiteSpace(date) ? folderName[..4] + "-" + folderName[4..6] + "-" + folderName[6..8] : date,
            Summary = summary,
            RewrittenMarkdown = rewritten,
            Images = jobs,
            PreviewObjects = preview.Take(3).ToList(),
            RetiredObjects = retired,
            Body = previous?.Body ?? new NoteResourceMeta()
        };
    }

    /// <summary>
    /// 已上页的夹不能在本阶段回收。
    /// </summary>
    public static string? RecycleBlockReason(NoteIndexFile index, string folderName)
    {
        var entry = index.Notes.FirstOrDefault(item =>
            string.Equals(item.Folder, folderName, StringComparison.Ordinal));
        if (entry == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(entry.Slug) && !entry.Draft)
        {
            return "这篇已经上页。删除已上页心得留到下一阶段，本阶段不撤对象存储，也不回收夹。";
        }

        return null;
    }

    /// <summary>
    /// 解析正文开头的 frontmatter。
    /// </summary>
    public static bool TryReadFrontmatter(
        string markdown,
        out Dictionary<string, string> fields,
        out string body,
        out string error)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);
        body = markdown;
        error = "";
        var text = markdown.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            error = "正文缺少 frontmatter。";
            return false;
        }

        var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            error = "frontmatter 没有结束。";
            return false;
        }

        var header = text[4..end];
        body = text[(end + 5)..].TrimStart('\n');
        foreach (var raw in header.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"');
            fields[key] = value;
        }

        return true;
    }

    /// <summary>
    /// 访客日期用间隔点。
    /// </summary>
    public static string DisplayDate(string? isoDate)
    {
        return (isoDate ?? "").Replace('-', '.');
    }

    /// <summary>
    /// 按正文出现顺序取出图片地址。先收集 Markdown 图片，再收集 HTML 图片，与原来的上页预演相同。
    /// 不检查文件是否存在。同一地址出现多次时，按出现次数返回多次。
    /// </summary>
    public static IReadOnlyList<string> ListImageTargets(string body)
    {
        var targetList = new List<string>();
        foreach (Match match in MarkdownImageRegex.Matches(body))
        {
            targetList.Add(match.Groups[2].Value);
        }

        foreach (Match match in HtmlImageRegex.Matches(body))
        {
            targetList.Add(match.Groups[2].Value);
        }

        return targetList;
    }

    private static NotePublishPlan Fail(string error, string? blockingReference = null)
    {
        return new NotePublishPlan
        {
            Ok = false,
            Error = error,
            BlockingReference = blockingReference ?? ""
        };
    }

    /// <summary>
    /// 把带有具体文件的拒绝异常写进上页计划。其它异常只保留说明。
    /// </summary>
    private static NotePublishPlan Fail(InvalidOperationException exception)
    {
        if (exception is BlockingFileException blocking)
        {
            return Fail(blocking.Message, blocking.Reference);
        }

        return Fail(exception.Message);
    }

    private static string? FindBlockedFile(string folderFullPath)
    {
        foreach (var file in Directory.EnumerateFiles(folderFullPath, "*", SearchOption.AllDirectories))
        {
            var rel = JsonUtil.ToRel(Path.GetRelativePath(folderFullPath, file));
            if (MediaPathRules.IsNoteAuxiliaryRel(rel))
            {
                continue;
            }

            if (BlockedExtSet.Contains(Path.GetExtension(file)))
            {
                return rel;
            }
        }

        return null;
    }

    /// <summary>
    /// 列出夹内允许上页的配图，路径相对投放箱根。不含正文、GIF 与附属文件夹。
    /// </summary>
    public static IReadOnlyList<string> ListPublishImageStageRels(string folderFullPath, string stageRoot)
    {
        if (string.IsNullOrWhiteSpace(folderFullPath) || !Directory.Exists(folderFullPath))
        {
            return Array.Empty<string>();
        }

        return ListLocalImages(folderFullPath)
            .Select(image => JsonUtil.ToRel(Path.GetRelativePath(stageRoot, image.FullPath)))
            .ToList();
    }

    private static List<(string FullPath, string Extension)> ListLocalImages(string folderFullPath)
    {
        var list = new List<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(folderFullPath, "*", SearchOption.AllDirectories))
        {
            var rel = JsonUtil.ToRel(Path.GetRelativePath(folderFullPath, file));
            if (MediaPathRules.IsNoteAuxiliaryRel(rel))
            {
                continue;
            }

            var ext = Path.GetExtension(file);
            if (PublishImageExtSet.Contains(ext))
            {
                list.Add((file, ext));
            }
        }

        list.Sort((a, b) => StringComparer.Ordinal.Compare(a.Item1, b.Item1));
        return list;
    }

    private static (List<(string FullPath, string Extension)> Images, string? Error) ChooseImages(
        List<(string FullPath, string Extension)> locals,
        List<NoteImageRef> referenced,
        IReadOnlySet<string>? markedStageRels,
        string stageRoot)
    {
        if (markedStageRels == null || markedStageRels.Count == 0)
        {
            return (locals, null);
        }

        var chosen = new List<(string FullPath, string Extension)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in locals)
        {
            var stageRel = JsonUtil.ToRel(Path.GetRelativePath(stageRoot, image.FullPath));
            if (markedStageRels.Contains(stageRel) && seen.Add(image.FullPath))
            {
                chosen.Add(image);
            }
        }

        foreach (var image in referenced)
        {
            var match = locals.FirstOrDefault(item =>
                string.Equals(item.FullPath, image.FullPath, StringComparison.OrdinalIgnoreCase));
            if (match.FullPath != null && seen.Add(match.FullPath))
            {
                chosen.Add(match);
            }
        }

        return (chosen, null);
    }

    private static List<NoteImageRef> ReadImageRefs(string folderFullPath, string body)
    {
        var list = new List<NoteImageRef>();
        foreach (var raw in ListImageTargets(body))
        {
            list.Add(ResolveRef(folderFullPath, raw));
        }

        return list;
    }

    private static NoteImageRef ResolveRef(string folderFullPath, string raw)
    {
        var location = NoteImageLocation.Resolve(folderFullPath, raw);
        var target = location.Reference;
        if (location.Kind == NoteImageLocationKind.Remote)
        {
            if (IsGifExtension(target))
            {
                throw new BlockingFileException(GifPublishRejectedPrefix + target, target);
            }

            return new NoteImageRef(target, true, "");
        }

        if (location.Kind == NoteImageLocationKind.Outside)
        {
            throw new BlockingFileException(OutsideFolderPrefix + target, target);
        }

        if (IsGifExtension(location.FullPath))
        {
            throw new BlockingFileException(GifPublishRejectedPrefix + target, target);
        }

        if (!File.Exists(location.FullPath))
        {
            throw new BlockingFileException(MissingImagePrefix + target, target);
        }

        var ext = Path.GetExtension(location.FullPath);
        if (!PublishImageExtSet.Contains(ext))
        {
            throw new BlockingFileException(UnpublishableReferencePrefix + target, target);
        }

        return new NoteImageRef(target, false, location.FullPath);
    }

    private static string RewriteBody(
        string folderFullPath,
        string body,
        Dictionary<string, string> objectBySource,
        out List<string> preview)
    {
        var found = new List<string>();
        var rewritten = MarkdownImageRegex.Replace(body, match =>
        {
            var alt = match.Groups[1].Value;
            var raw = match.Groups[2].Value;
            var next = RewriteOne(folderFullPath, raw, objectBySource, found);
            return "![" + alt + "](" + next + ")";
        });
        rewritten = HtmlImageRegex.Replace(rewritten, match =>
        {
            var next = RewriteOne(folderFullPath, match.Groups[2].Value, objectBySource, found);
            return match.Groups[1].Value + next + match.Groups[3].Value;
        });
        preview = found;
        return rewritten;
    }

    private static string RewriteOne(
        string folderFullPath,
        string raw,
        Dictionary<string, string> objectBySource,
        List<string> preview)
    {
        var location = NoteImageLocation.Resolve(folderFullPath, raw);
        if (location.Kind == NoteImageLocationKind.Remote)
        {
            return raw;
        }

        if (location.Kind != NoteImageLocationKind.Inside
            || !objectBySource.TryGetValue(location.FullPath, out var objectKey))
        {
            throw new BlockingFileException(NotChosenPrefix + raw, raw);
        }

        preview.Add(objectKey);
        return objectKey;
    }

    /// <summary>
    /// 找出夹内第一张 GIF。附属文件夹里的备份不算。
    /// </summary>
    private static string? FindGifFile(string folderFullPath)
    {
        foreach (var file in Directory.EnumerateFiles(folderFullPath, "*", SearchOption.AllDirectories))
        {
            var rel = JsonUtil.ToRel(Path.GetRelativePath(folderFullPath, file));
            if (MediaPathRules.IsNoteAuxiliaryRel(rel) || !IsGifExtension(file))
            {
                continue;
            }

            return rel;
        }

        return null;
    }

    /// <summary>
    /// 扩展名或路径是否为 GIF。不区分大小写，并忽略网址里的查询参数。
    /// </summary>
    private static bool IsGifExtension(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return false;
        }

        var text = pathOrExtension.Trim();
        var query = text.IndexOf('?');
        var hash = text.IndexOf('#');
        var cut = query < 0 ? hash : hash < 0 ? query : Math.Min(query, hash);
        if (cut >= 0)
        {
            text = text[..cut];
        }

        var extension = text.StartsWith('.') && text.IndexOf('/') < 0 && text.IndexOf('\\') < 0
            ? text
            : Path.GetExtension(text);
        return string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct NoteImageRef(string Raw, bool Remote, string FullPath);

    /// <summary>
    /// 上页计划因某一个文件被拒绝。引用随异常一起返回，不再从原因文字里反推。
    /// </summary>
    private sealed class BlockingFileException : InvalidOperationException
    {
        public BlockingFileException(string message, string reference)
            : base(message)
        {
            Reference = reference;
        }

        public string Reference { get; }
    }
}
