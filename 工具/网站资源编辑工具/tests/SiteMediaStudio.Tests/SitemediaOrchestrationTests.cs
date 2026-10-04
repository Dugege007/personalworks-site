using System.Diagnostics;
using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class SitemediaOrchestrationTests
{
    [Fact]
    public void Preview_DirectIngestAndWithdraw_AreAllowedOnUniqueKeys()
    {
        var workDir = CopySampleSite();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var incoming = session.StageItems.First(item =>
                item.StageRel.EndsWith("demo-park/03.png", StringComparison.OrdinalIgnoreCase));
            var hideable = session.SiteItems.First(item =>
                item.ObjectKey == "demo-render/demo-park/02.png");

            var ingestDoc = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.StageIngest, incoming, (SiteItem?)null) },
                "demo-park");
            var ingestReport = PreviewReporter.Build(
                session,
                ingestDoc,
                new[] { (ingestDoc.Items[0], incoming, (SiteItem?)null) },
                "demo-park");
            Assert.False(ingestReport.HasHardError, ingestReport.Lines[0].Decision.Message);
            Assert.Equal("demo-render/demo-park/03-02.png", ingestDoc.Items[0].Object);
            Assert.Contains("追加", ingestReport.PatchPreview, StringComparison.Ordinal);

            var withdrawDoc = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.SiteWithdraw, (StageItem?)null, hideable) });
            var withdrawReport = PreviewReporter.Build(
                session,
                withdrawDoc,
                new[] { (withdrawDoc.Items[0], (StageItem?)null, hideable) },
                hideable.WorkId);
            Assert.False(withdrawReport.HasHardError, withdrawReport.Lines[0].Decision.Message);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Preview_Withdraw_UsesSiteGateEvenWhenStageFileStillExists()
    {
        var workDir = CopySampleSite();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var site = session.SiteItems.First(item =>
                item.ObjectKey == "demo-render/demo-park/02.png");
            var stage = session.StageItems.First(item =>
                string.Equals(item.StageRel, site.StageRel, StringComparison.Ordinal));
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.SiteWithdraw, (StageItem?)null, site) });
            var report = PreviewReporter.Build(
                session,
                document,
                new[] { (document.Items[0], stage, site) },
                site.WorkId);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);
            Assert.DoesNotContain("投放箱条目", report.Lines[0].Decision.Message, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Batch_Ingest_KeepsStageFile_WritesLedgerAndSrc()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 sitemedia 编排。");
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var incoming = session.StageItems.First(item =>
                item.StageRel.EndsWith("demo-park/03.png", StringComparison.OrdinalIgnoreCase));
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.StageIngest, incoming, (SiteItem?)null) },
                "demo-park");
            var report = PreviewReporter.Build(
                session,
                document,
                new[] { (document.Items[0], incoming, (SiteItem?)null) },
                "demo-park");
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);

            var intentPath = Path.Combine(workDir, "intent-ingest.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Equal(1, result.IngestOk);
            Assert.True(File.Exists(incoming.FullPath), "中转站文件必须仍在。");
            Assert.True(File.Exists(Path.Combine(workDir, "public", "placeholders", "demo-render", "demo-park", "03.png")));

            var ledger = File.ReadAllText(Path.Combine(workDir, "content", "media-ledger.json"));
            Assert.Contains("demo-render/demo-park/03.png", ledger, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"published\"", ledger, StringComparison.Ordinal);
            var catalog = File.ReadAllText(Path.Combine(workDir, "content", "catalog.json"));
            Assert.Contains("demo-render/demo-park/03.png", catalog, StringComparison.Ordinal);
            Assert.Contains("效果图 03", catalog, StringComparison.Ordinal);
            Assert.DoesNotContain("prune-assets", result.FailureText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Batch_Ingest_ClearsMarkAfterEachSuccess_KeepsFailedMark()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 sitemedia 编排。");
        var workDir = CopySampleSite();
        var marksPath = Path.Combine(workDir, "marks.json");
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var incoming = session.StageItems.First(item =>
                item.StageRel.EndsWith("demo-park/03.png", StringComparison.OrdinalIgnoreCase));
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.StageIngest, incoming, (SiteItem?)null) },
                "demo-park");
            document.Items.Add(new IntentItem
            {
                Intent = MediaIntentCodes.StageIngest,
                WorkId = "demo-park",
                Channel = incoming.ChannelKey,
                Object = "demo-render/demo-park/99.png",
                StageRel = "demo-render/demo-park/missing.png",
                SourceStageRel = "demo-render/demo-park/missing.png"
            });

            var successKey = MarkDraftStore.KeyOf(document.Items[0]);
            var failedKey = MarkDraftStore.KeyOf(document.Items[1]);
            Assert.False(string.IsNullOrWhiteSpace(successKey));
            Assert.False(string.IsNullOrWhiteSpace(failedKey));
            MarkDraftStore.CommitMany(
                session.Profile,
                new[]
                {
                    new MarkDraftWrite(successKey!, MediaIntent.StageIngest, incoming.ChannelKey, "demo-park"),
                    new MarkDraftWrite(failedKey!, MediaIntent.StageIngest, incoming.ChannelKey, "demo-park")
                },
                marksPath);

            var intentPath = Path.Combine(workDir, "intent-partial.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath, marksPath: marksPath);
            Assert.False(result.Ok);
            Assert.Equal(1, result.IngestOk);

            var file = MarkDraftStore.Load(marksPath);
            Assert.DoesNotContain(file.Entries, entry => entry.Key == successKey);
            Assert.Contains(file.Entries, entry => entry.Key == failedKey);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Batch_Ingest_ClearsMarkWhenAlreadyPublishedSkip()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 sitemedia 编排。");
        var workDir = CopySampleSite();
        var marksPath = Path.Combine(workDir, "marks.json");
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var published = session.StageItems.First(item =>
                item.StageRel.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.StageIngest, published, (SiteItem?)null) },
                published.WorkIdGuess ?? "demo-park");
            var skipKey = MarkDraftStore.KeyOf(document.Items[0]);
            Assert.False(string.IsNullOrWhiteSpace(skipKey));
            MarkDraftStore.CommitMany(
                session.Profile,
                new[]
                {
                    new MarkDraftWrite(skipKey!, MediaIntent.StageIngest, published.ChannelKey, "demo-park")
                },
                marksPath);

            var intentPath = Path.Combine(workDir, "intent-skip.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath, marksPath: marksPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Equal(0, result.IngestOk);

            var file = MarkDraftStore.Load(marksPath);
            Assert.DoesNotContain(file.Entries, entry => entry.Key == skipKey);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Batch_Recycle_ClearsMarkAfterEachSuccess_KeepsFailedMark()
    {
        var workDir = CopySampleSite();
        var marksPath = Path.Combine(workDir, "marks.json");
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var leftover = session.StageItems.First(item =>
                item.StageRel.Replace('\\', '/').EndsWith("demo-park/03.png", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(leftover.FullPath), leftover.FullPath);
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.StageRecycle, leftover, (SiteItem?)null) });
            document.Items.Add(new IntentItem
            {
                Intent = MediaIntentCodes.StageRecycle,
                Channel = leftover.ChannelKey,
                StageRel = "demo-render/demo-park/missing.png",
                SourceStageRel = "demo-render/demo-park/missing.png"
            });

            var successKey = MarkDraftStore.KeyOf(document.Items[0]);
            var failedKey = MarkDraftStore.KeyOf(document.Items[1]);
            Assert.False(string.IsNullOrWhiteSpace(successKey));
            Assert.False(string.IsNullOrWhiteSpace(failedKey));
            MarkDraftStore.CommitMany(
                session.Profile,
                new[]
                {
                    new MarkDraftWrite(successKey!, MediaIntent.StageRecycle, leftover.ChannelKey, leftover.WorkIdGuess),
                    new MarkDraftWrite(failedKey!, MediaIntent.StageRecycle, leftover.ChannelKey, leftover.WorkIdGuess)
                },
                marksPath);

            var intentPath = Path.Combine(workDir, "intent-recycle-partial.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath, marksPath: marksPath);
            Assert.False(result.Ok);
            Assert.False(File.Exists(leftover.FullPath), leftover.FullPath);

            var file = MarkDraftStore.Load(marksPath);
            Assert.DoesNotContain(file.Entries, entry => entry.Key == successKey);
            Assert.Contains(file.Entries, entry => entry.Key == failedKey);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Batch_Hide_ClearsMarkAfterPatch_AndWhenAlreadyHiddenSkip()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁。");
        var workDir = CopySampleSite();
        var marksPath = Path.Combine(workDir, "marks.json");
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var park = session.SiteItems.First(item =>
                item.ObjectKey.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.SiteHide, (StageItem?)null, park) });
            var hideKey = MarkDraftStore.KeyOf(document.Items[0]);
            Assert.False(string.IsNullOrWhiteSpace(hideKey));
            MarkDraftStore.CommitMany(
                session.Profile,
                new[]
                {
                    new MarkDraftWrite(hideKey!, MediaIntent.SiteHide, park.ChannelKey, park.WorkId)
                },
                marksPath);

            var intentPath = Path.Combine(workDir, "intent-hide.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath, marksPath: marksPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Equal(1, result.HideOk);
            var afterHide = MarkDraftStore.Load(marksPath);
            Assert.DoesNotContain(afterHide.Entries, entry => entry.Key == hideKey);

            session = WorkspaceSession.Load(profilePath);
            var hidden = session.SiteItems.First(item =>
                item.ObjectKey.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
            Assert.True(hidden.IsHidden);
            var skipDocument = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.SiteHide, (StageItem?)null, hidden) });
            MarkDraftStore.CommitMany(
                session.Profile,
                new[]
                {
                    new MarkDraftWrite(hideKey!, MediaIntent.SiteHide, hidden.ChannelKey, hidden.WorkId)
                },
                marksPath);
            var skipIntentPath = Path.Combine(workDir, "intent-hide-skip.json");
            File.WriteAllText(skipIntentPath, IntentDocumentBuilder.ToJson(skipDocument), JsonUtil.Utf8NoBom);
            var skipResult = BatchExecutor.Run(session, skipDocument, skipIntentPath, marksPath: marksPath);
            Assert.True(skipResult.Ok, skipResult.FailureText);
            Assert.Equal(0, skipResult.HideOk);
            var afterSkip = MarkDraftStore.Load(marksPath);
            Assert.DoesNotContain(afterSkip.Entries, entry => entry.Key == hideKey);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Batch_Withdraw_ApplyBlockedWhileCatalogStillHasObject()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 sitemedia 编排。");
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var apply = SitemediaClient.Withdraw(session.Profile, "demo-render/demo-park/02.png", apply: true);
            Assert.False(apply.Ok);
            Assert.Contains("内容层仍引用", apply.FailureText, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(workDir, "public", "placeholders", "demo-render", "demo-park", "02.png")));
            Assert.DoesNotContain("prune-assets", string.Join(' ', apply.ArgumentList), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Batch_Withdraw_AfterHide_KeepsPlaceholderUntilPublish()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 sitemedia 编排。");
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var site = session.SiteItems.First(item => item.ObjectKey == "demo-render/demo-park/02.png");
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.SiteWithdraw, (StageItem?)null, site) });
            var report = PreviewReporter.Build(
                session,
                document,
                new[] { (document.Items[0], (StageItem?)null, site) },
                site.WorkId);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);

            var intentPath = Path.Combine(workDir, "intent-withdraw.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Equal(1, result.WithdrawOk);
            Assert.NotNull(result.Pending);
            Assert.Equal("spa", result.Pending!.Deploy);
            Assert.Contains("demo-render/demo-park/02.png", result.Pending.WithdrawObjectList);
            Assert.True(File.Exists(Path.Combine(workDir, "public", "placeholders", "demo-render", "demo-park", "02.png")));
            Assert.True(File.Exists(Path.Combine(workDir, "stage", "demo-render", "demo-park", "02.png")));
            var catalog = File.ReadAllText(Path.Combine(workDir, "content", "catalog.json"));
            Assert.DoesNotContain("demo-render/demo-park/02.png", catalog, StringComparison.Ordinal);
            Assert.Contains("demo-render/demo-park/01.png", catalog, StringComparison.Ordinal);
            var ledger = JsonDocument.Parse(File.ReadAllText(Path.Combine(workDir, "content", "media-ledger.json")));
            var record = ledger.RootElement.GetProperty("records").EnumerateArray()
                .First(item => item.GetProperty("object").GetString() == "demo-render/demo-park/02.png");
            Assert.Equal("published", record.GetProperty("status").GetString());
            Assert.False(PendingPublishStore.CanPublish(session.Profile, result.Pending));
            Assert.DoesNotContain("prune-assets", result.FailureText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Executor_ReorderOnly_WritesCatalogOrder()
    {
        var workDir = CopySampleSite();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var document = new IntentDocument
            {
                Mode = "direct",
                Items =
                {
                    new IntentItem
                    {
                        Intent = MediaIntentCodes.MediaReorder,
                        Channel = "demo-render",
                        WorkId = "demo-park",
                        ObjectList =
                        [
                            "demo-render/demo-park/02.png",
                            "demo-render/demo-park/01.png"
                        ]
                    }
                }
            };
            var intentPath = Path.Combine(workDir, "intent-reorder.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Contains("排序 1", result.CompletedStepList);
            var catalog = File.ReadAllText(Path.Combine(workDir, "content", "catalog.json"));
            var first = catalog.IndexOf("demo-render/demo-park/02.png", StringComparison.Ordinal);
            var second = catalog.IndexOf("demo-render/demo-park/01.png", StringComparison.Ordinal);
            Assert.True(first >= 0 && second > first, catalog);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Gate_Stock_StillRejected()
    {
        var workDir = CopySampleSite();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var stock = session.StageItems.First(item => item.IsStock);
            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[] { (MediaIntent.StageRecycle, stock, (SiteItem?)null) });
            var report = PreviewReporter.Build(
                session,
                document,
                new[] { (document.Items[0], stock, (SiteItem?)null) },
                null);
            Assert.True(report.HasHardError);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    /// <summary>
    /// 拷贝模拟站契约并写入色块图，避免改仓库正本。
    /// </summary>
    private static string CopySampleSite()
    {
        var source = Path.GetDirectoryName(ToolPaths.FindFixtureProfile())!;
        var dest = Path.Combine(Path.GetTempPath(), "sms-site-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        CopyFile(source, dest, "profile.json");
        Directory.CreateDirectory(Path.Combine(dest, "content"));
        CopyFile(Path.Combine(source, "content"), Path.Combine(dest, "content"), "catalog.json");
        CopyFile(Path.Combine(source, "content"), Path.Combine(dest, "content"), "media-ledger.json");
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

    private static void CopyFile(string sourceDir, string destDir, string name)
    {
        File.Copy(Path.Combine(sourceDir, name), Path.Combine(destDir, name), overwrite: true);
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
