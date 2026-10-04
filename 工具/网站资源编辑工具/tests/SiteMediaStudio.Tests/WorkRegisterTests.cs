using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class WorkRegisterTests
{
    [Fact]
    public void SeedId_TechnicalFolder_CanFill_ChineseCannot()
    {
        Assert.Equal("new-folder", WorkRegisterRules.SeedId("new-folder"));
        Assert.Null(WorkRegisterRules.SeedId("新林"));
        Assert.Null(WorkRegisterRules.SeedId("New-Folder"));
        Assert.False(WorkRegisterRules.IsTechnicalKey("demo_park"));
        Assert.Equal("新林", WorkRegisterRules.SeedTitle("新林"));
        Assert.Equal("仪摄影写真", WorkRegisterRules.SeedTitle("20240824 仪摄影写真"));
        Assert.Equal("2025-02-20", WorkRegisterRules.SeedStartedOn("20250220 范德威尔"));
        Assert.Equal("2024-09", WorkRegisterRules.SeedStartedOn("202409 新林"));
        Assert.Equal("", WorkRegisterRules.SeedStartedOn("新林"));
        Assert.True(WorkRegisterRules.TryNormalizeStartedOn("20250220", out var startedOn));
        Assert.Equal("2025-02-20", startedOn);
        Assert.False(WorkRegisterRules.TryNormalizeStartedOn("2025-13-40", out _));
        Assert.True(WorkRegisterRules.TryNormalizePlace("无锡", out var place));
        Assert.Equal("无锡", place);
        Assert.False(WorkRegisterRules.TryNormalizePlace("无锡/车间", out _));
        Assert.Equal("20250413 上海 静安寺", WorkRegisterRules.SeedIdForChannel("real-world-photo", "20250413 上海 静安寺"));
        Assert.Null(WorkRegisterRules.SeedIdForChannel("landscape-photo", "20250413 上海 静安寺"));
        Assert.True(WorkRegisterRules.IsAcceptedWorkId("real-world-photo", "20250413 上海 静安寺"));
        Assert.False(WorkRegisterRules.IsAcceptedWorkId("landscape-rendering", "20250413 上海 静安寺"));
    }

    [Fact]
    public void Gate_RejectsBadStartedOn_AllowsEmptySchedule()
    {
        var session = LoadFixture();
        Assert.True(Evaluate(session, "demo-render", "new-folder", "新夹", "demo-render/new-folder").Allowed);
        var badDate = IntentGate.EvaluateRegister(
            new IntentItem
            {
                Intent = MediaIntentCodes.WorkRegister,
                Channel = "demo-render",
                WorkId = "new-folder",
                Title = "新夹",
                StageFolder = "demo-render/new-folder",
                StartedOn = "不是日期"
            },
            Context(session, session.Profile));
        Assert.Contains("开始日", badDate.Message, StringComparison.Ordinal);
        var missing = IntentGate.EvaluateUpdate(
            new IntentItem
            {
                Intent = MediaIntentCodes.WorkUpdate,
                Channel = "demo-render",
                WorkId = "no-such-work",
                Title = "无"
            },
            Context(session, session.Profile));
        Assert.Contains("找不到已入编作品", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_RegisterNewFolder_ShowsEmptyShellAndDoesNotWrite()
    {
        var session = LoadFixture();
        var catalogPath = WorkspaceProfileLoader.ResolveUnderRoot(
            session.Profile,
            session.Profile.SiteCatalog.Path);
        var original = File.ReadAllText(catalogPath);
        var document = IntentDocumentBuilder.BuildRegister(
            session,
            ExecutionMode.Prompt,
            "demo-render",
            "new-folder",
            "新夹",
            "demo-render/new-folder");
        var report = PreviewReporter.BuildRegister(session, document);

        Assert.False(report.HasHardError);
        Assert.Contains("登记空壳", report.PatchPreview, StringComparison.Ordinal);
        Assert.Contains("\"media\": []", report.PatchPreview, StringComparison.Ordinal);
        Assert.Contains("new-folder", report.PatchPreview, StringComparison.Ordinal);
        Assert.DoesNotContain("demo-render/new-folder/01.png", report.PatchPreview, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllText(catalogPath));
    }

    [Fact]
    public void Gate_RejectsIllegalDuplicateAndUnknownChannel()
    {
        var session = LoadFixture();
        Assert.Contains("小写短横线", Evaluate(session, "demo-render", "新林", "新林", "demo-render/new-folder").Message);
        Assert.Contains("同 id", Evaluate(session, "demo-render", "demo-park", "公园", "demo-render/new-folder").Message);
        Assert.Contains("未写入工作区配置", Evaluate(session, "no-such-channel", "demo-grove", "林", "no-such-channel/new-folder").Message);
        Assert.True(Evaluate(session, "demo-render", "new-folder", "新夹", "demo-render/new-folder").Allowed);
        Assert.True(Evaluate(session, "demo-render", "nested-new", "嵌套新夹", "demo-render/studio-a/nested-new").Allowed);
        Assert.Contains(
            "已登记容器",
            Evaluate(session, "demo-render", "orphan-new", "误夹", "demo-render/leftover/orphan-new").Message);
    }

    [Fact]
    public void Gate_PersonalWorks_RejectsSiteOnlyAllowsPool()
    {
        var workDir = CopyRegisterTs();
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            Assert.Contains(
                "gameDataPath",
                Evaluate(session, "game-dev", "grove-new", "新林", "game-dev/grove-new").Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "sitePath",
                Evaluate(session, "profile", "session-a", "场次甲", "profile/session-a").Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "该栏目本轮不登记空壳",
                Evaluate(session, "game-photo", "grove-shot", "截图", "game-photo/grove-shot").Message,
                StringComparison.Ordinal);
            Assert.True(Evaluate(session, "landscape-rendering", "grove-new", "新林", "landscape-rendering/grove-new").Allowed);
            Assert.Contains(
                "同 id",
                Evaluate(session, "landscape-rendering", "existing-grove", "已有", "landscape-rendering/grove-new").Message,
                StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_IsolatedCopy_BecomesRegisteredWithoutIngest()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归登记写盘。");

        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var original = File.ReadAllText(catalogPath);
            var session = WorkspaceSession.Load(profilePath);
            var document = IntentDocumentBuilder.BuildRegister(
                session,
                ExecutionMode.Direct,
                "demo-render",
                "new-folder",
                "新夹",
                "demo-render/new-folder");
            var report = PreviewReporter.BuildRegister(session, document);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);

            var intentPath = Path.Combine(workDir, "intent-register.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Contains(result.CompletedStepList, line => line.Contains("登记", StringComparison.Ordinal));

            var patched = File.ReadAllText(catalogPath);
            using var catalog = JsonDocument.Parse(patched);
            var works = catalog.RootElement.GetProperty("works");
            var registered = works.EnumerateArray().First(work =>
                work.GetProperty("id").GetString() == "new-folder"
                && work.GetProperty("channel").GetString() == "demo-render");
            Assert.Equal("新夹", registered.GetProperty("title").GetString());
            Assert.Equal(JsonValueKind.Array, registered.GetProperty("media").ValueKind);
            Assert.Equal(0, registered.GetProperty("media").GetArrayLength());
            Assert.Equal("demo-render/new-folder", registered.GetProperty("stageFolder").GetString());
            Assert.True(registered.TryGetProperty("year", out var yearEl));
            Assert.False(string.IsNullOrWhiteSpace(yearEl.GetString()));
            Assert.DoesNotContain("demo-render/new-folder/01.png", patched, StringComparison.Ordinal);

            var after = WorkspaceSession.Load(profilePath);
            var work = after.Works.First(item => item.Id == "new-folder" && item.Channel == "demo-render");
            Assert.False(work.IsUnregistered);
            Assert.Empty(work.Media);
            Assert.DoesNotContain(after.Works, item => item.Id == "new-folder" && item.IsUnregistered);
            Assert.Contains(UnregisteredWorkDiscovery.RegisteredInChannel(after.Works, "demo-render"), item => item.Id == "new-folder");

            var stage = after.StageItems.First(item =>
                item.StageRel.Replace('\\', '/').EndsWith("new-folder/01.png", StringComparison.OrdinalIgnoreCase));
            var ingest = IntentGate.EvaluateStage(
                MediaIntent.StageIngest,
                stage,
                new IntentContext
                {
                    Profile = after.Profile,
                    LedgerDict = after.LedgerDict,
                    ContentRefCountDict = after.ContentRefCountDict,
                    WorkList = after.Works,
                    TargetWorkId = "new-folder"
                });
            Assert.True(ingest.Allowed);
            Assert.DoesNotContain(after.SiteItems, item => item.WorkId == "new-folder");

            File.WriteAllText(catalogPath, original, JsonUtil.Utf8NoBom);
            var failRun = RunPatch(script!, intentPath, profilePath, "--apply", "--fail-after-write");
            Assert.False(failRun.Ok);
            Assert.Equal(original, File.ReadAllText(catalogPath));
            Assert.True(File.Exists(intentPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_DifferentId_HidesFolderViaStageFolder()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归登记写盘。");
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var session = WorkspaceSession.Load(profilePath);
            var document = IntentDocumentBuilder.BuildRegister(
                session,
                ExecutionMode.Direct,
                "demo-render",
                "demo-grove",
                "新林",
                "demo-render/new-folder");
            var intentPath = Path.Combine(workDir, "intent-grove.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);

            var after = WorkspaceSession.Load(profilePath);
            Assert.Contains(after.Works, item => item.Id == "demo-grove" && !item.IsUnregistered && item.Media.Count == 0);
            Assert.DoesNotContain(after.Works, item => item.Id == "new-folder" && item.IsUnregistered);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_RegisterWithSchedule_WritesStartedOnAndPlace()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归登记写盘。");
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var session = WorkspaceSession.Load(profilePath);
            var document = IntentDocumentBuilder.BuildRegister(
                session,
                ExecutionMode.Direct,
                "demo-render",
                "new-folder",
                "新夹",
                "demo-render/new-folder",
                "20250220",
                "无锡");
            var report = PreviewReporter.BuildRegister(session, document);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);
            Assert.Contains("2025-02-20", report.PatchPreview, StringComparison.Ordinal);
            Assert.Contains("无锡", report.PatchPreview, StringComparison.Ordinal);

            var intentPath = Path.Combine(workDir, "intent-register-meta.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);

            using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var registered = catalog.RootElement.GetProperty("works").EnumerateArray().First(work =>
                work.GetProperty("id").GetString() == "new-folder");
            Assert.Equal("2025", registered.GetProperty("year").GetString());
            Assert.Equal("2025-02-20", registered.GetProperty("startedOn").GetString());
            Assert.Equal("无锡", registered.GetProperty("place").GetString());
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_UpdateExistingWork_RewritesTitleStartedOnPlace()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归编辑写盘。");
        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var original = File.ReadAllText(catalogPath);
            var session = WorkspaceSession.Load(profilePath);
            var work = session.Works.First(item => item.Id == "demo-park" && item.Channel == "demo-render");
            var document = IntentDocumentBuilder.BuildUpdate(
                session,
                ExecutionMode.Direct,
                work,
                "演示公园改名",
                "2024-04-08",
                "上海");
            var report = PreviewReporter.BuildRegister(session, document);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);
            Assert.Equal(original, File.ReadAllText(catalogPath));

            var intentPath = Path.Combine(workDir, "intent-update.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);
            Assert.Contains(result.CompletedStepList, line => line.Contains("编辑项目", StringComparison.Ordinal));

            using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var updated = catalog.RootElement.GetProperty("works").EnumerateArray().First(item =>
                item.GetProperty("id").GetString() == "demo-park");
            Assert.Equal("演示公园改名", updated.GetProperty("title").GetString());
            Assert.Equal("2024-04-08", updated.GetProperty("startedOn").GetString());
            Assert.Equal("上海", updated.GetProperty("place").GetString());
            Assert.Equal("2024", updated.GetProperty("year").GetString());
            Assert.True(updated.TryGetProperty("media", out var media));
            Assert.True(media.GetArrayLength() > 0);

            var after = WorkspaceSession.Load(profilePath);
            var afterWork = after.Works.First(item => item.Id == "demo-park");
            Assert.Equal("演示公园改名", afterWork.Title);
            Assert.Equal("2024-04-08", afterWork.StartedOn);
            Assert.Equal("上海", afterWork.Place);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_PersonalWorksUpdate_WritesTsFields()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 works.ts 编辑。");
        var workDir = CopyRegisterTs();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var session = WorkspaceSession.Load(profilePath);
            var work = session.Works.First(item => item.Id == "existing-grove");
            var document = IntentDocumentBuilder.BuildUpdate(
                session,
                ExecutionMode.Direct,
                work,
                "已有林改名",
                "2024-06-01",
                "杭州");
            var intentPath = Path.Combine(workDir, "intent-update-ts.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);
            var patched = File.ReadAllText(worksPath);
            Assert.Contains("title: \"已有林改名\"", patched, StringComparison.Ordinal);
            Assert.Contains("startedOn: \"2024-06-01\"", patched, StringComparison.Ordinal);
            Assert.Contains("place: \"杭州\"", patched, StringComparison.Ordinal);
            Assert.Contains("year: \"2024\"", patched, StringComparison.Ordinal);
            Assert.Contains("consent: \"pending\"", patched, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void RealWorksTs_RegisteredHookStaysEmpty()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var profile = WorkspaceProfileLoader.Load(profilePath!);
        var worksPath = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.Path);
        var text = File.ReadAllText(worksPath);
        Assert.Contains("const registeredWorks: WorkRecord[]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("id: \"grove-new\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_PersonalWorksIsolated_WritesPendingShell()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归 works.ts 登记。");
        var workDir = CopyRegisterTs();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var original = File.ReadAllText(worksPath);
            var session = WorkspaceSession.Load(profilePath);
            var document = IntentDocumentBuilder.BuildRegister(
                session,
                ExecutionMode.Direct,
                "landscape-rendering",
                "grove-new",
                "新林",
                "landscape-rendering/grove-new");
            var report = PreviewReporter.BuildRegister(session, document);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);
            Assert.Contains("consent: \"pending\"", report.PatchPreview, StringComparison.Ordinal);
            Assert.Contains("media: []", report.PatchPreview, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllText(worksPath));

            var intentPath = Path.Combine(workDir, "intent-register-ts.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);

            var patched = File.ReadAllText(worksPath);
            Assert.Contains("id: \"grove-new\"", patched, StringComparison.Ordinal);
            Assert.Contains("consent: \"pending\"", patched, StringComparison.Ordinal);
            Assert.Contains("stageFolder: \"landscape-rendering/grove-new\"", patched, StringComparison.Ordinal);
            Assert.DoesNotContain("landscape-rendering/grove-new/01.png", patched, StringComparison.Ordinal);

            var after = WorkspaceSession.Load(profilePath);
            var work = after.Works.First(item => item.Id == "grove-new" && item.Channel == "landscape-rendering");
            Assert.False(work.IsUnregistered);
            Assert.Empty(work.Media);
            Assert.DoesNotContain(after.Works, item => item.Id == "grove-new" && item.IsUnregistered);

            File.WriteAllText(worksPath, original, JsonUtil.Utf8NoBom);
            var failRun = RunPatch(ContentPatchClient.FindScript()!, intentPath, profilePath, "--apply", "--fail-after-write");
            Assert.False(failRun.Ok);
            Assert.Equal(original, File.ReadAllText(worksPath));
            Assert.True(File.Exists(intentPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_RegisteredWorksEmpty_RestoreWritesSrc()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归空壳恢复写盘。");
        var workDir = CopyRegisterTs();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var worksPath = Path.Combine(workDir, "works.ts");
            var intentPath = Path.Combine(workDir, "intent-restore-shell.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "site.restore",
                      "workId": "existing-grove",
                      "channel": "landscape-rendering",
                      "object": "landscape-rendering/existing-grove/01.webp"
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(ContentPatchClient.FindScript()!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(worksPath);
            var groveFrom = patched.IndexOf("id: \"existing-grove\"", StringComparison.Ordinal);
            Assert.True(groveFrom >= 0);
            var groveBlock = patched.Substring(groveFrom, Math.Min(480, patched.Length - groveFrom));
            Assert.Contains("landscape-rendering/existing-grove/01.webp", groveBlock, StringComparison.Ordinal);
            Assert.Contains("consent: \"granted\"", groveBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("consent: \"pending\"", groveBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("media: []", groveBlock, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_ProfileFolder_SkipsRegisterAndIngestsIntoFlatPortraitSrcs()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归形象照上页。");
        var workDir = CopyRegisterTsWithSiteHooks();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var sitePath = Path.Combine(workDir, "site.ts");
            var worksPath = Path.Combine(workDir, "works.ts");
            var originalWorks = File.ReadAllText(worksPath);
            var session = WorkspaceSession.Load(profilePath);
            Assert.DoesNotContain(session.Works, item => item.IsUnregistered && item.Channel == "profile");
            Assert.Contains(session.Works, item => item.Channel == "profile" && !item.IsUnregistered && item.Id == "portrait");
            var register = IntentDocumentBuilder.BuildRegister(
                session,
                ExecutionMode.Direct,
                "profile",
                "session-a",
                "场次甲",
                "profile/session-a");
            var registerReport = PreviewReporter.BuildRegister(session, register);
            Assert.True(registerReport.HasHardError);
            Assert.Contains("同 id", registerReport.Lines[0].Decision.Message, StringComparison.Ordinal);

            var ingestPath = Path.Combine(workDir, "intent-profile-ingest.json");
            File.WriteAllText(
                ingestPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "stage.ingest",
                      "workId": "portrait",
                      "channel": "profile",
                      "object": "profile/session-a/01.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);
            var ingest = RunPatch(ContentPatchClient.FindScript()!, ingestPath, profilePath, "--apply");
            Assert.True(ingest.Ok, ingest.StdOut + ingest.StdErr);

            var patched = File.ReadAllText(sitePath);
            Assert.Contains("portraitSrc: \"profile/portrait.webp\"", patched, StringComparison.Ordinal);
            Assert.Contains("profile/portrait.webp", patched, StringComparison.Ordinal);
            Assert.Contains("profile/session-a/01.webp", patched, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(patched, "portraitSrcs:"));
            Assert.DoesNotContain("id: \"session-a\"", patched, StringComparison.Ordinal);
            Assert.Equal(originalWorks, File.ReadAllText(worksPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Apply_GameShell_RegistersEmptyAndAcceptsScreenshotIngest()
    {
        Assert.True(HasNode(), "本机需要 node 才能回归游戏空壳登记。");
        var workDir = CopyRegisterTsWithSiteHooks();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var gamesPath = Path.Combine(workDir, "games.ts");
            var worksPath = Path.Combine(workDir, "works.ts");
            var originalWorks = File.ReadAllText(worksPath);
            var session = WorkspaceSession.Load(profilePath);
            Assert.Contains(session.Works, item => item.IsUnregistered && item.Channel == "game-dev" && item.Id == "grove-play");
            var document = IntentDocumentBuilder.BuildRegister(
                session,
                ExecutionMode.Direct,
                "game-dev",
                "grove-play",
                "林间",
                "game-dev/grove-play");
            var report = PreviewReporter.BuildRegister(session, document);
            Assert.False(report.HasHardError, report.Lines[0].Decision.Message);
            Assert.Contains("screenshots: []", report.PatchPreview, StringComparison.Ordinal);
            Assert.Contains("consent: \"pending\"", report.PatchPreview, StringComparison.Ordinal);

            var intentPath = Path.Combine(workDir, "intent-game-register.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var result = BatchExecutor.Run(session, document, intentPath);
            Assert.True(result.Ok, result.FailureText);

            var gamesAfterRegister = File.ReadAllText(gamesPath);
            Assert.Contains("screenshots: Array<[string, string]>", gamesAfterRegister, StringComparison.Ordinal);
            Assert.Contains("id: \"grove-play\"", gamesAfterRegister, StringComparison.Ordinal);
            Assert.Contains("screenshots: []", gamesAfterRegister, StringComparison.Ordinal);
            Assert.DoesNotContain("Array<[string, string,\n  {", gamesAfterRegister, StringComparison.Ordinal);
            Assert.DoesNotContain("grove-play", File.ReadAllText(worksPath), StringComparison.Ordinal);
            Assert.Equal(originalWorks, File.ReadAllText(worksPath));

            var afterRegister = WorkspaceSession.Load(profilePath);
            var shell = afterRegister.Works.First(item => item.Id == "grove-play" && item.Channel == "game-dev");
            Assert.False(shell.IsUnregistered);
            Assert.Empty(shell.Media);

            var ingestPath = Path.Combine(workDir, "intent-game-ingest.json");
            File.WriteAllText(
                ingestPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "stage.ingest",
                      "workId": "grove-play",
                      "channel": "game-dev",
                      "object": "game-dev/grove-play/01.webp"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);
            var ingest = RunPatch(ContentPatchClient.FindScript()!, ingestPath, profilePath, "--apply");
            Assert.True(ingest.Ok, ingest.StdOut + ingest.StdErr);

            var patched = File.ReadAllText(gamesPath);
            Assert.Contains("game-dev/grove-play/01.webp", patched, StringComparison.Ordinal);
            Assert.DoesNotContain("screenshots: []", patched, StringComparison.Ordinal);
            Assert.Equal(originalWorks, File.ReadAllText(worksPath));

            var afterIngest = WorkspaceSession.Load(profilePath);
            var filled = afterIngest.Works.First(item => item.Id == "grove-play" && item.Channel == "game-dev");
            Assert.Contains(filled.Media, item => item.Src == "game-dev/grove-play/01.webp");
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_DryRun_DoesNotWriteCatalog()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归登记预演。");

        var workDir = CopySampleSite();
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var original = File.ReadAllText(catalogPath);
            var session = WorkspaceSession.Load(profilePath);
            var document = IntentDocumentBuilder.BuildRegister(
                session,
                ExecutionMode.Prompt,
                "demo-render",
                "new-folder",
                "新夹",
                "demo-render/new-folder");
            var intentPath = Path.Combine(workDir, "intent-dry.json");
            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);
            var dry = RunPatch(script!, intentPath, profilePath, "--dry-run");
            Assert.True(dry.Ok, dry.StdOut + dry.StdErr);
            Assert.Contains("new-folder", dry.StdOut, StringComparison.Ordinal);
            Assert.Contains("\"kindBefore\": \"none\"", dry.StdOut, StringComparison.Ordinal);
            Assert.Contains("\\\"media\\\": []", dry.StdOut, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllText(catalogPath));
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
    /// 组装登记闸门判定。
    /// </summary>
    private static GateDecision Evaluate(
        WorkspaceSession session,
        string channel,
        string workId,
        string title,
        string stageFolder)
    {
        return IntentGate.EvaluateRegister(
            new IntentItem
            {
                Intent = MediaIntentCodes.WorkRegister,
                Channel = channel,
                WorkId = workId,
                Title = title,
                StageFolder = stageFolder
            },
            Context(session, session.Profile));
    }

    /// <summary>
    /// 登记闸门上下文。
    /// </summary>
    private static IntentContext Context(WorkspaceSession session, WorkspaceProfile profile)
    {
        return new IntentContext
        {
            Profile = profile,
            LedgerDict = session.LedgerDict,
            ContentRefCountDict = session.ContentRefCountDict,
            WorkList = session.Works
        };
    }

    /// <summary>
    /// 只改编目类型，供拒绝 PersonalWorks 路径。
    /// </summary>
    private static WorkspaceProfile CloneJsonKind(WorkspaceProfile source, string kind)
    {
        return new WorkspaceProfile
        {
            Version = source.Version,
            Name = source.Name,
            Root = source.Root,
            StageRoot = source.StageRoot,
            PlaceholdersRoot = source.PlaceholdersRoot,
            LedgerPath = source.LedgerPath,
            SiteCatalog = new SiteCatalogConfig
            {
                Kind = kind,
                Path = source.SiteCatalog.Path
            },
            Channels = source.Channels,
            ResolvedRoot = source.ResolvedRoot,
            ProfilePath = source.ProfilePath
        };
    }

    /// <summary>
    /// 拷贝模拟站正本到隔离盘。
    /// </summary>
    private static string CopySampleSite()
    {
        var source = Path.GetDirectoryName(ToolPaths.FindFixtureProfile())!;
        var dest = Path.Combine(Path.GetTempPath(), "sms-register-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(source, dest);
        return dest;
    }

    /// <summary>
    /// 拷贝 works.ts 登记夹具，并补一枚投放箱色块。
    /// </summary>
    private static string CopyRegisterTs()
    {
        var source = Path.GetDirectoryName(ToolPaths.FindRegisterTsProfile())!;
        var dest = Path.Combine(Path.GetTempPath(), "sms-register-ts-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(source, dest);
        var stageDir = Path.Combine(dest, "stage", "landscape-rendering", "grove-new");
        Directory.CreateDirectory(stageDir);
        WriteTinyPng(Path.Combine(stageDir, "01.png"));
        return dest;
    }

    /// <summary>
    /// 拷贝登记夹具并补形象 / 游戏钩子与未登记夹。
    /// </summary>
    private static string CopyRegisterTsWithSiteHooks()
    {
        var dest = CopyRegisterTs();
        var profilePath = Path.Combine(dest, "profile.json");
        var json = JsonNode.Parse(File.ReadAllText(profilePath))!.AsObject();
        var catalog = json["siteCatalog"]!.AsObject();
        catalog["sitePath"] = "site.ts";
        catalog["gameDataPath"] = "games.ts";
        File.WriteAllText(
            profilePath,
            json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(dest, "site.ts"),
            """
            export const profile = {
              name: "测试",
              portraitSrc: "profile/portrait.webp",
              portraitSrcs: [
                "profile/portrait.webp",
              ],
            };

            export const registeredProfileSessions = [];

            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(dest, "games.ts"),
            """
            export const initialGameProjects = [];

            export const registeredGames: Array<{
              id: string;
              title: string;
              titleEn: string;
              lead: string;
              playable: boolean;
              consent?: string;
              stageFolder?: string;
              screenshots: Array<[string, string]>;
            }> = [];

            """,
            JsonUtil.Utf8NoBom);
        WriteTinyPng(Path.Combine(dest, "stage", "profile", "session-a", "01.png"));
        WriteTinyPng(Path.Combine(dest, "stage", "game-dev", "grove-play", "01.png"));
        return dest;
    }

    /// <summary>
    /// 写入一枚最小 PNG，供投放箱发现未登记夹。
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
    /// 统计子串出现次数。
    /// </summary>
    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var index = text.IndexOf(value, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            start = index + value.Length;
        }
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
    /// 调用 content-patch.mjs。
    /// </summary>
    private static NodeRunResult RunPatch(string script, string intentPath, string profilePath, params string[] extraArgs)
    {
        var argumentList = new List<string>
        {
            "--intent",
            intentPath,
            "--profile",
            profilePath
        };
        argumentList.AddRange(extraArgs);
        return NodeHost.Run(script, argumentList, Path.GetDirectoryName(script)!);
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
