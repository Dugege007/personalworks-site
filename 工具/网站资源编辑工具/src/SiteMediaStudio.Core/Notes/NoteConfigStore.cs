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
            config.Slug = DefaultSlug(folderName);
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
    /// 用正文第一个一级标题更新配置。没有标题则不改。
    /// </summary>
    public static NoteConfig SyncTitle(string folderFullPath, string folderName)
    {
        var config = Ensure(folderFullPath, folderName);
        var bodyPath = Path.Combine(folderFullPath, NoteRules.BodyFileName);
        if (!File.Exists(bodyPath))
        {
            return config;
        }

        var heading = NoteRules.ReadFirstHeading(File.ReadAllText(bodyPath));
        if (string.IsNullOrWhiteSpace(heading) || string.Equals(heading, config.Title, StringComparison.Ordinal))
        {
            return config;
        }

        config.Title = heading;
        Save(folderFullPath, config);
        return config;
    }

    /// <summary>
    /// 发布上线后：正文或配图比上次记录新，就追加一条修改时间。
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

            config.ModifiedAt.Add(stamp);
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
            return JsonSerializer.Deserialize<NoteConfig>(File.ReadAllText(path), JsonUtil.Options) ?? new NoteConfig();
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
    /// 夹名转网址用的默认 slug。
    /// </summary>
    public static string DefaultSlug(string folderName)
    {
        var core = folderName.Replace('_', '-');
        return "n-" + core;
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
        DateTime? latest = Parse(config.CreatedAt);
        foreach (var item in config.ModifiedAt)
        {
            var parsed = Parse(item);
            if (parsed != null && (latest == null || parsed > latest))
            {
                latest = parsed;
            }
        }

        return latest;
    }

    private static DateTime? Parse(string? text)
    {
        if (DateTime.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
