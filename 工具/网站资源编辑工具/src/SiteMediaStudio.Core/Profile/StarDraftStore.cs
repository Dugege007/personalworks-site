using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机星级草稿；不进 Git，不写内容层。
/// </summary>
public sealed class StarDraftFile
{
    public int Version { get; set; } = 1;
    public string ProfilePath { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public List<StarDraftEntry> Entries { get; set; } = new();
}

/// <summary>
/// 一条资源尚未执行的星级。
/// </summary>
public sealed class StarDraftEntry
{
    public string Key { get; set; } = "";
    public int Stars { get; set; }
    public int PublishedStars { get; set; }
    public string UpdatedAt { get; set; } = "";
}

/// <summary>
/// 原子读写 %AppData%/SiteMediaStudio/star-drafts.json。
/// </summary>
public static class StarDraftStore
{
    /// <summary>
    /// 本机草稿文件路径。
    /// </summary>
    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SiteMediaStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "star-drafts.json");
        }
    }

    /// <summary>
    /// 已入编对象键。
    /// </summary>
    public static string ObjectKey(string objectKey)
    {
        return "object:" + JsonUtil.ToRel(objectKey);
    }

    /// <summary>
    /// 投放箱未配对相对路径。
    /// </summary>
    public static string StageKey(string stageRel)
    {
        return "stage:" + JsonUtil.ToRel(stageRel);
    }

    /// <summary>
    /// 读取草稿；缺失或损坏视为空文件，不抛。
    /// </summary>
    public static StarDraftFile Load(string? path = null)
    {
        var file = path ?? FilePath;
        if (!File.Exists(file))
        {
            return new StarDraftFile();
        }

        try
        {
            return JsonSerializer.Deserialize<StarDraftFile>(File.ReadAllText(file), JsonUtil.Options)
                ?? new StarDraftFile();
        }
        catch (JsonException)
        {
            return new StarDraftFile();
        }
        catch (IOException)
        {
            return new StarDraftFile();
        }
    }

    /// <summary>
    /// 工作区匹配且键相同则返回该条；否则空。
    /// </summary>
    public static StarDraftEntry? Find(StarDraftFile file, WorkspaceProfile profile, string key)
    {
        if (file == null || profile == null || string.IsNullOrWhiteSpace(key) || !MatchesWorkspace(file, profile))
        {
            return null;
        }

        return file.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// 与内容层相同则删掉该条；否则写入。文件空则删除。
    /// </summary>
    public static void Commit(
        WorkspaceProfile profile,
        string key,
        int stars,
        int publishedStars,
        string? path = null)
    {
        if (profile == null || string.IsNullOrWhiteSpace(key) || stars is < 0 or > 5)
        {
            return;
        }

        var filePath = path ?? FilePath;
        var file = Load(filePath);
        if (!MatchesWorkspace(file, profile))
        {
            file = NewFile(profile);
        }

        file.Entries.RemoveAll(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
        if (stars != publishedStars)
        {
            file.Entries.Add(new StarDraftEntry
            {
                Key = key,
                Stars = stars,
                PublishedStars = publishedStars,
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        if (file.Entries.Count == 0)
        {
            Delete(filePath);
            return;
        }

        Save(file, filePath);
    }

    /// <summary>
    /// 当前文件是否属于该工作区。
    /// </summary>
    public static bool MatchesWorkspace(StarDraftFile file, WorkspaceProfile profile)
    {
        if (file == null || profile == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(file.ProfilePath)
            && string.Equals(file.ProfilePath, profile.ProfilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(file.WorkspaceRoot ?? "", profile.ResolvedRoot ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 执行成功后删掉已对齐的草稿条；文件空则删除。
    /// </summary>
    public static void RemoveKeys(WorkspaceProfile profile, IEnumerable<string> keyList, string? path = null)
    {
        if (profile == null)
        {
            return;
        }

        var filePath = path ?? FilePath;
        var file = Load(filePath);
        if (!MatchesWorkspace(file, profile))
        {
            return;
        }

        var removeSet = new HashSet<string>(
            keyList.Where(key => !string.IsNullOrWhiteSpace(key)),
            StringComparer.Ordinal);
        if (removeSet.Count == 0)
        {
            return;
        }

        file.Entries.RemoveAll(entry => removeSet.Contains(entry.Key));
        if (file.Entries.Count == 0)
        {
            Delete(filePath);
            return;
        }

        Save(file, filePath);
    }

    /// <summary>
    /// 删除草稿文件；缺失不报错。
    /// </summary>
    public static void Delete(string? path = null)
    {
        var file = path ?? FilePath;
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    private static StarDraftFile NewFile(WorkspaceProfile profile)
    {
        return new StarDraftFile
        {
            Version = 1,
            ProfilePath = profile.ProfilePath,
            WorkspaceRoot = profile.ResolvedRoot
        };
    }

    private static void Save(StarDraftFile file, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(file, JsonUtil.Options);
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tempPath, json, JsonUtil.Utf8NoBom);
        try
        {
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}
