using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 读写 <c>PersonalSite/src/content/notes/index.json</c>。
/// </summary>
public static class NoteIndexStore
{
    /// <summary>
    /// 发布索引路径。站点编目未配置时为空。
    /// </summary>
    public static string? IndexPath(WorkspaceProfile profile)
    {
        var dir = NotesDir(profile);
        return dir == null ? null : Path.Combine(dir, "index.json");
    }

    /// <summary>
    /// 发布稿目录。
    /// </summary>
    public static string? NotesDir(WorkspaceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.SiteCatalog.SitePath))
        {
            return null;
        }

        var siteFile = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.SitePath);
        var contentDir = Path.GetDirectoryName(siteFile);
        if (string.IsNullOrWhiteSpace(contentDir))
        {
            return null;
        }

        return Path.Combine(contentDir, "notes");
    }

    /// <summary>
    /// 读取索引。文件不存在时为空表。
    /// </summary>
    public static NoteIndexFile Load(WorkspaceProfile profile)
    {
        var path = IndexPath(profile);
        if (path == null || !File.Exists(path))
        {
            return new NoteIndexFile();
        }

        return LoadFile(path);
    }

    /// <summary>
    /// 读取指定索引文件。
    /// </summary>
    public static NoteIndexFile LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return new NoteIndexFile();
        }

        try
        {
            return JsonSerializer.Deserialize<NoteIndexFile>(File.ReadAllText(path), JsonUtil.Options)
                ?? new NoteIndexFile();
        }
        catch (JsonException)
        {
            return new NoteIndexFile();
        }
    }

    /// <summary>
    /// 写回索引。
    /// </summary>
    public static void Save(WorkspaceProfile profile, NoteIndexFile index)
    {
        var path = IndexPath(profile);
        if (path == null)
        {
            throw new InvalidOperationException("工作区没有站点内容目录，不能写心得索引。");
        }

        SaveFile(path, index);
    }

    /// <summary>
    /// 写到指定路径。
    /// </summary>
    public static void SaveFile(string path, NoteIndexFile index)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(index, JsonUtil.Options), JsonUtil.Utf8NoBom);
    }
}
