using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class RecycleServiceTests
{
    [Fact]
    public void SendToRecycleBin_RemovesUnpublishedFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-recycle-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, MinimalPng);
        RecycleService.SendToRecycleBin(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Reload_AfterRecyclingUnused_DropsThatStageItem()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var sourceRoot = Path.GetDirectoryName(profilePath)!;
        var destRoot = Path.Combine(Path.GetTempPath(), "sms-ws-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceRoot, destRoot);

        var unused = Path.Combine(destRoot, "stage", "demo-render", "leftover", "unused.png");
        Assert.True(File.Exists(unused));
        RecycleService.SendToRecycleBin(unused);

        var session = WorkspaceSession.Load(Path.Combine(destRoot, "profile.json"));
        Assert.DoesNotContain(session.StageItems, item => item.StageRel.Contains("unused", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(session.StageItems, item => item.StageRel.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(session.SiteItems, item => item.ObjectKey == "demo-render/demo-park/01.png" && item.FullPath != null);
    }

    /// <summary>
    /// 复制模拟站到临时目录，避免回收站测试改仓库夹具。
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest);
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
}
