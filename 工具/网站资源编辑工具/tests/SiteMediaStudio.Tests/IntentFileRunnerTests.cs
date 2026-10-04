using System.Diagnostics;
using System.Text;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class IntentFileRunnerTests
{
    [Fact]
    public void Preview_Hide_DoesNotWriteCatalog()
    {
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var original = File.ReadAllText(catalogPath);
            var intentPath = WriteHidePark02(workDir);
            var result = IntentFileRunner.Preview(profilePath, intentPath);
            Assert.True(result.Ok, result.Text);
            Assert.Equal(IntentFileRunner.ExitOk, result.ExitCode);
            Assert.Contains("[允许] site.hide", result.Text, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllText(catalogPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Preview_MatchesDirectReporter_ForSameHideIntent()
    {
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var site = session.SiteItems.First(item => item.ObjectKey == "demo-render/demo-park/02.png");
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.SiteHide, (StageItem?)null, site) });
            var windowReport = PreviewReporter.BuildFromDocument(session, document);
            var intentPath = Path.Combine(workDir, "intent-hide.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var cliReport = IntentFileRunner.Preview(profilePath, intentPath).Preview;
            Assert.NotNull(cliReport);
            Assert.Equal(windowReport.HasHardError, cliReport!.HasHardError);
            Assert.Equal(windowReport.Lines[0].Decision.Message, cliReport.Lines[0].Decision.Message);
            Assert.Equal(windowReport.PatchPreview, cliReport.PatchPreview);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_WhenGateRejects_DoesNotWrite()
    {
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var original = File.ReadAllText(catalogPath);
            var intentPath = Path.Combine(workDir, "intent-stock.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "stage.recycle",
                      "channel": "demo-render",
                      "stageRel": "demo-render/stock/keep.png"
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);
            var pendingPath = Path.Combine(workDir, "pending-publish.json");
            var result = IntentFileRunner.Apply(profilePath, intentPath, pendingPath);
            Assert.False(result.Ok);
            Assert.Equal(IntentFileRunner.ExitRejected, result.ExitCode);
            Assert.Contains("拒绝", result.Text, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllText(catalogPath));
            Assert.False(File.Exists(pendingPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_Hide_WritesCatalogAndKeepsPlaceholders()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁。");
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var intentPath = WriteHidePark02(workDir);
            var pendingPath = Path.Combine(workDir, "pending-publish.json");
            var result = IntentFileRunner.Apply(profilePath, intentPath, pendingPath);
            Assert.True(result.Ok, result.Text);
            Assert.Equal(1, result.Batch?.HideOk);
            var catalog = File.ReadAllText(catalogPath);
            Assert.DoesNotContain("demo-render/demo-park/02.png", catalog, StringComparison.Ordinal);
            Assert.Contains("demo-render/demo-park/01.png", catalog, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(
                workDir,
                "public",
                "placeholders",
                "demo-render",
                "demo-park",
                "02.png")));
            Assert.True(File.Exists(pendingPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Cli_Preview_ExitZeroOnAllowedHide()
    {
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var intentPath = WriteHidePark02(workDir);
            var run = RunCli("preview", profilePath, intentPath);
            Assert.Equal(0, run.ExitCode);
            Assert.Contains("[允许] site.hide", run.StdOut, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    /// <summary>
    /// 写出隐藏演示公园第 2 张的意图。
    /// </summary>
    private static string WriteHidePark02(string workDir)
    {
        var intentPath = Path.Combine(workDir, "intent-hide.json");
        File.WriteAllText(
            intentPath,
            """
            {
              "version": 1,
              "mode": "direct",
              "items": [
                {
                  "intent": "site.hide",
                  "channel": "demo-render",
                  "workId": "demo-park",
                  "object": "demo-render/demo-park/02.png"
                }
              ],
              "options": { "relabel": true, "deploy": "none" }
            }
            """,
            JsonUtil.Utf8NoBom);
        return intentPath;
    }

    /// <summary>
    /// 启动已编好的 CLI 程序集。
    /// </summary>
    private static (int ExitCode, string StdOut, string StdErr) RunCli(
        string command,
        string profilePath,
        string intentPath)
    {
        var cliDll = Path.Combine(
            AppContext.BaseDirectory,
            "SiteMediaStudio.Cli.dll");
        Assert.True(File.Exists(cliDll), "测试输出目录应包含 SiteMediaStudio.Cli.dll。");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{cliDll}\" {command} --profile \"{profilePath}\" --intent \"{intentPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            CreateNoWindow = true
        });
        Assert.NotNull(process);
        var stdOut = process!.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit(30000);
        return (process.ExitCode, stdOut, stdErr);
    }

    /// <summary>
    /// 拷贝模拟站契约并写入色块图，避免改仓库正本。
    /// </summary>
    private static string CopySampleSite()
    {
        var source = Path.GetDirectoryName(ToolPaths.FindFixtureProfile())!;
        var dest = Path.Combine(Path.GetTempPath(), "sms-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        File.Copy(Path.Combine(source, "profile.json"), Path.Combine(dest, "profile.json"), overwrite: true);
        Directory.CreateDirectory(Path.Combine(dest, "content"));
        File.Copy(
            Path.Combine(source, "content", "catalog.json"),
            Path.Combine(dest, "content", "catalog.json"),
            overwrite: true);
        File.Copy(
            Path.Combine(source, "content", "media-ledger.json"),
            Path.Combine(dest, "content", "media-ledger.json"),
            overwrite: true);
        foreach (var rel in new[]
                 {
                     "demo-render/demo-park/01.png",
                     "demo-render/demo-park/02.png",
                     "demo-render/demo-park/03.png",
                     "demo-render/demo-park/old.png",
                     "demo-render/demo-yard/01.png",
                     "demo-render/leftover/unused.png",
                     "demo-render/stock/keep.png",
                     "demo-photo/demo-walk/01.png"
                 })
        {
            WritePng(Path.Combine(dest, "stage", rel.Replace('/', Path.DirectorySeparatorChar)));
        }

        foreach (var rel in new[]
                 {
                     "demo-render/demo-park/01.png",
                     "demo-render/demo-park/02.png",
                     "demo-render/demo-yard/01.png",
                     "demo-photo/demo-walk/01.png",
                     "demo-render/stock/keep.png"
                 })
        {
            WritePng(Path.Combine(dest, "public", "placeholders", rel.Replace('/', Path.DirectorySeparatorChar)));
        }

        return dest;
    }

    private static void WritePng(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, MinimalPng);
    }

    private static bool HasNode()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                Arguments = "-v",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

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
