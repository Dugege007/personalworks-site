using System.Diagnostics;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 文件占用检查。非 Windows 只验证独占打开和「名称列表为空且不抛异常」。
/// 占用程序的名称要在 Windows 上由另一个进程锁住文件后再看。
/// </summary>
public sealed class FileLockProbeTests
{
    [Fact]
    public void Probe_WhenFileIsFree_ReportsUnlocked()
    {
        using var tree = new TempTree();
        var image = tree.Write("a.png", MinimalPng);

        var report = FileLockProbe.Probe(image);

        Assert.False(report.IsLocked);
        Assert.Empty(report.HolderNameList);
    }

    [Fact]
    public void Probe_WhenFileIsLocked_ReportsLockedWithoutThrowing()
    {
        using var tree = new TempTree();
        var image = tree.Write("a.png", MinimalPng);
        if (OperatingSystem.IsWindows())
        {
            AssertWindowsHolderName(image);
            return;
        }

        using var stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var report = FileLockProbe.Probe(image);

        Assert.True(report.IsLocked);
        Assert.Empty(report.HolderNameList);
    }

    [Fact]
    public void QueryHolderNames_WhenNobodyElseHoldsTheFile_ReturnsEmptyWithoutThrowing()
    {
        using var tree = new TempTree();
        var image = tree.Write("a.png", MinimalPng);

        Assert.Empty(FileLockProbe.QueryHolderNames(image));
        Assert.Empty(FileLockProbe.QueryHolderNames(tree.Missing("gone.png")));
    }

    [Fact]
    public void Probe_WhenFileIsMissing_ThrowsFileNotFound()
    {
        using var tree = new TempTree();

        Assert.Throws<FileNotFoundException>(() => FileLockProbe.Probe(tree.Missing("gone.png")));
    }

    [Fact]
    public void QueryHolderNames_WhenPathIsBlank_Throws()
    {
        Assert.Throws<ArgumentException>(() => FileLockProbe.QueryHolderNames("  "));
    }

    /// <summary>
    /// 另起一个 PowerShell 独占打开文件，再确认重启管理器能返回程序名称。
    /// </summary>
    private static void AssertWindowsHolderName(string image)
    {
        var powershell = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        Assert.True(File.Exists(powershell), "找不到用来锁文件的 PowerShell：" + powershell);

        var script = "$stream = [System.IO.File]::Open('"
            + image.Replace("'", "''")
            + "', 'Open', 'ReadWrite', 'None'); Start-Sleep -Seconds 30";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = powershell,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add(script);

        try
        {
            Assert.True(process.Start());
            var deadline = DateTime.UtcNow.AddSeconds(20);
            FileLockReport? report = null;
            while (DateTime.UtcNow < deadline)
            {
                report = FileLockProbe.Probe(image);
                if (report.IsLocked)
                {
                    break;
                }

                Thread.Sleep(200);
            }

            Assert.NotNull(report);
            Assert.True(report!.IsLocked, "PowerShell 没有把文件锁住。");
            Assert.NotEmpty(report.HolderNameList);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }

    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
        0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0xD4, 0xEF, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };

    /// <summary>
    /// 临时目录。测试结束时整棵删掉。
    /// </summary>
    private sealed class TempTree : IDisposable
    {
        private readonly string mRoot = Path.Combine(Path.GetTempPath(), "sms-lock-" + Guid.NewGuid().ToString("N"));

        public TempTree()
        {
            Directory.CreateDirectory(mRoot);
        }

        /// <summary>
        /// 写一个小文件。
        /// </summary>
        public string Write(string name, byte[] content)
        {
            var path = Path.Combine(mRoot, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        /// <summary>
        /// 返回一个肯定不存在的路径。
        /// </summary>
        public string Missing(string name)
        {
            return Path.Combine(mRoot, name);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(mRoot))
                {
                    Directory.Delete(mRoot, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
