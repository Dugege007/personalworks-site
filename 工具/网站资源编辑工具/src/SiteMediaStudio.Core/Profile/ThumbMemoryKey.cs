namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 网格缩略图的内存缓存键；与磁盘文件名哈希分离。
/// </summary>
public static class ThumbMemoryKey
{
    /// <summary>
    /// 完整键：路径、边长与源文件修改时间。
    /// </summary>
    public static string Build(string fullPath, int decodePx, DateTime writeUtc)
    {
        return fullPath + "|" + decodePx + "|" + writeUtc.Ticks;
    }

    /// <summary>
    /// 去掉修改时间后的前缀，用于按源图与边长驱逐。
    /// </summary>
    public static string Prefix(string fullPath, int decodePx)
    {
        return fullPath + "|" + decodePx + "|";
    }

    /// <summary>
    /// 是否同一源图、同一解码边长。
    /// </summary>
    public static bool Matches(string cacheKey, string fullPath, int decodePx)
    {
        return cacheKey.StartsWith(Prefix(fullPath, decodePx), StringComparison.OrdinalIgnoreCase);
    }
}
