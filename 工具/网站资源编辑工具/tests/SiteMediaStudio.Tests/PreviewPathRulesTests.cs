using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PreviewPathRulesTests
{
    [Fact]
    public void PreferWeb_WhenWebExists_ReturnsWebPath()
    {
        var web = CreateTempPng();
        var original = CreateTempPng();
        var chosen = PreviewPathRules.PreferWebThenOriginal(web, original);
        Assert.Equal(web, chosen);
    }

    [Fact]
    public void PreferWeb_WhenWebMissing_ReturnsOriginal()
    {
        var original = CreateTempPng();
        var missing = Path.Combine(Path.GetTempPath(), "sms-missing-" + Guid.NewGuid().ToString("N") + ".png");
        var chosen = PreviewPathRules.PreferWebThenOriginal(missing, original);
        Assert.Equal(original, chosen);
    }

    [Fact]
    public void Scan_PublishedStage_HasWebFullPath()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var published = session.StageItems.First(item =>
            item.StageRel.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(published.WebFullPath));
        Assert.True(File.Exists(published.WebFullPath!));
        Assert.Equal(
            published.WebFullPath,
            PreviewPathRules.PreferWebThenOriginal(published.WebFullPath, published.FullPath));
    }

    /// <summary>
    /// 写入最小 PNG 供存在性检查。
    /// </summary>
    private static string CreateTempPng()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-prev-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, MinimalPng);
        return path;
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
