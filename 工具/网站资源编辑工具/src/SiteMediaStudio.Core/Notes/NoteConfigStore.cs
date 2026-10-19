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

    private static IRevisionSummaryGenerator mSummaryGenerator = HeuristicRevisionSummaryGenerator.Instance;

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
    /// 把解析出的主标题、副标题与展示名写入配置。已经保存的主标题保持不动；时间戳夹名仍可回退到正文一级标题。
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
    /// 解析当前应写入配置的主副标题。已经记下的主标题优先，避免正文一级标题或夹名盖掉检视栏里分开填写的标题。
    /// 还没有主标题时，非时间戳夹名仍可拆出初始标题；时间戳夹名再回退到已有标题或正文一级标题。
    /// </summary>
    public static NoteTitleSet ResolveTitles(string folderFullPath, string folderName, NoteConfig config)
    {
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
    /// 发布上线时使用的摘要生成器。默认是本机启发式，不访问网络。
    /// 换成模型时在程序启动处赋一个新的 <see cref="IRevisionSummaryGenerator"/> 即可。
    /// 单次 <see cref="AppendModified"/> 传入的生成器只影响那一次，也不改修订 JSON 的字段。
    /// </summary>
    public static IRevisionSummaryGenerator SummaryGenerator
    {
        get => mSummaryGenerator;
        set => mSummaryGenerator = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 发布上线后：正文、配图、展示标题或导语相对上一版有实质差异时，追加一条助手修订。
    /// 只应由发布上线或等价的显式保存调用，不要在每次击键时调用。差异为空则不追加。
    /// 生成失败或没有上一版可对照时写入「内容已更新。」，不中断保存。已有修订的摘要不会被改写。
    /// </summary>
    public static int AppendModified(
        WorkspaceProfile profile,
        DateTime localNow,
        IRevisionSummaryGenerator? summaryGenerator = null)
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

        var generator = summaryGenerator ?? SummaryGenerator;
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
            var current = NoteRevisionBaselineCapture.Capture(dir, config);
            if (config.RevisionBaseline == null)
            {
                if (!HasNewerSource(dir, config))
                {
                    config.RevisionBaseline = current;
                    Save(dir, config);
                    continue;
                }

                // 旧稿没有快照，不能假装知道新增了哪些小节。只记一笔回退说明。
                AppendAssistantRevision(config, stamp, NoteRevisionSummaries.Fallback);
                config.RevisionBaseline = current;
                Save(dir, config);
                TryLog(true, folderName + " 没有可对照的上一版，已写入回退说明。");
                appended++;
                continue;
            }

            var diff = NoteRevisionBaselineCapture.Compare(config.RevisionBaseline, current);
            if (!diff.HasMeaningfulChange)
            {
                continue;
            }

            AppendAssistantRevision(config, stamp, Describe(generator, diff, folderName));
            config.RevisionBaseline = current;
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

    /// <summary>
    /// 追加一条助手修订。不改已经存在的条目，手改过的摘要因此不会被覆盖。
    /// </summary>
    private static void AppendAssistantRevision(NoteConfig config, string stamp, string summary)
    {
        config.Revisions.Add(new NoteRevision
        {
            At = stamp,
            Summary = summary,
            Source = NoteRevisionSource.Assistant
        });
    }

    /// <summary>
    /// 调用生成器。空结果和异常都改成回退句，并写入运行日志。日志失败也不往外抛。
    /// </summary>
    private static string Describe(IRevisionSummaryGenerator generator, NoteRevisionDiff diff, string folderName)
    {
        try
        {
            var summary = NoteRevisionSummaries.Fit(generator.Generate(diff));
            if (summary.Length == 0)
            {
                TryLog(false, folderName + " 生成结果为空，已写入回退说明。");
                return NoteRevisionSummaries.Fallback;
            }

            return summary;
        }
        catch (Exception ex)
        {
            TryLog(false, folderName + " 生成失败，已写入回退说明。" + ex.Message);
            return NoteRevisionSummaries.Fallback;
        }
    }

    /// <summary>
    /// 正文或配图的写入时间是否新于创建时间与已有修订。只在还没有内容快照时用来判断要不要记一笔。
    /// </summary>
    private static bool HasNewerSource(string folderFullPath, NoteConfig config)
    {
        var newest = NewestSourceTime(folderFullPath);
        if (newest == null)
        {
            return false;
        }

        var boundary = LatestRecorded(config);
        return boundary == null || newest > boundary;
    }

    private static void TryLog(bool ok, string detail)
    {
        try
        {
            RunLog.Append("修订摘要", ok, detail);
        }
        catch (Exception)
        {
            // 日志写不进去时仍然保存修订。
        }
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
        if (config.RevisionBaseline != null)
        {
            config.RevisionBaseline.Title ??= "";
            config.RevisionBaseline.Lead ??= "";
            config.RevisionBaseline.Body ??= "";
            config.RevisionBaseline.Images ??= new List<string>();
        }
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
