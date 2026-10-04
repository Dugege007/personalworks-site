using System.Security.Cryptography;
using System.Text;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// EXE 旁 thumbs 目录的磁盘缩略图缓存。
/// </summary>
public static class ThumbCacheStore
{
    /// <summary>
    /// 缓存根目录：可执行文件同级的 thumbs。
    /// </summary>
    public static string DirectoryPath
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "thumbs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// 按源文件、边长与修改时间生成缓存文件名。
    /// </summary>
    public static string FileNameFor(string fullPath, int decodePx, DateTime lastWriteUtc)
    {
        var raw = fullPath.Replace('\\', '/') + "|" + decodePx + "|" + lastWriteUtc.Ticks;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
            .ToLowerInvariant()[..16];
        return hash + ".jpg";
    }

    /// <summary>
    /// 一条缓存文件的绝对路径。
    /// </summary>
    public static string ResolveFile(string fullPath, int decodePx, DateTime lastWriteUtc)
    {
        return Path.Combine(DirectoryPath, FileNameFor(fullPath, decodePx, lastWriteUtc));
    }

    /// <summary>
    /// 统计缓存目录字节数。
    /// </summary>
    public static long MeasureBytes(string? root = null)
    {
        var dir = root ?? DirectoryPath;
        if (!Directory.Exists(dir))
        {
            return 0;
        }

        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path).Length)
            .Sum();
    }

    /// <summary>
    /// 清空缓存文件，保留目录。
    /// </summary>
    public static void Clear(string? root = null)
    {
        var dir = root ?? DirectoryPath;
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
