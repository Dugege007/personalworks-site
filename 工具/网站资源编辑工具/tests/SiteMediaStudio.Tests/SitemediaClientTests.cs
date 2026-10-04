using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class SitemediaClientTests
{
    [Fact]
    public void Args_NeverContainPruneAssets()
    {
        var ingest = SitemediaClient.BuildIngestArgs("demo-render/a.png", "demo-render/demo-park/03.png", bump: true);
        var withdraw = SitemediaClient.BuildWithdrawArgs("demo-render/demo-park/02.png", apply: true);
        Assert.False(SitemediaClient.ContainsPrune(ingest));
        Assert.False(SitemediaClient.ContainsPrune(withdraw));
        Assert.DoesNotContain(ingest, item => item.Contains("prune", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(withdraw, item => item.Contains("prune", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("--dry-run", SitemediaClient.BuildWithdrawArgs("demo-render/demo-park/02.png", apply: false));
        Assert.Contains("--apply", withdraw);
        Assert.DoesNotContain("--apply", SitemediaClient.BuildWithdrawArgs("demo-render/demo-park/02.png", apply: false));
        var withSource = SitemediaClient.BuildIngestArgs(
            "demo-render/.site-ready/demo-park/08.mp4",
            "demo-render/demo-park/08.mp4",
            bump: false,
            sourceStageRel: "demo-render/01 clip.mp4");
        Assert.Contains("--source-stage-rel", withSource);
        Assert.Contains("demo-render/01 clip.mp4", withSource);
        var samePath = SitemediaClient.BuildIngestArgs(
            "demo-render/a.png",
            "demo-render/demo-park/03.png",
            bump: false,
            sourceStageRel: "demo-render/a.png");
        Assert.DoesNotContain("--source-stage-rel", samePath);
    }

    [Fact]
    public void Parse_ContentRefCountAndRefreshUrls()
    {
        const string stdout = """
            对象：demo-render/demo-park/02.png
            内容层引用：0
            待刷新 CDN：
              https://cdn.example.com/demo-render/demo-park/02.png
              /placeholders/demo-render/demo-park/02.png
            """;
        Assert.Equal(0, SitemediaClient.ParseContentRefCount(stdout));
        var urlList = SitemediaClient.ParseRefreshUrls(stdout);
        Assert.Equal(2, urlList.Count);
        Assert.Contains("https://cdn.example.com/demo-render/demo-park/02.png", urlList);
        Assert.Equal(2, SitemediaClient.ParseContentRefCount("内容层引用：2\n"));
        Assert.Null(SitemediaClient.ParseContentRefCount("没有计数"));
        Assert.Equal(2, SitemediaClient.ParseLedgerRenameRetries("已入库 demo/01.webp\n台账改名重试：2\n"));
        Assert.Equal(0, SitemediaClient.ParseLedgerRenameRetries("已入库 demo/01.webp\n"));
        Assert.Equal(0, SitemediaClient.ParseLedgerRenameRetries("台账改名重试中：1\n"));
        Assert.Equal(1, SitemediaClient.ParseLedgerRenameAttempt("台账改名重试中：1"));
        Assert.Equal(2, SitemediaClient.ParseLedgerRenameAttempt("台账改名重试：2"));
        Assert.Equal(0, SitemediaClient.ParseLedgerRenameAttempt("已入库 demo/01.webp"));
    }

    [Fact]
    public void Environment_UsesProfileRoots()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var env = SitemediaClient.BuildEnvironment(session.Profile);
        Assert.Equal(session.Profile.ResolvedRoot, env["WORKSPACE_ROOT"]);
        Assert.EndsWith("stage", env["SITEMEDIA_STAGE_ROOT"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("placeholders", env["SITEMEDIA_PLACEHOLDERS_ROOT"], StringComparison.OrdinalIgnoreCase);
        Assert.False(env.ContainsKey("SITEMEDIA_DEPLOY_ENV"));
        Assert.DoesNotContain(env.Values, value => value.Contains("prune-assets", StringComparison.OrdinalIgnoreCase));
    }
}
