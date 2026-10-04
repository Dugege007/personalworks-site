namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 在固定磁盘上按文件名查找可执行文件。无权限的目录、回收站、卷信息和 WinSxS 跳过。
/// </summary>
public static class ExeDiskSearch
{
    private static readonly HashSet<string> SkipDirNameSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin",
        "System Volume Information",
        "WinSxS"
    };

    /// <summary>
    /// 返回第一份匹配的文件。roots 为空时扫描已就绪的固定磁盘。
    /// </summary>
    public static string? FindFirst(IReadOnlyList<string> fileNames, IEnumerable<string>? roots = null)
    {
        if (fileNames == null || fileNames.Count == 0)
        {
            return null;
        }

        var nameSet = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots ?? FixedDriveRoots())
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            var found = Walk(root, nameSet);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 已就绪的固定磁盘根。
    /// </summary>
    private static IEnumerable<string> FixedDriveRoots()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            yield return drive.RootDirectory.FullName;
        }
    }

    /// <summary>
    /// 深度优先查找。目录打不开或是重解析点则跳过。
    /// </summary>
    private static string? Walk(string dir, HashSet<string> nameSet)
    {
        IEnumerable<string> fileList;
        try
        {
            fileList = Directory.EnumerateFiles(dir);
        }
        catch (Exception)
        {
            return null;
        }

        foreach (var file in fileList)
        {
            if (nameSet.Contains(Path.GetFileName(file)))
            {
                return file;
            }
        }

        IEnumerable<string> dirList;
        try
        {
            dirList = Directory.EnumerateDirectories(dir);
        }
        catch (Exception)
        {
            return null;
        }

        foreach (var sub in dirList)
        {
            if (SkipDirNameSet.Contains(Path.GetFileName(sub)) || IsReparsePoint(sub))
            {
                continue;
            }

            var found = Walk(sub, nameSet);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 联接点和符号链接不往下走，避免循环。
    /// </summary>
    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
