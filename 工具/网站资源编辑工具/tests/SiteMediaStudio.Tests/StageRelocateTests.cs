using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class StageRelocateTests
{
    [Fact]
    public void Preview_FileRename_DoesNotWriteLedger()
    {
        var workDir = CopySampleSite();
        try
        {
            RenamePark01(workDir);
            var profilePath = Path.Combine(workDir, "profile.json");
            var ledgerPath = Path.Combine(workDir, "content", "media-ledger.json");
            var original = File.ReadAllText(ledgerPath);
            var session = WorkspaceSession.Load(profilePath);
            var document = BuildSingle(
                session,
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01-renamed.png");
            var report = PreviewReporter.BuildRelocate(session, document);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);
            Assert.Contains("台账将改 stageRel", report.PatchPreview, StringComparison.Ordinal);
            Assert.Contains("demo-park/01-renamed.png", report.PatchPreview, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllText(ledgerPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_FileRename_PairsAgainWithoutTouchingContent()
    {
        var workDir = CopySampleSite();
        try
        {
            RenamePark01(workDir);
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var rowsBefore = ComparePairing.Build(session, "demo-render", "demo-park", Array.Empty<ManualPin>());
            Assert.Contains(rowsBefore, row =>
                row.Kind == CompareRowKind.SiteOnly
                && row.Site?.ObjectKey == "demo-render/demo-park/01.png");
            Assert.Contains(rowsBefore, row =>
                row.Kind == CompareRowKind.StageOnly
                && row.Stage != null
                && row.Stage.StageRel.Replace('\\', '/').EndsWith("demo-park/01-renamed.png", StringComparison.Ordinal));

            var catalogBefore = File.ReadAllText(Path.Combine(workDir, "content", "catalog.json"));
            var document = BuildSingle(
                session,
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01-renamed.png");
            var intentPath = Path.Combine(workDir, "intent-relocate.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Contains(result.CompletedStepList, line => line.Contains("重挂", StringComparison.Ordinal));

            var after = WorkspaceSession.Load(profilePath);
            Assert.Equal(
                "demo-render/demo-park/01-renamed.png",
                after.LedgerDict["demo-render/demo-park/01.png"].StageRel);
            Assert.Equal("published", after.LedgerDict["demo-render/demo-park/01.png"].Status);
            var rowsAfter = ComparePairing.Build(after, "demo-render", "demo-park", Array.Empty<ManualPin>());
            Assert.Contains(rowsAfter, row =>
                row.Kind == CompareRowKind.Paired
                && row.Site?.ObjectKey == "demo-render/demo-park/01.png"
                && row.Stage?.StageRel.Replace('\\', '/') == "demo-render/demo-park/01-renamed.png");

            var catalogAfter = File.ReadAllText(Path.Combine(workDir, "content", "catalog.json"));
            Assert.Equal(catalogBefore, catalogAfter);
            using var catalog = JsonDocument.Parse(catalogAfter);
            var park = catalog.RootElement.GetProperty("works").EnumerateArray()
                .First(work => work.GetProperty("id").GetString() == "demo-park");
            Assert.Equal("演示公园", park.GetProperty("title").GetString());
            Assert.Equal(
                "demo-render/demo-park/01.png",
                park.GetProperty("media")[0].GetProperty("src").GetString());
            Assert.Equal("效果图 01", park.GetProperty("media")[0].GetProperty("label").GetString());
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_FolderRename_RewritesPublishedPrefix()
    {
        var workDir = CopySampleSite();
        try
        {
            var oldDir = Path.Combine(workDir, "stage", "demo-render", "demo-park");
            var newDir = Path.Combine(workDir, "stage", "demo-render", "demo-park-renamed");
            Directory.Move(oldDir, newDir);
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var itemList = StageRelocateRules.PlanFolder(
                session.LedgerDict,
                "demo-render/demo-park/01.png",
                "demo-render/demo-park-renamed/01.png");
            Assert.Equal(2, itemList.Count);
            Assert.All(itemList, item => Assert.StartsWith("demo-render/demo-park-renamed/", item.StageRelAfter, StringComparison.Ordinal));
            Assert.DoesNotContain(itemList, item => item.Object == "demo-render/demo-park/old.png");

            var catalogBefore = File.ReadAllText(Path.Combine(workDir, "content", "catalog.json"));
            var document = IntentDocumentBuilder.BuildRelocate(session, ExecutionMode.Direct, itemList);
            var report = PreviewReporter.BuildRelocate(session, document);
            Assert.False(report.HasHardError, string.Join("；", report.Lines.Select(line => line.Decision.Message)));
            var intentPath = Path.Combine(workDir, "intent-folder.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);

            var after = WorkspaceSession.Load(profilePath);
            Assert.Equal("demo-render/demo-park-renamed/01.png", after.LedgerDict["demo-render/demo-park/01.png"].StageRel);
            Assert.Equal("demo-render/demo-park-renamed/02.png", after.LedgerDict["demo-render/demo-park/02.png"].StageRel);
            Assert.Equal("demo-render/demo-park/old.png", after.LedgerDict["demo-render/demo-park/old.png"].StageRel);
            Assert.Equal(catalogBefore, File.ReadAllText(Path.Combine(workDir, "content", "catalog.json")));
            var rows = ComparePairing.Build(after, "demo-render", "demo-park", Array.Empty<ManualPin>());
            Assert.Contains(rows, row =>
                row.Kind == CompareRowKind.Paired
                && row.Site?.ObjectKey == "demo-render/demo-park/01.png");
            Assert.Contains(rows, row =>
                row.Kind == CompareRowKind.Paired
                && row.Site?.ObjectKey == "demo-render/demo-park/02.png");
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Gate_OccupiedAndStock_AreRejected()
    {
        var session = LoadFixture();
        var occupied = IntentGate.EvaluateRelocate(
            StageRelocateRules.CreateItem(
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/02.png"),
            Context(session));
        Assert.False(occupied.Allowed);
        Assert.Contains("占用", occupied.Message, StringComparison.Ordinal);

        var stock = IntentGate.EvaluateRelocate(
            StageRelocateRules.CreateItem(
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01.png",
                "demo-render/stock/keep.png"),
            Context(session));
        Assert.False(stock.Allowed);
        Assert.Contains("stock", stock.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_FailAfterWrite_RestoresLedgerAndKeepsIntent()
    {
        var workDir = CopySampleSite();
        try
        {
            RenamePark01(workDir);
            var profilePath = Path.Combine(workDir, "profile.json");
            var ledgerPath = Path.Combine(workDir, "content", "media-ledger.json");
            var original = File.ReadAllText(ledgerPath);
            var session = WorkspaceSession.Load(profilePath);
            var document = BuildSingle(
                session,
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01.png",
                "demo-render/demo-park/01-renamed.png");
            var intentPath = Path.Combine(workDir, "intent-fail.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var fail = LedgerWriter.Apply(session.Profile, document.Items, failAfterWrite: true);
            Assert.False(fail.Ok);
            Assert.True(fail.Restored);
            Assert.Equal(original, File.ReadAllText(ledgerPath));
            Assert.True(File.Exists(intentPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    /// <summary>
    /// 加载模拟站正本。
    /// </summary>
    private static WorkspaceSession LoadFixture()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        return WorkspaceSession.Load(profilePath!);
    }

    /// <summary>
    /// 重挂闸门上下文。
    /// </summary>
    private static IntentContext Context(WorkspaceSession session)
    {
        return new IntentContext
        {
            Profile = session.Profile,
            LedgerDict = session.LedgerDict,
            ContentRefCountDict = session.ContentRefCountDict,
            WorkList = session.Works
        };
    }

    /// <summary>
    /// 单条重挂文档。
    /// </summary>
    private static IntentDocument BuildSingle(
        WorkspaceSession session,
        string objectKey,
        string before,
        string after)
    {
        return IntentDocumentBuilder.BuildRelocate(
            session,
            ExecutionMode.Direct,
            new[] { StageRelocateRules.CreateItem(objectKey, before, after) });
    }

    /// <summary>
    /// 把公园 01 改名，模拟已上页文件漂移。
    /// </summary>
    private static void RenamePark01(string workDir)
    {
        var from = Path.Combine(workDir, "stage", "demo-render", "demo-park", "01.png");
        var to = Path.Combine(workDir, "stage", "demo-render", "demo-park", "01-renamed.png");
        Assert.True(File.Exists(from), "隔离盘缺少 demo-park/01.png");
        File.Move(from, to);
    }

    /// <summary>
    /// 拷贝模拟站正本到隔离盘。
    /// </summary>
    private static string CopySampleSite()
    {
        var source = Path.GetDirectoryName(ToolPaths.FindFixtureProfile())!;
        var dest = Path.Combine(Path.GetTempPath(), "sms-relocate-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(source, dest);
        return dest;
    }

    /// <summary>
    /// 递归复制目录。
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
