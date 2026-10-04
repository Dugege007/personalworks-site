using System.Diagnostics;
using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class CopyUpdateTests
{
    [Fact]
    public void Gate_ProfileWithDescription_IsRejected()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var profileWork = session.Works.First(item => item.Channel == "profile" && !item.IsUnregistered);
        var context = new IntentContext
        {
            LedgerDict = session.LedgerDict,
            ContentRefCountDict = session.ContentRefCountDict,
            Profile = session.Profile,
            WorkList = session.Works
        };
        var decision = IntentGate.EvaluateCopy(
            new IntentItem
            {
                Intent = MediaIntentCodes.CopyUpdate,
                Target = "work",
                Channel = "profile",
                WorkId = profileWork.Id,
                Description = "形象不该有描述"
            },
            context);
        Assert.False(decision.Allowed);
        Assert.Contains("形象", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_UnknownChannel_IsRejected()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var decision = IntentGate.EvaluateCopy(
            new IntentItem
            {
                Intent = MediaIntentCodes.CopyUpdate,
                Target = "channel",
                Channel = "not-a-channel",
                Description = "x"
            },
            new IntentContext
            {
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                Profile = session.Profile,
                WorkList = session.Works
            });
        Assert.False(decision.Allowed);
        Assert.Contains("未知频道", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_SampleMediaCopy_AllowsAndDescribesVisibility()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var document = new IntentDocument
        {
            Mode = "direct",
            Items =
            {
                new IntentItem
                {
                    Intent = MediaIntentCodes.CopyUpdate,
                    Target = "media",
                    Channel = "demo-render",
                    WorkId = "demo-park",
                    Object = "demo-render/demo-park/01.png",
                    Title = "公园入口",
                    Description = "资源说明。"
                }
            }
        };
        var report = PreviewReporter.BuildFromDocument(session, document);
        Assert.False(report.HasHardError);
        Assert.Contains("自定义名", report.Lines[0].Decision.Message, StringComparison.Ordinal);
        Assert.Contains("displayName", report.PatchPreview, StringComparison.Ordinal);
    }

    [Fact]
    public void Compiler_BuildsMediaKeyAndItems()
    {
        var profile = new WorkspaceProfile
        {
            ProfilePath = @"D:\ws\profile.json",
            ResolvedRoot = @"D:\ws"
        };
        var file = new CopyDraftFile
        {
            Version = 1,
            ProfilePath = profile.ProfilePath,
            WorkspaceRoot = profile.ResolvedRoot,
            Entries =
            {
                new CopyDraftEntry
                {
                    Key = CopyDraftStore.MediaKey("demo-render", "demo-park", "demo-render/demo-park/01.png"),
                    Title = "公园入口",
                    Description = "资源说明。"
                }
            }
        };
        Assert.True(CopyDraftCompiler.TryParseKey(file.Entries[0].Key, out var target, out var channel, out var workId, out var objectKey));
        Assert.Equal("media", target);
        Assert.Equal("demo-render", channel);
        Assert.Equal("demo-park", workId);
        Assert.Equal("demo-render/demo-park/01.png", objectKey);

        var session = new WorkspaceSession
        {
            Profile = profile,
            Works = new[]
            {
                new WorkCatalogItem
                {
                    Id = "demo-park",
                    Channel = "demo-render",
                    Title = "演示公园",
                    Media = new[]
                    {
                        new WorkMediaItem { Src = "demo-render/demo-park/01.png", Label = "效果图 01" }
                    }
                }
            }
        };
        var items = CopyDraftCompiler.ToItems(session, file);
        Assert.Single(items);
        Assert.Equal(MediaIntentCodes.CopyUpdate, items[0].Intent);
        Assert.Equal("公园入口", items[0].Title);
        Assert.Equal(file.Entries[0].Key, CopyDraftCompiler.KeyOf(items[0]));
    }

    [Fact]
    public void Script_SampleMediaCopy_WritesOptionalFieldsAndRestoresOnFailure()
    {
        var sourceProfile = ToolPaths.FindFixtureProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyDirectory(Path.GetDirectoryName(sourceProfile!)!);
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var original = File.ReadAllText(catalogPath);
            var intentPath = Path.Combine(workDir, "copy-media.json");
            File.WriteAllText(intentPath, MediaCopyIntent(), JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(catalogPath);
            Assert.Contains("\"displayName\": \"公园入口\"", patched, StringComparison.Ordinal);
            Assert.Contains("\"description\": \"资源说明。\"", patched, StringComparison.Ordinal);
            Assert.Contains("\"label\": \"效果图 01\"", patched, StringComparison.Ordinal);
            Assert.Contains("\"src\": \"demo-render/demo-park/01.png\"", patched, StringComparison.Ordinal);
            Assert.Contains("\"id\": \"demo-park\"", patched, StringComparison.Ordinal);

            File.WriteAllText(catalogPath, original, JsonUtil.Utf8NoBom);
            var fail = RunPatch(script!, intentPath, profilePath, "--apply", "--fail-after-write");
            Assert.False(fail.Ok);
            Assert.Equal(original, File.ReadAllText(catalogPath));
            Assert.True(File.Exists(intentPath));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_SampleClearWorkAndChannel_LeavesEmpty()
    {
        var sourceProfile = ToolPaths.FindFixtureProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(sourceProfile);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = CopyDirectory(Path.GetDirectoryName(sourceProfile!)!);
        try
        {
            var profilePath = Path.Combine(workDir, "profile.json");
            var catalogPath = Path.Combine(workDir, "content", "catalog.json");
            var intentPath = Path.Combine(workDir, "copy-clear.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "copy.update",
                      "target": "work",
                      "channel": "demo-render",
                      "workId": "demo-park",
                      "description": ""
                    },
                    {
                      "intent": "copy.update",
                      "target": "channel",
                      "channel": "demo-photo",
                      "description": ""
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var session = WorkspaceSession.Load(profilePath);
            var park = Assert.Single(session.Works, item => item.Id == "demo-park");
            Assert.False(CopyText.IsFilled(park.Summary));
            Assert.False(CopyText.IsFilled(session.ChannelLeadDict["demo-photo"]));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_PersonalWorksIsolation_UpdatesSummaryKeepsIdAndLabels()
    {
        var personal = ToolPaths.FindPersonalWorksProfile();
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(personal);
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var sourceProfile = WorkspaceProfileLoader.Load(personal!);
        var workData = WorkspaceProfileLoader.ResolveUnderRoot(sourceProfile, sourceProfile.SiteCatalog.WorkDataPath!);
        var worksPath = WorkspaceProfileLoader.ResolveUnderRoot(sourceProfile, sourceProfile.SiteCatalog.Path);
        var workDir = Path.Combine(Path.GetTempPath(), "sms-copy-pw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.Copy(workData, Path.Combine(workDir, "initialWorkProjects.ts"));
            File.Copy(worksPath, Path.Combine(workDir, "works.ts"));
            var profilePath = Path.Combine(workDir, "profile.json");
            File.WriteAllText(
                profilePath,
                """
                {
                  "version": 1,
                  "name": "copy-iso",
                  "root": ".",
                  "stageRoot": "stage",
                  "placeholdersRoot": "placeholders",
                  "siteCatalog": {
                    "kind": "personalworks-ts",
                    "path": "works.ts",
                    "workDataPath": "initialWorkProjects.ts"
                  },
                  "channels": [
                    { "key": "landscape-rendering", "zh": "景观效果图", "en": "Landscape Rendering", "deco": "L. RENDER" }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);
            var original = File.ReadAllText(Path.Combine(workDir, "initialWorkProjects.ts"));
            var intentPath = Path.Combine(workDir, "copy-summary.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "copy.update",
                      "target": "work",
                      "channel": "landscape-rendering",
                      "workId": "xiaowayao",
                      "description": "隔离副本项目描述。"
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(Path.Combine(workDir, "initialWorkProjects.ts"));
            var block = ExtractXiaowayao(patched);
            Assert.Contains("\"id\": \"xiaowayao\"", block, StringComparison.Ordinal);
            Assert.Contains("\"summary\": \"隔离副本项目描述。\"", block, StringComparison.Ordinal);
            Assert.Contains("\"body\": \"北京世茂丰台小瓦窑样板庭院。南院为下叠花园，北院为上叠花园。方案按六口之家模拟业主日常：手工、观天、书画与儿童活动。空间语言先走日式禅意，再落到现代东方的聚会与收藏。\"", block, StringComparison.Ordinal);
            Assert.Contains("landscape-rendering/xiaowayao/01", block, StringComparison.Ordinal);
            Assert.Equal(CountLabels(original, "xiaowayao"), CountLabels(patched, "xiaowayao"));
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_ChannelLead_SkipsNavIdAndPatchesCategoryLead()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = Path.Combine(Path.GetTempPath(), "sms-copy-nav-lead-" + Guid.NewGuid().ToString("N"));
        var lexiconDir = Path.Combine(workDir, "PersonalSite", "src", "content");
        Directory.CreateDirectory(lexiconDir);
        try
        {
            File.WriteAllText(
                Path.Combine(lexiconDir, "lexicon.ts"),
                """
                export const lexicon = {
                  gameDev: { zh: "游戏开发", en: "Game Development", deco: "GAME DEV", key: "game-dev" },
                  landscapeRendering: { zh: "景观效果图", en: "Landscape Rendering", deco: "L. RENDER", key: "landscape-rendering" },
                };
                """,
                JsonUtil.Utf8NoBom);
            var pad = new string('x', 1300);
            File.WriteAllText(
                Path.Combine(workDir, "site.ts"),
                $$"""
                export const navItems = [
                  {
                    id: lexicon.gameDev.key,
                    label: lexicon.gameDev.zh,
                    path: `/${lexicon.gameDev.key}`,
                  },
                ];
                const navPad = "{{pad}}";
                export const categories = [
                  {
                    id: lexicon.gameDev.key,
                    title: lexicon.gameDev.zh,
                    lead: "旧游戏导语",
                    collections: [
                      {
                        id: lexicon.landscapeRendering.key,
                        title: lexicon.landscapeRendering.zh,
                        lead: "旧效果图导语",
                      },
                    ],
                  },
                ];
                """,
                JsonUtil.Utf8NoBom);
            File.WriteAllText(
                Path.Combine(workDir, "works.ts"),
                "export const registeredWorks = [];\n",
                JsonUtil.Utf8NoBom);
            var profilePath = Path.Combine(workDir, "profile.json");
            File.WriteAllText(
                profilePath,
                """
                {
                  "version": 1,
                  "name": "nav-lead",
                  "root": ".",
                  "stageRoot": "stage",
                  "placeholdersRoot": "placeholders",
                  "siteCatalog": {
                    "kind": "personalworks-ts",
                    "path": "works.ts",
                    "sitePath": "site.ts"
                  }
                }
                """,
                JsonUtil.Utf8NoBom);
            var intentPath = Path.Combine(workDir, "copy-channel.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "copy.update",
                      "target": "channel",
                      "channel": "game-dev",
                      "description": "新游戏导语"
                    },
                    {
                      "intent": "copy.update",
                      "target": "channel",
                      "channel": "landscape-rendering",
                      "description": "新效果图导语"
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(Path.Combine(workDir, "site.ts"));
            Assert.Contains("lead: \"新游戏导语\"", patched, StringComparison.Ordinal);
            Assert.Contains("lead: \"新效果图导语\"", patched, StringComparison.Ordinal);
            Assert.DoesNotContain("旧游戏导语", patched, StringComparison.Ordinal);
            Assert.DoesNotContain("旧效果图导语", patched, StringComparison.Ordinal);
            Assert.Contains("label: lexicon.gameDev.zh", patched, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_GameWorkCopy_MatchesLeadWithoutChannelField()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = Path.Combine(Path.GetTempPath(), "sms-copy-game-lead-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "works.ts"),
                "export const registeredWorks = [];\n",
                JsonUtil.Utf8NoBom);
            File.WriteAllText(
                Path.Combine(workDir, "initialGameProjects.ts"),
                """
                export const initialGameProjects = [
                  {
                    "id": "antigravity",
                    "title": "反重力",
                    "lead": "旧描述",
                    "screenshots": []
                  },
                  {
                    "id": "classic-games",
                    "title": "经典游戏",
                    "lead": "保持不变",
                    "screenshots": []
                  }
                ];
                """,
                JsonUtil.Utf8NoBom);
            var profilePath = Path.Combine(workDir, "profile.json");
            File.WriteAllText(
                profilePath,
                """
                {
                  "version": 1,
                  "name": "game-lead",
                  "root": ".",
                  "stageRoot": "stage",
                  "placeholdersRoot": "placeholders",
                  "siteCatalog": {
                    "kind": "personalworks-ts",
                    "path": "works.ts",
                    "gameDataPath": "initialGameProjects.ts"
                  }
                }
                """,
                JsonUtil.Utf8NoBom);
            var intentPath = Path.Combine(workDir, "copy-game.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "copy.update",
                      "target": "work",
                      "channel": "game-dev",
                      "workId": "antigravity",
                      "description": "新描述"
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);

            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = File.ReadAllText(Path.Combine(workDir, "initialGameProjects.ts"));
            Assert.Contains("\"lead\": \"新描述\"", patched, StringComparison.Ordinal);
            Assert.Contains("\"lead\": \"保持不变\"", patched, StringComparison.Ordinal);
            Assert.DoesNotContain("旧描述", patched, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void DisplayRules_EmptyFallsBack()
    {
        Assert.Equal("效果图 01", CopyDisplayRules.ResolveMediaDisplayName("", "效果图 01"));
        Assert.Equal("公园入口", CopyDisplayRules.ResolveMediaDisplayName("公园入口", "效果图 01"));
        Assert.Equal("项目说明。", CopyDisplayRules.ResolveMediaDescription("", "项目说明。"));
        Assert.Equal("", CopyDisplayRules.ResolveMediaDescription("", ""));
        Assert.Equal("", CopyDisplayRules.ResolveMediaDescription(CopyText.Placeholder, CopyText.Placeholder));
    }

    private static string MediaCopyIntent()
    {
        return JsonSerializer.Serialize(new
        {
            version = 1,
            mode = "direct",
            items = new[]
            {
                new
                {
                    intent = "copy.update",
                    target = "media",
                    channel = "demo-render",
                    workId = "demo-park",
                    @object = "demo-render/demo-park/01.png",
                    title = "公园入口",
                    description = "资源说明。"
                }
            },
            options = new { relabel = true, deploy = "none" }
        }, JsonUtil.Options);
    }

    private static string CopyDirectory(string source)
    {
        var dest = Path.Combine(Path.GetTempPath(), "sms-copy-fix-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return dest;
    }

    private static NodeRunResult RunPatch(string script, string intentPath, string profilePath, params string[] extraArgs)
    {
        var argumentList = new List<string> { "--intent", intentPath, "--profile", profilePath };
        argumentList.AddRange(extraArgs);
        return NodeHost.Run(script, argumentList, Path.GetDirectoryName(script)!);
    }

    private static string ExtractXiaowayao(string text)
    {
        var start = text.IndexOf("\"id\": \"xiaowayao\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var next = text.IndexOf("\"id\": \"", start + 10, StringComparison.Ordinal);
        return next < 0 ? text[start..] : text[start..next];
    }

    private static int CountLabels(string text, string workId)
    {
        return ExtractXiaowayao(text).Split("\"label\"", StringSplitOptions.None).Length
            + ExtractXiaowayao(text).Split(workId + "/", StringSplitOptions.None).Length;
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
}
