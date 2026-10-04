using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机站点排序草稿；不进 Git，不写内容层。
/// </summary>
public sealed class ReorderDraftFile
{
    public int Version { get; set; } = 1;
    public string ProfilePath { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public List<ReorderDraftEntry> Entries { get; set; } = new();
}

/// <summary>
/// 一个作品的显示顺序工作副本。
/// </summary>
public sealed class ReorderDraftEntry
{
    public string Key { get; set; } = "";
    public List<string> ObjectList { get; set; } = new();
    public string UpdatedAt { get; set; } = "";
}

/// <summary>
/// 原子读写 %AppData%/SiteMediaStudio/reorder-drafts.json。
/// </summary>
public static class ReorderDraftStore
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
            return Path.Combine(dir, "reorder-drafts.json");
        }
    }

    /// <summary>
    /// 作品排序键。
    /// </summary>
    public static string WorkKey(string channel, string workId)
    {
        return "work:" + channel + ":" + workId;
    }

    /// <summary>
    /// 读取草稿；缺失或损坏视为空文件，不抛。
    /// </summary>
    public static ReorderDraftFile Load(string? path = null)
    {
        var file = path ?? FilePath;
        if (!File.Exists(file))
        {
            return new ReorderDraftFile();
        }

        try
        {
            return JsonSerializer.Deserialize<ReorderDraftFile>(File.ReadAllText(file), JsonUtil.Options)
                ?? new ReorderDraftFile();
        }
        catch (JsonException)
        {
            return new ReorderDraftFile();
        }
        catch (IOException)
        {
            return new ReorderDraftFile();
        }
    }

    /// <summary>
    /// 工作区匹配且键相同则返回该条。
    /// </summary>
    public static ReorderDraftEntry? Find(ReorderDraftFile file, WorkspaceProfile profile, string key)
    {
        if (file == null || profile == null || string.IsNullOrWhiteSpace(key) || !MatchesWorkspace(file, profile))
        {
            return null;
        }

        return file.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// 顺序与内容层不同则写入；相同则删条。
    /// </summary>
    public static void Commit(
        WorkspaceProfile profile,
        string channel,
        string workId,
        IReadOnlyList<string> objectList,
        IReadOnlyList<string> publishedList,
        string? path = null)
    {
        if (profile == null || string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(workId))
        {
            return;
        }

        var key = WorkKey(channel, workId);
        var filePath = path ?? FilePath;
        var file = Load(filePath);
        if (!MatchesWorkspace(file, profile))
        {
            file = NewFile(profile);
        }

        file.Entries.RemoveAll(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
        var normalizedList = (objectList ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => JsonUtil.ToRel(item))
            .ToList();
        if (!MediaOrder.SameOrder(normalizedList, publishedList))
        {
            file.Entries.Add(new ReorderDraftEntry
            {
                Key = key,
                ObjectList = normalizedList,
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
    public static bool MatchesWorkspace(ReorderDraftFile file, WorkspaceProfile profile)
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
    /// 执行成功后删掉已写入的草稿条。
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

    private static ReorderDraftFile NewFile(WorkspaceProfile profile)
    {
        return new ReorderDraftFile
        {
            Version = 1,
            ProfilePath = profile.ProfilePath,
            WorkspaceRoot = profile.ResolvedRoot
        };
    }

    private static void Save(ReorderDraftFile file, string path)
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
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
