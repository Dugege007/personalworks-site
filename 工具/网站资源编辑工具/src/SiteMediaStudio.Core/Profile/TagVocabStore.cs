using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机分栏标签预设。不进 Git，不代替内容层。
/// </summary>
public sealed class TagVocabFile
{
    public int Version { get; set; } = 1;

    public List<TagVocabWorkspace> Workspaces { get; set; } = new();
}

/// <summary>
/// 一个工作区根下的各栏目预设。
/// </summary>
public sealed class TagVocabWorkspace
{
    public string WorkspaceRoot { get; set; } = "";

    public List<TagVocabChannel> Channels { get; set; } = new();
}

/// <summary>
/// 一个栏目用过的自由标签。
/// </summary>
public sealed class TagVocabChannel
{
    public string Key { get; set; } = "";

    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 原子读写 %AppData%/SiteMediaStudio/tag-vocab.json。
/// </summary>
public static class TagVocabStore
{
    /// <summary>
    /// 本机预设文件路径。
    /// </summary>
    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SiteMediaStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "tag-vocab.json");
        }
    }

    /// <summary>
    /// 读取该栏目预设。缺失或损坏视为空。
    /// </summary>
    public static IReadOnlyList<string> Read(string workspaceRoot, string channel, string? path = null)
    {
        var file = Load(path);
        var found = FindChannel(file, workspaceRoot, channel, create: false);
        return found?.Tags.ToList() ?? new List<string>();
    }

    /// <summary>
    /// 记住一条自由标签。年份与类型键不写入。
    /// </summary>
    public static void Remember(string workspaceRoot, string channel, string tag, string? path = null)
    {
        var text = (tag ?? "").Trim();
        if (text.Length == 0 || !PhotoFactRules.TryNormalizeTags(new[] { text }, out _, out _))
        {
            return;
        }

        var filePath = path ?? FilePath;
        var file = Load(filePath);
        var found = FindChannel(file, workspaceRoot, channel, create: true);
        if (found == null || found.Tags.Contains(text, StringComparer.Ordinal))
        {
            return;
        }

        found.Tags.Add(text);
        Save(file, filePath);
    }

    /// <summary>
    /// 从预设中去掉一条。内容层另行改写。
    /// </summary>
    public static void Forget(string workspaceRoot, string channel, string tag, string? path = null)
    {
        var text = (tag ?? "").Trim();
        var filePath = path ?? FilePath;
        var file = Load(filePath);
        var found = FindChannel(file, workspaceRoot, channel, create: false);
        if (found == null)
        {
            return;
        }

        var next = found.Tags.Where(item => !string.Equals(item, text, StringComparison.Ordinal)).ToList();
        if (next.Count == found.Tags.Count)
        {
            return;
        }

        found.Tags = next;
        Save(file, filePath);
    }

    /// <summary>
    /// 读取预设文件。
    /// </summary>
    private static TagVocabFile Load(string? path)
    {
        var filePath = path ?? FilePath;
        if (!File.Exists(filePath))
        {
            return new TagVocabFile();
        }

        try
        {
            return JsonSerializer.Deserialize<TagVocabFile>(File.ReadAllText(filePath), JsonUtil.Options)
                ?? new TagVocabFile();
        }
        catch (JsonException)
        {
            return new TagVocabFile();
        }
        catch (IOException)
        {
            return new TagVocabFile();
        }
    }

    /// <summary>
    /// 按工作区根与栏目找预设。需要时创建空栏目。
    /// </summary>
    private static TagVocabChannel? FindChannel(TagVocabFile file, string workspaceRoot, string channel, bool create)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        var workspace = file.Workspaces.FirstOrDefault(item =>
            string.Equals(item.WorkspaceRoot, workspaceRoot, StringComparison.OrdinalIgnoreCase));
        if (workspace == null)
        {
            if (!create)
            {
                return null;
            }

            workspace = new TagVocabWorkspace { WorkspaceRoot = workspaceRoot };
            file.Workspaces.Add(workspace);
        }

        var found = workspace.Channels.FirstOrDefault(item =>
            string.Equals(item.Key, channel, StringComparison.Ordinal));
        if (found == null && create)
        {
            found = new TagVocabChannel { Key = channel };
            workspace.Channels.Add(found);
        }

        return found;
    }

    /// <summary>
    /// 先写临时文件再替换，避免写到一半。
    /// </summary>
    private static void Save(TagVocabFile file, string path)
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
