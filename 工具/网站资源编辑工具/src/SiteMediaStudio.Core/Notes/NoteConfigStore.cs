using System.Globalization;
using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 读写心得夹里的 <c>心得.json</c>。正文不放标题和日期。
/// </summary>
public static class NoteConfigStore
{
    public const string FileName = "心得.json";
    public const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 读配置。没有文件时按夹名新建，并把旧 frontmatter 挪过来后从正文删掉。
    /// </summary>
    public static NoteConfig Ensure(string folderFullPath, string folderName)
    {
        var path = Path.Combine(folderFullPath, FileName);
        var dirty = !File.Exists(path);
        var config = dirty ? NewConfig(folderName) : Load(path);
        if (config.LegacyModifiedAtMigrated)
        {
            dirty = true;
            config.LegacyModifiedAtMigrated = false;
        }
        var bodyPath = Path.Combine(folderFullPath, NoteRules.BodyFileName);
        if (File.Exists(bodyPath)
            && NoteRules.TryReadFrontmatter(File.ReadAllText(bodyPath), out var fields, out var body, out _))
        {
            if (string.IsNullOrWhiteSpace(config.Title) && fields.TryGetValue("title", out var title))
            {
                config.Title = title;
            }

            if (string.IsNullOrWhiteSpace(config.Slug) && fields.TryGetValue("slug", out var slug) && !string.IsNullOrWhiteSpace(slug))
            {
                config.Slug = slug;
            }

            if (string.IsNullOrWhiteSpace(config.Summary) && fields.TryGetValue("summary", out var summary))
            {
                config.Summary = summary;
            }

            var prose = body.TrimStart('\n');
            if (!prose.EndsWith('\n') && prose.Length > 0)
            {
                prose += "\n";
            }

            File.WriteAllText(bodyPath, prose, JsonUtil.Utf8NoBom);
            dirty = true;
        }

        if (string.IsNullOrWhiteSpace(config.Slug))
        {
            config.Slug = DefaultSlug(folderName, config.CreatedAt);
            dirty = true;
        }

        if (string.IsNullOrWhiteSpace(config.CreatedAt))
        {
            config.CreatedAt = CreatedFromFolder(folderName);
            dirty = true;
        }

        if (dirty)
        {
            Save(folderFullPath, config);
        }

        return config;
    }

    /// <summary>
    /// 按夹名写入主标题、副标题与完整标题。时间戳夹名仍可回退到正文一级标题，但不覆盖夹名已经给出的主副标题。
    /// </summary>
    public static NoteConfig SyncTitle(string folderFullPath, string folderName)
    {
        var config = Ensure(folderFullPath, folderName);
        var resolved = ResolveTitles(folderFullPath, folderName, config);
        if (!resolved.Resolved
            || (string.Equals(resolved.MainTitle, config.MainTitle, StringComparison.Ordinal)
                && string.Equals(resolved.Subtitle, config.Subtitle, StringComparison.Ordinal)
                && string.Equals(resolved.DisplayTitle, config.Title, StringComparison.Ordinal)))
        {
            return config;
        }

        config.MainTitle = resolved.MainTitle;
        config.Subtitle = resolved.Subtitle;
        config.Title = resolved.DisplayTitle;
        Save(folderFullPath, config);
        return config;
    }

    /// <summary>
    /// 解析当前应写入配置的主副标题。夹名含「——」时以夹名为准；时间戳夹名回退到已有标题或正文一级标题。
    /// </summary>
    public static NoteTitleSet ResolveTitles(string folderFullPath, string folderName, NoteConfig config)
    {
        if (!NoteRules.IsTimestampFolderName(folderName))
        {
            var parsed = NoteRules.ParseFolderTitles(folderName);
            if (string.IsNullOrWhiteSpace(parsed.MainTitle))
            {
                return new NoteTitleSet();
            }

            return new NoteTitleSet
            {
                Resolved = true,
                MainTitle = parsed.MainTitle,
                Subtitle = parsed.Subtitle,
                DisplayTitle = parsed.DisplayTitle
            };
        }

        var heading = ReadHeading(folderFullPath);
        if (!string.IsNullOrWhiteSpace(heading)
            && !string.Equals(heading, config.Title, StringComparison.Ordinal))
        {
            return new NoteTitleSet
            {
                Resolved = true,
                MainTitle = heading,
                Subtitle = "",
                DisplayTitle = heading
            };
        }

        if (!string.IsNullOrWhiteSpace(config.MainTitle))
        {
            var main = config.MainTitle.Trim();
            var subtitle = (config.Subtitle ?? "").Trim();
            return new NoteTitleSet
            {
                Resolved = true,
                MainTitle = main,
                Subtitle = subtitle,
                DisplayTitle = NoteRules.ComposeDisplayTitle(main, subtitle)
            };
        }

        if (!string.IsNullOrWhiteSpace(config.Title))
        {
            var main = config.Title.Trim();
            return new NoteTitleSet
            {
                Resolved = true,
                MainTitle = main,
                Subtitle = "",
                DisplayTitle = main
            };
        }

        if (!string.IsNullOrWhiteSpace(heading))
        {
            return new NoteTitleSet
            {
                Resolved = true,
                MainTitle = heading,
                Subtitle = "",
                DisplayTitle = heading
            };
        }

        return new NoteTitleSet();
    }

    /// <summary>
    /// 发布上线后：正文或配图比上次记录新，就追加一条修订。摘要先留空，来源记为上页。
    /// </summary>
    public static int AppendModified(WorkspaceProfile profile, DateTime localNow)
    {
        var channel = profile.Channels.FirstOrDefault(item => NoteRules.IsNotesChannel(item.Key));
        if (channel == null)
        {
            return 0;
        }

        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot);
        var channelDir = Path.Combine(
            stageRoot,
            WorkspaceProfileLoader.ChannelStageRelative(profile, channel).Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(channelDir))
        {
            return 0;
        }

        var appended = 0;
        var stamp = localNow.ToString(TimeFormat, CultureInfo.InvariantCulture);
        foreach (var dir in Directory.EnumerateDirectories(channelDir))
        {
            if (!File.Exists(Path.Combine(dir, NoteRules.BodyFileName)))
            {
                continue;
            }

            var folderName = Path.GetFileName(dir);
            var config = Ensure(dir, folderName);
            var newest = NewestSourceTime(dir);
            if (newest == null)
            {
                continue;
            }

            var boundary = LatestRecorded(config);
            if (boundary != null && newest <= boundary)
            {
                continue;
            }

            config.Revisions.Add(new NoteRevision
            {
                At = stamp,
                Summary = "",
                Source = NoteRevisionSource.Publish
            });
            Save(dir, config);
            appended++;
        }

        return appended;
    }

    /// <summary>
    /// 读配置文件。损坏时当空表。
    /// </summary>
    public static NoteConfig Load(string path)
    {
        try
        {
            var config = JsonSerializer.Deserialize<NoteConfig>(File.ReadAllText(path), JsonUtil.Options) ?? new NoteConfig();
            Normalize(config);
            return config;
        }
        catch (Exception)
        {
            return new NoteConfig();
        }
    }

    /// <summary>
    /// 写配置文件。
    /// </summary>
    public static void Save(string folderFullPath, NoteConfig config)
    {
        File.WriteAllText(
            Path.Combine(folderFullPath, FileName),
            JsonSerializer.Serialize(config, JsonUtil.Options),
            JsonUtil.Utf8NoBom);
    }

    /// <summary>
    /// 夹名转网址用的默认 slug。只接受能变成 ASCII slug 的夹名，中文夹名不会进对象键。
    /// </summary>
    public static string DefaultSlug(string folderName)
    {
        return DefaultSlug(folderName, "");
    }

    /// <summary>
    /// 夹名不是时间戳时，用创建时间生成 <c>n-yyyyMMdd-HHmmss</c>。
    /// </summary>
    public static string DefaultSlug(string folderName, string? createdAt)
    {
        var slug = "n-" + (folderName ?? "").Replace('_', '-');
        if (NoteRules.IsSlug(slug))
        {
            return slug;
        }

        if (TryParseTime(createdAt, out var parsed))
        {
            return "n-" + parsed.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        return "n-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 最近一次修订时间。没有修订时与创建时间相同。
    /// </summary>
    public static string RevisedAt(NoteConfig config)
    {
        DateTime? latest = null;
        var latestText = "";
        foreach (var revision in config.Revisions)
        {
            if (revision == null || string.IsNullOrWhiteSpace(revision.At))
            {
                continue;
            }

            if (!TryParseTime(revision.At, out var parsed))
            {
                if (latest == null && latestText.Length == 0)
                {
                    latestText = revision.At;
                }

                continue;
            }

            if (latest == null || parsed > latest)
            {
                latest = parsed;
                latestText = revision.At;
            }
        }

        return latestText.Length > 0 ? latestText : config.CreatedAt ?? "";
    }

    /// <summary>
    /// 复制一条修订，避免索引与夹内配置共用同一个对象。
    /// </summary>
    public static NoteRevision CopyRevision(NoteRevision revision)
    {
        return new NoteRevision
        {
            At = revision.At ?? "",
            Summary = revision.Summary ?? "",
            Source = revision.Source ?? ""
        };
    }

    private static NoteConfig NewConfig(string folderName)
    {
        return new NoteConfig
        {
            CreatedAt = CreatedFromFolder(folderName),
            Slug = DefaultSlug(folderName)
        };
    }

    /// <summary>
    /// 夹名 <c>yyyyMMdd_HHmmss</c> 写成配置里的创建时间。
    /// </summary>
    public static string CreatedFromFolder(string folderName)
    {
        if (DateTime.TryParseExact(
                folderName,
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed.ToString(TimeFormat, CultureInfo.InvariantCulture);
        }

        return DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    private static DateTime? NewestSourceTime(string folderFullPath)
    {
        DateTime? newest = null;
        foreach (var file in Directory.EnumerateFiles(folderFullPath, "*", SearchOption.AllDirectories))
        {
            var rel = JsonUtil.ToRel(Path.GetRelativePath(folderFullPath, file));
            if (rel.Contains("/.site-ready/", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith(".site-ready/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(file), FileName, StringComparison.Ordinal))
            {
                continue;
            }

            var write = File.GetLastWriteTime(file);
            if (newest == null || write > newest)
            {
                newest = write;
            }
        }

        return newest;
    }

    private static DateTime? LatestRecorded(NoteConfig config)
    {
        DateTime? latest = null;
        if (TryParseTime(config.CreatedAt, out var created))
        {
            latest = created;
        }

        foreach (var item in config.Revisions)
        {
            if (item == null || !TryParseTime(item.At, out var parsed))
            {
                continue;
            }

            if (latest == null || parsed > latest)
            {
                latest = parsed;
            }
        }

        return latest;
    }

    private static string ReadHeading(string folderFullPath)
    {
        var bodyPath = Path.Combine(folderFullPath, NoteRules.BodyFileName);
        if (!File.Exists(bodyPath))
        {
            return "";
        }

        return NoteRules.ReadFirstHeading(File.ReadAllText(bodyPath));
    }

    /// <summary>
    /// 把旧时间戳收进修订列表，并补齐空字符串，避免后续空引用。
    /// </summary>
    private static void Normalize(NoteConfig config)
    {
        config.CreatedAt ??= "";
        config.Title ??= "";
        config.MainTitle ??= "";
        config.Subtitle ??= "";
        config.Slug ??= "";
        config.Summary ??= "";
        config.Revisions ??= new List<NoteRevision>();
        for (var i = config.Revisions.Count - 1; i >= 0; i--)
        {
            var revision = config.Revisions[i];
            if (revision == null)
            {
                config.Revisions.RemoveAt(i);
                continue;
            }

            revision.At ??= "";
            revision.Summary ??= "";
            revision.Source ??= "";
        }

        if (config.ModifiedAt != null)
        {
            if (config.Revisions.Count == 0)
            {
                foreach (var at in config.ModifiedAt)
                {
                    if (string.IsNullOrWhiteSpace(at))
                    {
                        continue;
                    }

                    config.Revisions.Add(new NoteRevision
                    {
                        At = at.Trim(),
                        Summary = "",
                        Source = NoteRevisionSource.Publish
                    });
                }
            }

            config.LegacyModifiedAtMigrated = true;
            config.ModifiedAt = null;
        }
    }

    private static bool TryParseTime(string? text, out DateTime parsed)
    {
        return DateTime.TryParseExact(
            text,
            TimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsed);
    }
}
