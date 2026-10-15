using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class RunLogTests
{
    [Fact]
    public void FilePath_UnderTest_StaysOutOfMachineSettings()
    {
        var path = RunLog.FilePath;
        var machineDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SiteMediaStudio");

        Assert.EndsWith("run.log", path, StringComparison.OrdinalIgnoreCase);
        Assert.False(IsUnder(path, machineDir));
        Assert.Equal(path, RunLog.FilePath);
    }

    [Fact]
    public void UsePath_WritesOnlyTheGivenFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-runlog-explicit-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "run.log");
        var machine = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SiteMediaStudio",
            "run.log");
        var before = File.Exists(machine) ? File.ReadAllText(machine) : null;

        using (RunLog.UsePath(path))
        {
            RunLog.Append("隔离测试", true, "不应写入本机设置");
            Assert.Equal(path, RunLog.FilePath);
        }

        var written = File.ReadAllText(path);
        Assert.Contains("隔离测试", written, StringComparison.Ordinal);
        Assert.Contains("不应写入本机设置", written, StringComparison.Ordinal);
        var after = File.Exists(machine) ? File.ReadAllText(machine) : null;
        Assert.Equal(before, after);
        Assert.NotEqual(path, RunLog.FilePath);
        Assert.False(IsUnder(RunLog.FilePath, Path.GetDirectoryName(machine)!));
    }

    private static bool IsUnder(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar))
        {
            fullDirectory += Path.DirectorySeparatorChar;
        }

        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
