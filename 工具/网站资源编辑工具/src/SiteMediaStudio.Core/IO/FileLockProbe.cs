using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 一次文件占用检查的结果。非 Windows 上不查询程序名称，名称列表为空。
/// </summary>
public sealed class FileLockReport
{
    /// <summary>
    /// 文件是否无法以独占方式打开。打得开则为假。
    /// </summary>
    public bool IsLocked { get; init; }

    /// <summary>
    /// 占用这个文件的程序名称。查询不到时为空列表，不表示文件一定没被占用。
    /// </summary>
    public IReadOnlyList<string> HolderNameList { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 检查文件是否被其他程序占用。Windows 上再用重启管理器查询占用程序的名称。
/// </summary>
public static class FileLockProbe
{
    private const int ErrorMoreData = 234;
    private const int MaxAppNameLength = 255;
    private const int MaxServiceNameLength = 63;

    /// <summary>
    /// 尝试以读写、不共享的方式打开文件。打不开则视为被占用，并在 Windows 上查询程序名称。
    /// 文件不存在时抛出异常。非 Windows 上名称列表为空，不因为缺少重启管理器而抛异常。
    /// </summary>
    public static FileLockReport Probe(string fullPath)
    {
        var path = RequirePath(fullPath);
        if (CanOpenExclusively(path))
        {
            return new FileLockReport();
        }

        return new FileLockReport
        {
            IsLocked = true,
            HolderNameList = QueryHolderNames(path)
        };
    }

    /// <summary>
    /// 查询正在占用文件的程序名称。非 Windows 返回空列表，不抛异常。
    /// Windows 上重启管理器不可用或没有查到名称时也返回空列表。
    /// </summary>
    public static IReadOnlyList<string> QueryHolderNames(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("要查询的文件路径不能为空。", nameof(fullPath));
        }

        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<string>();
        }

        try
        {
            return QueryHolderNamesOnWindows(Path.GetFullPath(fullPath));
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 独占打开成功则没有被占用。文件或目录不存在时把异常抛给调用方。
    /// </summary>
    private static bool CanOpenExclusively(string fullPath)
    {
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (DirectoryNotFoundException)
        {
            throw;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 路径不能是空白。返回绝对路径。
    /// </summary>
    private static string RequirePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("要检查的文件路径不能为空。", nameof(fullPath));
        }

        return Path.GetFullPath(fullPath);
    }

    /// <summary>
    /// 用重启管理器列出占用指定文件的程序，并去掉本进程自己的名称。
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> QueryHolderNamesOnWindows(string fullPath)
    {
        var sessionKey = new StringBuilder(64);
        var startCode = RmStartSession(out var sessionHandle, 0, sessionKey);
        if (startCode != 0)
        {
            return Array.Empty<string>();
        }

        try
        {
            var registerCode = RmRegisterResources(sessionHandle, 1, new[] { fullPath }, 0, IntPtr.Zero, 0, IntPtr.Zero);
            if (registerCode != 0)
            {
                return Array.Empty<string>();
            }

            var infoList = ReadProcessList(sessionHandle);
            if (infoList.Length == 0)
            {
                return Array.Empty<string>();
            }

            var nameList = new List<string>();
            foreach (var info in infoList)
            {
                if (info.Process.ProcessId == Environment.ProcessId)
                {
                    continue;
                }

                var name = FirstFilled(info.AppName, info.ServiceShortName);
                if (name.Length == 0 || nameList.Contains(name))
                {
                    continue;
                }

                nameList.Add(name);
            }

            return nameList;
        }
        finally
        {
            _ = RmEndSession(sessionHandle);
        }
    }

    /// <summary>
    /// 读取占用进程列表。数量在两次调用之间变多时再查一次，仍然不稳定就返回已经拿到的列表。
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static RmProcessInfo[] ReadProcessList(uint sessionHandle)
    {
        uint needed = 0;
        uint count = 0;
        uint reasons = 0;
        var code = RmGetList(sessionHandle, out needed, ref count, null, ref reasons);
        if (code == 0 || needed == 0)
        {
            return Array.Empty<RmProcessInfo>();
        }

        if (code != ErrorMoreData)
        {
            return Array.Empty<RmProcessInfo>();
        }

        var infoList = new RmProcessInfo[needed];
        count = (uint)infoList.Length;
        code = RmGetList(sessionHandle, out needed, ref count, infoList, ref reasons);
        if (code == ErrorMoreData)
        {
            infoList = new RmProcessInfo[needed];
            count = (uint)infoList.Length;
            code = RmGetList(sessionHandle, out needed, ref count, infoList, ref reasons);
        }

        if (code != 0 || count == 0)
        {
            return Array.Empty<RmProcessInfo>();
        }

        if (count > infoList.Length)
        {
            count = (uint)infoList.Length;
        }

        if (count == infoList.Length)
        {
            return infoList;
        }

        var trimmed = new RmProcessInfo[count];
        Array.Copy(infoList, trimmed, count);
        return trimmed;
    }

    /// <summary>
    /// 取第一个不是空白的名称。
    /// </summary>
    private static string FirstFilled(params string?[] candidateList)
    {
        foreach (var candidate in candidateList)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return "";
    }

    #region 重启管理器

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        string[] fileNameList,
        uint applicationCount,
        IntPtr applicationList,
        uint serviceCount,
        IntPtr serviceList);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmGetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfoCount,
        [In, Out] RmProcessInfo[]? processInfoList,
        ref uint rebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxAppNameLength + 1)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxServiceNameLength + 1)]
        public string ServiceShortName;

        public int ApplicationType;
        public uint AppStatus;
        public uint SessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;

        /// <summary>
        /// 让非空字符串在托管侧有初始值。重启管理器写入列表时会覆盖这些字段。
        /// </summary>
        public RmProcessInfo()
        {
            AppName = "";
            ServiceShortName = "";
        }
    }

    #endregion
}
