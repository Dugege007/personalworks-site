using System.Globalization;
using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.NoteTransitBackfill;

/// <summary>
/// 一次性把站点索引里已有、中转站还没有正文的心得夹补回来。
/// 站点 <c>content/notes</c> 与 <c>index.json</c> 只读，不在这里重发。
/// </summary>
internal static class Program
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 回填缺失夹，再按资源编辑工具的上页预演做只读核对。
    /// </summary>
    private static int Main(string[] args)
    {
        try
        {
            var repoRoot = ResolveRepoRoot(args);
            var checkOnly = args.Any(arg => string.Equals(arg, "--check", StringComparison.Ordinal));
            var report = checkOnly ? Check(repoRoot) : Backfill(repoRoot);
            Console.WriteLine(report.ToText());
            return report.Ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 为每篇还没有 <c>正文.md</c> 的索引笔记建立中转夹。
    /// </summary>
    private static BackfillReport Backfill(string repoRoot)
    {
        var paths = RepoPaths.FromRoot(repoRoot);
        var notes = ReadNotes(paths.IndexPath);
        var created = new List<string>();
        var skipped = new List<string>();
        foreach (var note in notes)
        {
            var folder = Path.Combine(paths.NotesChannel, note.Folder);
            var bodyPath = Path.Combine(folder, NoteRules.BodyFileName);
            if (File.Exists(bodyPath))
            {
                skipped.Add(note.Folder);
                continue;
            }

            WriteFolder(paths, note, folder);
            created.Add(note.Folder);
        }

        var check = Check(repoRoot);
        check.CreatedFolders = created;
        check.SkippedFolders = skipped;
        return check;
    }

    /// <summary>
    /// 写入一篇中转夹。调用前须确认该夹还没有正文。
    /// </summary>
    private static void WriteFolder(RepoPaths paths, SourceNote note, string folder)
    {
        var sitePath = Path.Combine(paths.SiteNotesDir, note.Slug + ".md");
        if (!File.Exists(sitePath))
        {
            throw new InvalidOperationException("站点正文不存在：" + note.Slug);
        }

        var body = BuildBody(File.ReadAllText(sitePath), note, out var imageRefs);
        var assetDir = Path.Combine(paths.SiteAssetRoot, note.Slug);
        var assetFiles = ListAssetImages(assetDir);
        EnsureReferencedImagesExist(note, imageRefs, assetFiles);

        var createdDir = !Directory.Exists(folder);
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var asset in assetFiles)
            {
                var destination = Path.Combine(folder, asset.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination))
                {
                    File.Copy(asset.FullPath, destination);
                }
            }

            File.WriteAllText(Path.Combine(folder, NoteRules.BodyFileName), body, JsonUtil.Utf8NoBom);
            var config = BuildConfig(note);
            // 假设：回填出来的正文和配图就是当前已发布版本。
            // 与 NoteRules.CreateFolder 相同，只拍 revisionBaseline，不追加修订。
            // 不调用 AppendModified：它会扫到已有夹，并在文件时间新于 createdAt 时写入「内容已更新。」。
            config.RevisionBaseline = NoteRevisionBaselineCapture.Capture(folder, config);
            NoteConfigStore.Save(folder, config);
        }
        catch
        {
            if (createdDir && Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            throw;
        }
    }

    /// <summary>
    /// 核对每篇非草稿都有中转正文和配置，且新建夹能通过上页预演。
    /// </summary>
    private static BackfillReport Check(string repoRoot)
    {
        var paths = RepoPaths.FromRoot(repoRoot);
        var notes = ReadNotes(paths.IndexPath);
        var index = JsonSerializer.Deserialize<NoteIndexFile>(File.ReadAllText(paths.IndexPath), JsonUtil.Options)
            ?? throw new InvalidOperationException("读不到心得索引。");
        var report = new BackfillReport();
        foreach (var note in notes)
        {
            var folder = Path.Combine(paths.NotesChannel, note.Folder);
            var bodyPath = Path.Combine(folder, NoteRules.BodyFileName);
            var configPath = Path.Combine(folder, NoteConfigStore.FileName);
            if (!File.Exists(bodyPath) || !File.Exists(configPath))
            {
                report.Errors.Add(note.Folder + " 缺少正文.md 或 心得.json。");
                continue;
            }

            if (note.Draft)
            {
                continue;
            }

            var config = NoteConfigStore.Load(configPath);
            // 旧夹不是这次回填的，字段可能早于索引，只要求正文和配置还在。
            if (config.RevisionBaseline == null)
            {
                continue;
            }

            if (!string.Equals(config.Slug, note.Slug, StringComparison.Ordinal))
            {
                report.Errors.Add(note.Folder + " 的 slug 与索引不一致。");
            }

            if (!string.Equals(config.Summary, note.Summary, StringComparison.Ordinal))
            {
                report.Errors.Add(note.Folder + " 的 summary 与索引不一致。");
            }

            if (!string.Equals(config.Title, note.Title, StringComparison.Ordinal))
            {
                report.Errors.Add(note.Folder + " 的 title 与索引不一致。");
            }

            var json = File.ReadAllText(configPath);
            if (json.Contains("\"tags\"", StringComparison.Ordinal) || json.Contains("\"stars\"", StringComparison.Ordinal))
            {
                report.Errors.Add(note.Folder + " 的心得.json 含有 tags 或 stars。");
            }

            var body = File.ReadAllText(bodyPath);
            if (HasSiteAssetImageLink(body, note.Slug))
            {
                report.Errors.Add(note.Folder + " 的图片地址仍指向 /notes-assets/。");
            }

            foreach (var raw in NoteRules.ListImageTargets(body))
            {
                var location = NoteImageLocation.Resolve(folder, raw);
                if (location.Kind != NoteImageLocationKind.Inside || !File.Exists(location.FullPath))
                {
                    report.Errors.Add(note.Folder + " 的图片不在夹内：" + raw);
                }
            }
        }

        report.Errors.AddRange(CheckCreatedBaselines(paths, notes));
        return report;
    }

    /// <summary>
    /// 只核对带有 revisionBaseline 的夹：快照与当前正文一致，且临时副本可以通过上页预演。
    /// 已有旧夹没有快照，不在这里补写。
    /// </summary>
    private static List<string> CheckCreatedBaselines(RepoPaths paths, IReadOnlyList<SourceNote> notes)
    {
        var errors = new List<string>();
        var index = JsonSerializer.Deserialize<NoteIndexFile>(File.ReadAllText(paths.IndexPath), JsonUtil.Options)
            ?? throw new InvalidOperationException("读不到心得索引。");
        foreach (var note in notes)
        {
            var folder = Path.Combine(paths.NotesChannel, note.Folder);
            var configPath = Path.Combine(folder, NoteConfigStore.FileName);
            if (!File.Exists(configPath))
            {
                continue;
            }

            var config = NoteConfigStore.Load(configPath);
            if (config.RevisionBaseline == null)
            {
                continue;
            }

            var current = NoteRevisionBaselineCapture.Capture(folder, config);
            var diff = NoteRevisionBaselineCapture.Compare(config.RevisionBaseline, current);
            if (diff.HasMeaningfulChange)
            {
                errors.Add(note.Folder + " 的 revisionBaseline 与当前夹不一致。");
            }

            if (note.Draft)
            {
                continue;
            }

            var stage = Directory.CreateTempSubdirectory("note-transit-plan-");
            try
            {
                var copy = Path.Combine(stage.FullName, "心得（notes）", note.Folder);
                CopyDirectory(folder, copy);
                var plan = NoteRules.Plan(copy, stage.FullName, index, null, null);
                if (!plan.Ok)
                {
                    errors.Add(note.Folder + " 上页预演失败：" + plan.Error);
                    continue;
                }

                if (!string.Equals(plan.Slug, note.Slug, StringComparison.Ordinal)
                    || !string.Equals(plan.Title, note.Title, StringComparison.Ordinal)
                    || !string.Equals(plan.Summary, note.Summary, StringComparison.Ordinal))
                {
                    errors.Add(note.Folder + " 上页预演的标题、slug 或摘要与索引不一致。");
                }
            }
            finally
            {
                stage.Delete(recursive: true);
            }
        }

        return errors;
    }

    /// <summary>
    /// 从站点 Markdown 得到中转站正文：去掉 frontmatter，改写本篇配图地址，必要时补一级标题。
    /// </summary>
    private static string BuildBody(string siteMarkdown, SourceNote note, out List<string> imageRefs)
    {
        var text = siteMarkdown.Replace("\r\n", "\n").Replace('\r', '\n');
        string body;
        if (text.StartsWith("---\n", StringComparison.Ordinal))
        {
            if (!NoteRules.TryReadFrontmatter(text, out _, out body, out var error))
            {
                throw new InvalidOperationException(note.Slug + " 的 frontmatter 无法解析：" + error);
            }
        }
        else
        {
            body = text.TrimStart('\n');
        }

        body = RewriteAssetLinks(body, note.Slug, out imageRefs);
        if (!HasLeadingH1(body))
        {
            if (string.IsNullOrWhiteSpace(note.Title))
            {
                throw new InvalidOperationException(note.Folder + " 没有一级标题，索引里也没有 title。");
            }

            body = "# " + note.Title.Trim() + "\n\n" + body.TrimStart('\n');
        }

        if (body.Length > 0 && !body.EndsWith('\n'))
        {
            body += "\n";
        }

        return body;
    }

    /// <summary>
    /// 把 Markdown 图片和 HTML 图片里的 <c>/notes-assets/{slug}/</c> 改成夹内相对路径。
    /// </summary>
    private static string RewriteAssetLinks(string body, string slug, out List<string> imageRefs)
    {
        var refs = new List<string>();
        var prefix = "/notes-assets/" + slug + "/";
        string Map(string raw)
        {
            var trimmed = raw.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return raw;
            }

            var relative = trimmed[prefix.Length..];
            if (relative.Contains("..", StringComparison.Ordinal) || relative.StartsWith('/'))
            {
                throw new InvalidOperationException("配图相对路径越出笔记夹：" + raw);
            }

            refs.Add(relative);
            return relative;
        }

        var rewritten = NoteRules.MarkdownImageRegex.Replace(body, match =>
        {
            var next = Map(match.Groups[2].Value);
            return "![" + match.Groups[1].Value + "](" + next + ")";
        });
        rewritten = NoteRules.HtmlImageRegex.Replace(rewritten, match =>
        {
            var next = Map(match.Groups[2].Value);
            return match.Groups[1].Value + next + match.Groups[3].Value;
        });
        imageRefs = refs;
        return rewritten;
    }

    /// <summary>
    /// 正文去掉开头空行后，首行是否已是一级标题。
    /// </summary>
    private static bool HasLeadingH1(string body)
    {
        var text = body.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('\n');
        if (text.Length == 0)
        {
            return false;
        }

        var line = text.Split('\n')[0].Trim();
        return line.StartsWith("# ", StringComparison.Ordinal);
    }

    /// <summary>
    /// 图片或 src 地址是否仍使用本篇的站点配图前缀。
    /// </summary>
    private static bool HasSiteAssetImageLink(string body, string slug)
    {
        var prefix = "/notes-assets/" + slug + "/";
        foreach (var raw in NoteRules.ListImageTargets(body))
        {
            if (raw.Trim().StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 组装心得配置。修订只在索引真有该字段时复制，不补写标签和星级。
    /// </summary>
    private static NoteConfig BuildConfig(SourceNote note)
    {
        var config = new NoteConfig
        {
            CreatedAt = ResolveCreatedAt(note),
            Slug = note.Slug,
            Title = note.Title,
            Summary = note.Summary
        };
        if (note.MainTitle != null)
        {
            config.MainTitle = note.MainTitle;
        }

        if (note.Subtitle != null)
        {
            config.Subtitle = note.Subtitle;
        }

        if (note.HasRevisions)
        {
            config.Revisions = note.Revisions;
        }

        return config;
    }

    /// <summary>
    /// 优先用索引里的 createdAt；没有则用夹名里的日期和时间。
    /// </summary>
    private static string ResolveCreatedAt(SourceNote note)
    {
        if (!string.IsNullOrWhiteSpace(note.CreatedAt))
        {
            return note.CreatedAt.Trim();
        }

        string created;
        if (NoteRules.IsTimestampFolderName(note.Folder))
        {
            created = NoteConfigStore.CreatedFromFolder(note.Folder);
        }
        else if (DateTime.TryParseExact(
            note.Date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _))
        {
            created = note.Date + " 00:00:00";
        }
        else
        {
            throw new InvalidOperationException(note.Folder + " 无法确定 createdAt。");
        }

        if (!string.IsNullOrWhiteSpace(note.Date)
            && !created.StartsWith(note.Date, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(note.Folder + " 的 createdAt 与索引 date 不一致。");
        }

        if (!DateTime.TryParseExact(created, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new InvalidOperationException(note.Folder + " 的 createdAt 格式应为 " + TimeFormat + "。");
        }

        return created;
    }

    /// <summary>
    /// 正文引用的每张配图都必须已经在站点 notes-assets 里。
    /// </summary>
    private static void EnsureReferencedImagesExist(
        SourceNote note,
        IReadOnlyList<string> imageRefs,
        IReadOnlyList<AssetImage> assetFiles)
    {
        var available = new HashSet<string>(assetFiles.Select(item => item.RelativePath), StringComparer.Ordinal);
        foreach (var raw in imageRefs)
        {
            var relative = StripUrlSuffix(raw);
            if (!available.Contains(relative))
            {
                throw new InvalidOperationException(note.Folder + " 引用了站点上不存在的配图：" + raw);
            }
        }
    }

    /// <summary>
    /// 列出站点配图目录中的图片，路径相对该 slug 目录。
    /// </summary>
    private static List<AssetImage> ListAssetImages(string assetDir)
    {
        var list = new List<AssetImage>();
        if (!Directory.Exists(assetDir))
        {
            return list;
        }

        foreach (var file in Directory.EnumerateFiles(assetDir, "*", SearchOption.AllDirectories))
        {
            if (!NoteRules.StoredImageExtSet.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            var relative = JsonUtil.ToRel(Path.GetRelativePath(assetDir, file));
            list.Add(new AssetImage(file, relative));
        }

        list.Sort((left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return list;
    }

    /// <summary>
    /// 去掉地址里的查询和片段，只留下相对文件路径。
    /// </summary>
    private static string StripUrlSuffix(string relative)
    {
        var query = relative.IndexOf('?');
        var hash = relative.IndexOf('#');
        var cut = query < 0 ? hash : hash < 0 ? query : Math.Min(query, hash);
        return cut >= 0 ? relative[..cut] : relative;
    }

    /// <summary>
    /// 读取索引。只取中转配置需要的字段，不读 body.tags 和 stars。
    /// </summary>
    private static List<SourceNote> ReadNotes(string indexPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(indexPath));
        var list = new List<SourceNote>();
        foreach (var item in document.RootElement.GetProperty("notes").EnumerateArray())
        {
            var revisions = new List<NoteRevision>();
            var hasRevisions = item.TryGetProperty("revisions", out var revisionElement)
                && revisionElement.ValueKind == JsonValueKind.Array;
            if (hasRevisions)
            {
                foreach (var revision in revisionElement.EnumerateArray())
                {
                    revisions.Add(new NoteRevision
                    {
                        At = ReadString(revision, "at"),
                        Summary = ReadString(revision, "summary"),
                        Source = ReadString(revision, "source")
                    });
                }
            }

            list.Add(new SourceNote
            {
                Folder = ReadString(item, "folder"),
                Slug = ReadString(item, "slug"),
                Title = ReadString(item, "title"),
                MainTitle = item.TryGetProperty("mainTitle", out _) ? ReadString(item, "mainTitle") : null,
                Subtitle = item.TryGetProperty("subtitle", out _) ? ReadString(item, "subtitle") : null,
                CreatedAt = item.TryGetProperty("createdAt", out var created) && created.ValueKind == JsonValueKind.String
                    ? created.GetString()
                    : null,
                Date = ReadString(item, "date"),
                Summary = ReadString(item, "summary"),
                Draft = item.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True,
                HasRevisions = hasRevisions,
                Revisions = revisions
            });
        }

        return list;
    }

    /// <summary>
    /// 读取字符串字段。缺省时为空字符串。
    /// </summary>
    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        return value.GetString() ?? "";
    }

    /// <summary>
    /// 复制目录，供上页预演使用，避免预演写回正式中转夹。
    /// </summary>
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    /// <summary>
    /// 从参数或当前目录向上找到仓库根。
    /// </summary>
    private static string ResolveRepoRoot(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--root", StringComparison.Ordinal))
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PersonalSite", "src", "content", "notes", "index.json"))
                && Directory.Exists(Path.Combine(dir.FullName, "作品中转站")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("找不到仓库根目录。请在仓库内运行，或传入 --root。");
    }

    private readonly record struct AssetImage(string FullPath, string RelativePath);

    private readonly record struct RepoPaths(string IndexPath, string SiteNotesDir, string SiteAssetRoot, string NotesChannel)
    {
        /// <summary>
        /// 按仓库根拼出本次只用到的路径。
        /// </summary>
        public static RepoPaths FromRoot(string repoRoot)
        {
            return new RepoPaths(
                Path.Combine(repoRoot, "PersonalSite", "src", "content", "notes", "index.json"),
                Path.Combine(repoRoot, "PersonalSite", "src", "content", "notes"),
                Path.Combine(repoRoot, "PersonalSite", "public", "notes-assets"),
                Path.Combine(repoRoot, "作品中转站", "心得（notes）"));
        }
    }

    private sealed class SourceNote
    {
        public string Folder { get; init; } = "";
        public string Slug { get; init; } = "";
        public string Title { get; init; } = "";
        public string? MainTitle { get; init; }
        public string? Subtitle { get; init; }
        public string? CreatedAt { get; init; }
        public string Date { get; init; } = "";
        public string Summary { get; init; } = "";
        public bool Draft { get; init; }
        public bool HasRevisions { get; init; }
        public List<NoteRevision> Revisions { get; init; } = new();
    }

    private sealed class BackfillReport
    {
        public List<string> CreatedFolders { get; set; } = new();
        public List<string> SkippedFolders { get; set; } = new();
        public List<string> Errors { get; } = new();

        public bool Ok => Errors.Count == 0;

        /// <summary>
        /// 输出给操作者看的中文摘要。
        /// </summary>
        public string ToText()
        {
            var lines = new List<string>
            {
                "新增 " + CreatedFolders.Count + " 个中转夹。",
                "跳过已有正文 " + SkippedFolders.Count + " 个。"
            };
            if (CreatedFolders.Count > 0)
            {
                lines.Add("新增夹名：");
                lines.AddRange(CreatedFolders);
            }

            if (SkippedFolders.Count > 0)
            {
                lines.Add("已跳过：");
                lines.AddRange(SkippedFolders);
            }

            if (Errors.Count == 0)
            {
                lines.Add("校验通过。");
            }
            else
            {
                lines.Add("校验失败：");
                lines.AddRange(Errors);
            }

            return string.Join('\n', lines);
        }
    }
}
