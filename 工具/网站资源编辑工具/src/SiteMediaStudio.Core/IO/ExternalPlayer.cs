using System.Diagnostics;
using Microsoft.Win32;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 调用本机播放器打开视频；默认探测 PotPlayer。
/// </summary>
public static class ExternalPlayer
{
    private static readonly string[] WellKnownPathList =
    {
        @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe",
        @"C:\Program Files\DAUM\PotPlayer\PotPlayer.exe",
        @"C:\Program Files (x86)\DAUM\PotPlayer\PotPlayerMini.exe",
        @"C:\Program Files (x86)\DAUM\PotPlayer\PotPlayer.exe"
    };

    /// <summary>
    /// 解析可执行播放器；未找到则空，调用方改走系统关联。
    /// </summary>
    public static string? ResolvePlayerPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        foreach (var candidate in WellKnownPathList)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return TryReadPotPlayerFromRegistry();
    }

    /// <summary>
    /// 打开文件。优先配置 / PotPlayer，否则系统默认播放器。
    /// </summary>
    public static bool TryOpen(string filePath, string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var player = ResolvePlayerPath(configuredPath);
            if (!string.IsNullOrWhiteSpace(player))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = player,
                    Arguments = "\"" + filePath + "\"",
                    UseShellExecute = false
                });
                return true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 从常见卸载项读取 PotPlayer 安装目录。
    /// </summary>
    private static string? TryReadPotPlayerFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string?[] keyList =
        {
            @"SOFTWARE\DAUM\PotPlayer64",
            @"SOFTWARE\DAUM\PotPlayer",
            @"SOFTWARE\WOW6432Node\DAUM\PotPlayer"
        };

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var keyPath in keyList)
            {
                try
                {
                    using var key = hive.OpenSubKey(keyPath!);
                    var install = key?.GetValue("ProgramPath") as string
                        ?? key?.GetValue("InstallPath") as string
                        ?? key?.GetValue("Path") as string;
                    var resolved = ResolveInstallValue(install);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 注册表值可能是目录或 exe。
    /// </summary>
    private static string? ResolveInstallValue(string? install)
    {
        if (string.IsNullOrWhiteSpace(install))
        {
            return null;
        }

        if (File.Exists(install))
        {
            return install;
        }

        if (!Directory.Exists(install))
        {
            return null;
        }

        foreach (var name in new[] { "PotPlayerMini64.exe", "PotPlayer.exe", "PotPlayerMini.exe" })
        {
            var candidate = Path.Combine(install, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
