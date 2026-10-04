using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机文案草稿文件；不进 Git，不写内容层。
/// </summary>
public sealed class CopyDraftFile
{
    public int Version { get; set; } = 1;
    public string ProfilePath { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public List<CopyDraftEntry> Entries { get; set; } = new();
}

/// <summary>
/// 一条主体的工作副本。
/// </summary>
public sealed class CopyDraftEntry
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string PublishedTitle { get; set; } = "";
    public string PublishedDescription { get; set; } = "";
}

/// <summary>
/// 原子读写 %AppData%/SiteMediaStudio/copy-drafts.json。
/// </summary>
public static class CopyDraftStore
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
            return Path.Combine(dir, "copy-drafts.json");
        }
    }

    /// <summary>
    /// 栏目描述键。
    /// </summary>
    public static string ChannelKey(string channel)
    {
        return "channel:" + channel;
    }

    /// <summary>
    /// 项目描述键。
    /// </summary>
    public static string WorkKey(string channel, string workId)
    {
        return "work:" + channel + ":" + workId;
    }

    /// <summary>
    /// 单条资源文案键；对象键统一为正斜杠。
    /// </summary>
    public static string MediaKey(string channel, string workId, string objectKey)
    {
        return "media:" + channel + ":" + workId + ":" + JsonUtil.ToRel(objectKey);
    }

    /// <summary>
    /// 读取草稿；缺失或损坏视为空文件，不抛。
    /// </summary>
    public static CopyDraftFile Load(string? path = null)
    {
        var file = path ?? FilePath;
        if (!File.Exists(file))
        {
            return new CopyDraftFile();
        }

        try
        {
            return JsonSerializer.Deserialize<CopyDraftFile>(File.ReadAllText(file), JsonUtil.Options)
                ?? new CopyDraftFile();
        }
        catch (JsonException)
        {
            return new CopyDraftFile();
        }
        catch (IOException)
        {
            return new CopyDraftFile();
        }
    }

    /// <summary>
    /// 工作区匹配且键相同则返回该条；否则空。
    /// </summary>
    public static CopyDraftEntry? Find(CopyDraftFile file, WorkspaceProfile profile, string key)
    {
        if (file == null || profile == null || string.IsNullOrWhiteSpace(key) || !MatchesWorkspace(file, profile))
        {
            return null;
        }

        return file.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// 有草稿则覆盖输入框；无草稿则用内容层。
    /// </summary>
    public static (string Title, string Description) ResolveEditorText(
        string publishedTitle,
        string publishedDescription,
        CopyDraftEntry? entry)
    {
        if (entry == null)
        {
            return (publishedTitle ?? "", publishedDescription ?? "");
        }

        return (entry.Title ?? "", entry.Description ?? "");
    }

    /// <summary>
    /// 回车、失焦或切走主体时写入。与已发布相同则删掉该条；文件空则删除。
    /// </summary>
    public static void Commit(
        WorkspaceProfile profile,
        string key,
        string title,
        string description,
        string publishedTitle,
        string publishedDescription,
        string? path = null)
    {
        if (profile == null || string.IsNullOrWhiteSpace(key))
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
        if (IsDirty(title, description, publishedTitle, publishedDescription))
        {
            file.Entries.Add(new CopyDraftEntry
            {
                Key = key,
                Title = title ?? "",
                Description = description ?? "",
                PublishedTitle = publishedTitle ?? "",
                PublishedDescription = publishedDescription ?? "",
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
    public static bool MatchesWorkspace(CopyDraftFile file, WorkspaceProfile profile)
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

    // TODO: 草稿与内容层冲突时在底栏提示

    private static CopyDraftFile NewFile(WorkspaceProfile profile)
    {
        return new CopyDraftFile
        {
            Version = 1,
            ProfilePath = profile.ProfilePath,
            WorkspaceRoot = profile.ResolvedRoot
        };
    }

    private static void Save(CopyDraftFile file, string path)
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

    private static bool IsDirty(string? title, string? description, string? publishedTitle, string? publishedDescription)
    {
        return !SameText(title, publishedTitle) || !SameText(description, publishedDescription);
    }

    private static bool SameText(string? left, string? right)
    {
        return string.Equals(left ?? "", right ?? "", StringComparison.Ordinal);
    }
}
