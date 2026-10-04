using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class GamePackCatalogTests
{
    [Theory]
    [InlineData("webgl", true)]
    [InlineData("Build", true)]
    [InlineData("Library", false)]
    [InlineData("Assets", false)]
    public void PathRules_RecognizePackDirName(string name, bool expected)
    {
        Assert.Equal(expected, MediaPathRules.IsPackDirName(name));
        Assert.Equal(expected, MediaPathRules.IsPackDirectory("demo-render/demo-park/" + name));
    }

    [Fact]
    public void PathRules_SkipFilesInsidePack()
    {
        Assert.True(MediaPathRules.IsInsidePackDirectory("demo-render/demo-park/webgl/TemplateData/icon.png"));
        Assert.False(MediaPathRules.IsCatalogFile("demo-render/demo-park/webgl/TemplateData/icon.png"));
        Assert.False(MediaPathRules.IsInsidePackDirectory("demo-render/demo-park/webgl"));
        Assert.False(MediaPathRules.IsInsidePackDirectory("demo-render/demo-park/01.png"));
        Assert.True(MediaPathRules.IsGamePackTemplate("_id"));
        Assert.False(MediaPathRules.IsGamePackTemplate("antigravity"));
    }

    [Fact]
    public void Thumb_PrefersSiblingLogoThenTemplateIconThenScreenshot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-pack-thumb-" + Guid.NewGuid().ToString("N"));
        var workDir = Path.Combine(root, "park");
        var packDir = Path.Combine(workDir, "webgl");
        var templateDir = Path.Combine(packDir, "TemplateData");
        Directory.CreateDirectory(templateDir);
        WriteTinyPng(Path.Combine(workDir, "01.png"));
        WriteTinyPng(Path.Combine(templateDir, "icon-192.png"));
        try
        {
            Assert.Equal(
                Path.Combine(templateDir, "icon-192.png"),
                GamePackThumb.Resolve(workDir, packDir));
            WriteTinyPng(Path.Combine(workDir, "logo.webp"));
            Assert.Equal(
                Path.Combine(workDir, "logo.webp"),
                GamePackThumb.Resolve(workDir, packDir));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Scan_SameFolderPack_IsOneNodeAndSkipsInnerImages()
    {
        var workDir = CopySampleWithPacks();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var pack = session.StageItems.Single(item =>
                item.IsPack
                && item.StageRel.Replace('\\', '/').EndsWith("demo-park/webgl", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("demo-park", pack.WorkIdGuess);
            Assert.Equal("demo-park", pack.StageFolderGuess);
            Assert.Equal("demo-render", pack.ChannelKey);
            Assert.True(pack.ThumbPath?.EndsWith("logo.webp", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                session.StageItems,
                item => item.StageRel.Replace('\\', '/').Contains("/webgl/", StringComparison.OrdinalIgnoreCase)
                    && !item.IsPack);
            Assert.Contains(
                session.StageItems,
                item => item.StageRel.Replace('\\', '/').EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                session.StageItems,
                item => item.IsPack && item.WorkIdGuess == "_id");
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Scan_PackOnlyFolder_AppearsUnregistered()
    {
        var workDir = CopySampleWithPacks();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            Assert.Contains(
                session.Works,
                work => work.IsUnregistered
                    && work.Channel == "demo-render"
                    && work.Id == "pack-only");
            var pack = session.StageItems.Single(item =>
                item.IsPack && item.WorkIdGuess == "pack-only");
            Assert.Equal("pack-only", pack.StageFolderGuess);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Gate_Pack_UnregisteredFirst_ThenNotOpened()
    {
        var workDir = CopySampleWithPacks();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var unregistered = session.StageItems.Single(item => item.IsPack && item.WorkIdGuess == "pack-only");
            var unregisteredDecision = IntentGate.EvaluateStage(
                MediaIntent.StageIngest,
                unregistered,
                new IntentContext
                {
                    Profile = session.Profile,
                    LedgerDict = session.LedgerDict,
                    ContentRefCountDict = session.ContentRefCountDict,
                    WorkList = session.Works,
                    TargetWorkId = "pack-only"
                });
            Assert.False(unregisteredDecision.Allowed);
            Assert.Contains("须先登记或改选已有作品", unregisteredDecision.Message, StringComparison.Ordinal);

            var registered = session.StageItems.Single(item => item.IsPack && item.WorkIdGuess == "demo-park");
            var registeredDecision = IntentGate.EvaluateStage(
                MediaIntent.StageIngest,
                registered,
                new IntentContext
                {
                    Profile = session.Profile,
                    LedgerDict = session.LedgerDict,
                    ContentRefCountDict = session.ContentRefCountDict,
                    WorkList = session.Works,
                    TargetWorkId = "demo-park"
                });
            Assert.False(registeredDecision.Allowed);
            Assert.Contains("游戏包上页未开通", registeredDecision.Message, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    /// <summary>
    /// 拷贝模拟站并放入已入编 / 未登记 / 模板三处发布包。
    /// </summary>
    private static string CopySampleWithPacks()
    {
        var source = Path.GetDirectoryName(ToolPaths.FindFixtureProfile())!;
        var dest = Path.Combine(Path.GetTempPath(), "sms-pack-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(source, dest);
        var stageRoot = Path.Combine(dest, "stage", "demo-render");
        var parkPack = Path.Combine(stageRoot, "demo-park", "webgl", "TemplateData");
        Directory.CreateDirectory(parkPack);
        WriteTinyPng(Path.Combine(stageRoot, "demo-park", "logo.webp"));
        WriteTinyPng(Path.Combine(parkPack, "icon-192.png"));
        WriteTinyPng(Path.Combine(parkPack, "progress.png"));
        File.WriteAllText(Path.Combine(stageRoot, "demo-park", "webgl", "index.html"), "<html></html>");

        var onlyPack = Path.Combine(stageRoot, "pack-only", "webgl");
        Directory.CreateDirectory(onlyPack);
        File.WriteAllText(Path.Combine(onlyPack, "index.html"), "<html></html>");

        var templatePack = Path.Combine(stageRoot, "_id", "webgl");
        Directory.CreateDirectory(templatePack);
        File.WriteAllText(Path.Combine(templatePack, "index.html"), "<html></html>");
        return dest;
    }

    /// <summary>
    /// 写入一枚最小 PNG。
    /// </summary>
    private static void WriteTinyPng(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(
            path,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
    }

    /// <summary>
    /// 递归拷贝目录。
    /// </summary>
    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }

    /// <summary>
    /// 尽量删掉临时目录。
    /// </summary>
    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
