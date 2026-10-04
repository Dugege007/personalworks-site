using System.Diagnostics;
using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ChannelFreezeCheckTests
{
    [Fact]
    public void Script_OkFixture_ExitsZero()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归栏目冻结核对。");
        var run = RunCheck("profile-ok.json", "digital-twin", "line-sim");
        Assert.True(run.Ok, run.StdOut + run.StdErr);
        using var report = JsonDocument.Parse(run.StdOut);
        Assert.True(report.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Script_MissingChannel_ReportsExpectKey()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归栏目冻结核对。");
        var run = RunCheck("profile-missing.json", "digital-twin", "line-sim");
        Assert.False(run.Ok);
        Assert.Contains("漏写 channels：line-sim", run.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_MismatchedEnglish_ReportsFormalName()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归栏目冻结核对。");
        var run = RunCheck("profile-mismatch.json", "digital-twin", "line-sim");
        Assert.False(run.Ok);
        Assert.Contains("正式英文", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("Digital Shadow", run.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_PersonalWorks_MatchesLexicon()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归栏目冻结核对。");
        var script = FindScript();
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(script);
        Assert.NotNull(profilePath);
        var lexiconPath = Path.GetFullPath(Path.Combine(
            ToolPaths.FindToolRoot()!,
            "..",
            "..",
            "PersonalSite",
            "src",
            "content",
            "lexicon.ts"));
        Assert.True(File.Exists(lexiconPath), lexiconPath);
        var argumentList = new List<string>
        {
            "--lexicon",
            lexiconPath,
            "--profile",
            profilePath!
        };
        foreach (var key in new[]
                 {
                     "profile",
                     "digital-twin",
                     "line-sim",
                     "game-dev",
                     "landscape-rendering",
                     "landscape-cds",
                     "real-world-photo",
                     "game-photo"
                 })
        {
            argumentList.Add("--expect-channel");
            argumentList.Add(key);
        }

        var run = NodeHost.Run(script!, argumentList, Path.GetDirectoryName(script)!);
        Assert.True(run.Ok, run.StdOut + run.StdErr);
    }

    /// <summary>
    /// 对隔离夹具调用冻结核对脚本。
    /// </summary>
    private static NodeRunResult RunCheck(string profileName, params string[] expectChannelList)
    {
        var script = FindScript();
        Assert.NotNull(script);
        var fixtureDir = Path.Combine(ToolPaths.FindToolRoot()!, "fixtures", "channel-freeze");
        var argumentList = new List<string>
        {
            "--lexicon",
            Path.Combine(fixtureDir, "lexicon.ts"),
            "--profile",
            Path.Combine(fixtureDir, profileName)
        };
        foreach (var key in expectChannelList)
        {
            argumentList.Add("--expect-channel");
            argumentList.Add(key);
        }

        return NodeHost.Run(script!, argumentList, Path.GetDirectoryName(script)!);
    }

    /// <summary>
    /// 定位栏目冻结核对脚本。
    /// </summary>
    private static string? FindScript()
    {
        var root = ToolPaths.FindToolRoot();
        if (root == null)
        {
            return null;
        }

        var path = Path.Combine(root, "scripts", "channel-freeze-check.mjs");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 是否能启动 node。
    /// </summary>
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
}
