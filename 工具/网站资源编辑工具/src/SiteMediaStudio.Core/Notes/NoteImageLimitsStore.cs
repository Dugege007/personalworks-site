using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 心得配图的两个大小上限。数值单位是字节。
/// </summary>
public sealed class NoteImageLimits
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// 普通配图上限。缺省为 500 KB。
    /// </summary>
    public long ImageMaxBytes { get; set; } = NoteImageThreshold.DefaultImageMaxBytes;

    /// <summary>
    /// GIF 上限。缺省为 2 MB。
    /// </summary>
    public long GifMaxBytes { get; set; } = NoteImageThreshold.DefaultGifMaxBytes;

    /// <summary>
    /// 新建一份默认阈值。每次返回新对象，避免调用方改到共享实例。
    /// </summary>
    public static NoteImageLimits CreateDefault()
    {
        return new NoteImageLimits();
    }
}

/// <summary>
/// 读写仓库里的心得配图阈值。文件与工作区配置放在同一目录，名称为 <c>note-image-limits.json</c>，随仓库同步。
/// 不写进 <c>personalworks.json</c>，这样保存阈值时不必重写整份工作区配置。
/// 文件不存在时使用默认值。文件存在但无法解析时抛出异常，并拒绝用默认值覆盖原文件。
/// </summary>
public static class NoteImageLimitsStore
{
    /// <summary>
    /// 共享配置的文件名。
    /// </summary>
    public const string FileName = "note-image-limits.json";

    /// <summary>
    /// 工作区配置旁边的阈值文件路径。工作区还没有配置路径时为空。
    /// </summary>
    public static string? FilePath(WorkspaceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfilePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(profile.ProfilePath));
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        return Path.Combine(directory, FileName);
    }

    /// <summary>
    /// 读取当前工作区的阈值。没有配置路径或文件不存在时返回默认值。
    /// </summary>
    public static NoteImageLimits Load(WorkspaceProfile profile)
    {
        var path = FilePath(profile);
        if (path == null || !File.Exists(path))
        {
            return NoteImageLimits.CreateDefault();
        }

        return LoadFile(path);
    }

    /// <summary>
    /// 读取指定阈值文件。文件不存在时返回默认值。内容无法解析时抛出带路径的异常。
    /// </summary>
    public static NoteImageLimits LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return NoteImageLimits.CreateDefault();
        }

        JsonDocument document;
        try
        {
            var json = File.ReadAllText(path);
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            throw new NoteImageLimitsException(path, "文件内容不是合法的 JSON。", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new NoteImageLimitsException(path, "根必须是一个 JSON 对象。");
            }

            var limits = NoteImageLimits.CreateDefault();
            if (TryGetProperty(root, "version", out var version))
            {
                if (version.ValueKind != JsonValueKind.Number
                    || !version.TryGetInt32(out var versionNumber)
                    || versionNumber < 1)
                {
                    throw new NoteImageLimitsException(path, "version 不是有效的版本号。");
                }

                limits.Version = versionNumber;
            }

            limits.ImageMaxBytes = ReadByteLimit(root, "imageMaxBytes", limits.ImageMaxBytes, path);
            limits.GifMaxBytes = ReadByteLimit(root, "gifMaxBytes", limits.GifMaxBytes, path);
            return limits;
        }
    }

    /// <summary>
    /// 把阈值写到当前工作区的共享配置。先确认原文件可以解析，再原子替换。
    /// </summary>
    public static void Save(WorkspaceProfile profile, NoteImageLimits limits)
    {
        var path = FilePath(profile);
        if (path == null)
        {
            throw new InvalidOperationException("工作区配置还没有路径，不能保存心得配图阈值。");
        }

        SaveFile(path, limits);
    }

    /// <summary>
    /// 把阈值写到指定路径。原文件无法解析时拒绝写入，避免用新内容覆盖损坏的配置。
    /// </summary>
    public static void SaveFile(string path, NoteImageLimits limits)
    {
        if (limits.ImageMaxBytes < 0 || limits.GifMaxBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "心得配图阈值不能为负数。");
        }

        if (File.Exists(path))
        {
            LoadFile(path);
        }

        var json = JsonSerializer.Serialize(limits, JsonUtil.Options);
        if (!json.EndsWith('\n'))
        {
            json += "\n";
        }

        AtomicTextFile.Write(path, json);
    }

    /// <summary>
    /// 读取字节上限。字段不存在时用默认值。字段存在但不是不小于 0 的整数时抛出异常。
    /// </summary>
    private static long ReadByteLimit(JsonElement root, string name, long fallback, string path)
    {
        if (!TryGetProperty(root, name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var bytes) || bytes < 0)
        {
            throw new NoteImageLimitsException(path, name + " 不是不小于 0 的整数。");
        }

        return bytes;
    }

    /// <summary>
    /// 按名称查找属性，不区分大小写，以便和本工具其它 JSON 的读取方式一致。
    /// </summary>
    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

/// <summary>
/// 心得配图阈值文件存在，但内容无法解析。此时禁止写入，以免损坏的配置被覆盖。
/// </summary>
public sealed class NoteImageLimitsException : InvalidOperationException
{
    /// <summary>
    /// 界面展示的固定说明。完整异常信息在此之后附上文件路径和原因。
    /// </summary>
    public const string DisplayText = "心得配图阈值配置损坏，请先修复，工具不会用默认值覆盖它";

    /// <summary>
    /// 无法解析的阈值文件路径。
    /// </summary>
    public string ConfigPath { get; }

    /// <summary>
    /// 用文件路径和原因说明阈值配置已损坏。
    /// </summary>
    public NoteImageLimitsException(string configPath, string reason, Exception? inner = null)
        : base(DisplayText + "：" + configPath + "。" + reason, inner)
    {
        ConfigPath = configPath;
    }
}
