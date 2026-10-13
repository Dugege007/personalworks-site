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
    /// 读取索引。文件不存在时为空表。文件存在但解析失败时抛出 <see cref="NoteIndexCorruptException"/>，不能当成空表。
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
    /// 读取指定索引文件。文件不存在时为空表。内容无法解析时抛出带路径的异常。
    /// </summary>
    public static NoteIndexFile LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return new NoteIndexFile();
        }

        try
        {
            var index = JsonSerializer.Deserialize<NoteIndexFile>(File.ReadAllText(path), JsonUtil.Options);
            if (index == null || index.Notes == null)
            {
                throw new NoteIndexCorruptException(path);
            }

            return index;
        }
        catch (JsonException ex)
        {
            throw new NoteIndexCorruptException(path, ex);
        }
    }

    /// <summary>
    /// 写回索引。目标文件已存在但解析失败时拒绝覆盖。
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
    /// 写到指定路径。写入前再次确认原文件可以解析，避免把损坏的索引覆盖成空表。
    /// </summary>
    public static void SaveFile(string path, NoteIndexFile index)
    {
        if (File.Exists(path))
        {
            LoadFile(path);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(index, JsonUtil.Options), JsonUtil.Utf8NoBom);
    }
}

/// <summary>
/// 心得索引文件存在，但内容无法解析。此时禁止写入，以免整份索引被空表覆盖。
/// </summary>
public sealed class NoteIndexCorruptException : InvalidOperationException
{
    /// <summary>
    /// 界面展示的固定说明。完整异常信息在此之后附上文件路径。
    /// </summary>
    public const string DisplayText = "心得索引损坏或有冲突，请先修复";

    /// <summary>
    /// 无法解析的索引文件路径。
    /// </summary>
    public string IndexPath { get; }

    /// <summary>
    /// 用文件路径说明索引已损坏或含有冲突标记。
    /// </summary>
    public NoteIndexCorruptException(string indexPath, Exception? inner = null)
        : base(DisplayText + "：" + indexPath, inner)
    {
        IndexPath = indexPath;
    }
}
