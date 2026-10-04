using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机手动对照钉，按工作区配置路径分文件，不进 Git。
/// </summary>
public static class PinStore
{
    /// <summary>
    /// 读取该配置下的手动钉。
    /// </summary>
    public static IReadOnlyList<ManualPin> Load(string? profilePath)
    {
        var path = FilePath(profilePath);
        if (path == null || !File.Exists(path))
        {
            return Array.Empty<ManualPin>();
        }

        try
        {
            var file = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(path), JsonUtil.Options);
            return file?.Pins ?? new List<ManualPin>();
        }
        catch (JsonException)
        {
            return Array.Empty<ManualPin>();
        }
    }

    /// <summary>
    /// 覆盖写入该配置下的手动钉。
    /// </summary>
    public static void Save(string? profilePath, IReadOnlyList<ManualPin> pinList)
    {
        var path = FilePath(profilePath);
        if (path == null)
        {
            return;
        }

        var file = new PinFile { Pins = pinList.ToList() };
        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonUtil.Options));
    }

    /// <summary>
    /// 增加一条钉；已存在则替换。
    /// </summary>
    public static IReadOnlyList<ManualPin> Upsert(string? profilePath, ManualPin pin)
    {
        var pinList = Load(profilePath).ToList();
        pinList.RemoveAll(item =>
            string.Equals(item.StageRel, pin.StageRel, StringComparison.Ordinal)
            || (string.Equals(item.Object, pin.Object, StringComparison.Ordinal)
                && string.Equals(item.WorkId, pin.WorkId, StringComparison.Ordinal)));
        pinList.Add(pin);
        Save(profilePath, pinList);
        return pinList;
    }

    /// <summary>
    /// 去掉与投放路径或对象键匹配的钉。
    /// </summary>
    public static IReadOnlyList<ManualPin> Remove(string? profilePath, string? stageRel, string? objectKey, string? workId)
    {
        var pinList = Load(profilePath)
            .Where(item => !IsSamePin(item, stageRel, objectKey, workId))
            .ToList();
        Save(profilePath, pinList);
        return pinList;
    }

    /// <summary>
    /// 该配置对应的钉文件路径。
    /// </summary>
    public static string? FilePath(string? profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return null;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SiteMediaStudio");
        Directory.CreateDirectory(dir);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(profilePath))))
            .ToLowerInvariant()[..16];
        return Path.Combine(dir, "pins-" + hash + ".json");
    }

    /// <summary>
    /// 是否指向同一条对照。
    /// </summary>
    private static bool IsSamePin(ManualPin item, string? stageRel, string? objectKey, string? workId)
    {
        if (!string.IsNullOrWhiteSpace(stageRel)
            && string.Equals(item.StageRel, stageRel, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(objectKey)
            && !string.IsNullOrWhiteSpace(workId)
            && string.Equals(item.Object, objectKey, StringComparison.Ordinal)
            && string.Equals(item.WorkId, workId, StringComparison.Ordinal);
    }

    private sealed class PinFile
    {
        public List<ManualPin> Pins { get; set; } = new();
    }
}
