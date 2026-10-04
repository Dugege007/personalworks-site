using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 将本机文件移入回收站。
/// </summary>
public static class RecycleService
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    /// <summary>
    /// 把单个文件或发布包目录送入回收站；非 Windows 时拒绝。
    /// </summary>
    public static void SendToRecycleBin(string fullPath)
    {
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException("要回收的文件不存在。", fullPath);
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("回收站仅在 Windows 可用。");
        }

        SendOnWindows(fullPath);
    }

    /// <summary>
    /// 调用 Shell 文件操作，允许撤销。
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void SendOnWindows(string fullPath)
    {
        var operation = new SHFILEOPSTRUCT
        {
            hwnd = IntPtr.Zero,
            wFunc = FO_DELETE,
            pFrom = Path.GetFullPath(fullPath) + "\0\0",
            pTo = null,
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI),
            fAnyOperationsAborted = 0,
            hNameMappings = IntPtr.Zero,
            lpszProgressTitle = ""
        };

        var code = SHFileOperation(ref operation);
        if (code != 0 || operation.fAnyOperationsAborted != 0)
        {
            throw new IOException($"移入回收站失败，状态码 {code}。");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }
}
