using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 本机视频封面缓存：%AppData%/SiteMediaStudio/video-covers/。
/// </summary>
public static class VideoCoverStore
{
    /// <summary>
    /// 封面缓存根目录。
    /// </summary>
    public static string DirectoryPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SiteMediaStudio",
                "video-covers");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// 由投放路径与文件身份生成缓存键。
    /// </summary>
    public static string KeyFor(string stageRel, long length, DateTime lastWriteUtc)
    {
        var raw = JsonUtil.ToRel(stageRel) + "|" + length + "|" + lastWriteUtc.Ticks;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    /// <summary>
    /// 封面图候选路径：先 WebP，再 JPEG。
    /// </summary>
    public static string[] ImagePathList(string key, string? root = null)
    {
        var dir = root ?? DirectoryPath;
        return
        [
            Path.Combine(dir, key + ".webp"),
            Path.Combine(dir, key + ".jpg")
        ];
    }

    /// <summary>
    /// 元数据路径。
    /// </summary>
    public static string JsonPath(string key, string? root = null)
    {
        return Path.Combine(root ?? DirectoryPath, key + ".json");
    }

    /// <summary>
    /// 读取已手设封面；没有则空。
    /// </summary>
    public static VideoCoverHit? TryRead(string stageRel, long length, DateTime lastWriteUtc, string? root = null)
    {
        if (string.IsNullOrWhiteSpace(stageRel))
        {
            return null;
        }

        var key = KeyFor(stageRel, length, lastWriteUtc);
        var jsonPath = JsonPath(key, root);
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        try
        {
            var record = JsonSerializer.Deserialize<VideoCoverRecord>(File.ReadAllText(jsonPath), JsonUtil.Options);
            if (record == null)
            {
                return null;
            }

            var imagePath = ImagePathList(key, root).FirstOrDefault(File.Exists);
            if (imagePath == null)
            {
                return null;
            }

            return new VideoCoverHit(key, record, imagePath);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// 写入封面图与位置。优先 WebP 字节，否则 JPEG。
    /// </summary>
    public static VideoCoverHit Write(
        string stageRel,
        long length,
        DateTime lastWriteUtc,
        double positionSec,
        int width,
        int height,
        byte[] imageBytes,
        bool webp,
        string? root = null)
    {
        var dir = root ?? DirectoryPath;
        Directory.CreateDirectory(dir);
        var key = KeyFor(stageRel, length, lastWriteUtc);
        var ext = webp ? ".webp" : ".jpg";
        var imagePath = Path.Combine(dir, key + ext);
        foreach (var leftover in ImagePathList(key, dir))
        {
            if (!string.Equals(leftover, imagePath, StringComparison.OrdinalIgnoreCase) && File.Exists(leftover))
            {
                File.Delete(leftover);
            }
        }

        File.WriteAllBytes(imagePath, imageBytes);
        var record = new VideoCoverRecord
        {
            StageRel = JsonUtil.ToRel(stageRel),
            PositionSec = positionSec,
            Width = width,
            Height = height,
            UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
        File.WriteAllText(JsonPath(key, dir), JsonSerializer.Serialize(record, JsonUtil.Options), JsonUtil.Utf8NoBom);
        return new VideoCoverHit(key, record, imagePath);
    }
}

/// <summary>
/// 命中的封面缓存。
/// </summary>
public sealed record VideoCoverHit(string Key, VideoCoverRecord Record, string ImagePath);
