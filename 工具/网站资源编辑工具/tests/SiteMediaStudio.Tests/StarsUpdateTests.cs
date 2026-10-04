using System.Diagnostics;
using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class StarsUpdateTests
{
    [Fact]
    public void Fixture_ReadsDifferentStarsAndTreatsMissingAsZero()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var park = Assert.Single(session.Works, item => item.Id == "demo-park");
        var walk = Assert.Single(session.Works, item => item.Id == "demo-walk");
        Assert.Equal(4, park.Media[0].Stars);
        Assert.Equal(1, park.Media[1].Stars);
        Assert.Null(walk.Media[0].Stars);
        Assert.Equal(4, session.SiteItems.Single(item => item.ObjectKey == "demo-render/demo-park/01.png").Stars);
        Assert.Equal(1, session.SiteItems.Single(item => item.ObjectKey == "demo-render/demo-park/02.png").Stars);
        Assert.Equal(0, session.SiteItems.Single(item => item.ObjectKey == "demo-photo/demo-walk/01.png").Stars);
    }

    [Fact]
    public void Draft_EqualToPublished_DeletesTheEntry()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-stars-" + Guid.NewGuid().ToString("N") + ".json");
        var profile = new WorkspaceProfile
        {
            ProfilePath = @"D:\ws\profile.json",
            ResolvedRoot = @"D:\ws"
        };
        try
        {
            StarDraftStore.Commit(profile, StarDraftStore.ObjectKey("demo/a.webp"), 3, 0, path);
            var kept = StarDraftStore.Load(path);
            Assert.Single(kept.Entries);
            StarDraftStore.Commit(profile, StarDraftStore.ObjectKey("demo/a.webp"), 0, 0, path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Gate_AllowsCataloguedStar_AndDefersUnpairedStage()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var context = new IntentContext
        {
            LedgerDict = session.LedgerDict,
            ContentRefCountDict = session.ContentRefCountDict,
            Profile = session.Profile,
            WorkList = session.Works
        };
        var allow = IntentGate.EvaluateStars(
            new IntentItem
            {
                Intent = MediaIntentCodes.StarsUpdate,
                Channel = "demo-render",
                WorkId = "demo-park",
                Object = "demo-render/demo-park/02.png",
                Stars = 3
            },
            context);
        Assert.True(allow.Allowed);
        Assert.Contains("3 星", allow.Message, StringComparison.Ordinal);

        var clear = IntentGate.EvaluateStars(
            new IntentItem
            {
                Intent = MediaIntentCodes.StarsUpdate,
                Channel = "demo-render",
                WorkId = "demo-park",
                Object = "demo-render/demo-park/01.png",
                Stars = 0
            },
            context);
        Assert.True(clear.Allowed);
        Assert.Contains("清除星级", clear.Message, StringComparison.Ordinal);

        var stage = IntentGate.EvaluateStars(
            new IntentItem
            {
                Intent = MediaIntentCodes.StarsUpdate,
                StageRel = "studio-a/raw.webp",
                Stars = 4
            },
            context);
        Assert.True(stage.Allowed);
        Assert.Contains("上页时将", stage.Message, StringComparison.Ordinal);
        Assert.Contains("4 星", stage.Message, StringComparison.Ordinal);

        var bounds = IntentGate.EvaluateStars(
            new IntentItem
            {
                Intent = MediaIntentCodes.StarsUpdate,
                Channel = "demo-render",
                WorkId = "demo-park",
                Object = "demo-render/demo-park/02.png",
                Stars = 6
            },
            context);
        Assert.False(bounds.Allowed);
    }

    [Fact]
    public void Compiler_EmitsObjectIntent()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var file = new StarDraftFile
        {
            ProfilePath = session.Profile.ProfilePath,
            WorkspaceRoot = session.Profile.ResolvedRoot,
            Entries =
            {
                new StarDraftEntry
                {
                    Key = StarDraftStore.ObjectKey("demo-render/demo-park/02.png"),
                    Stars = 3,
                    PublishedStars = 0
                }
            }
        };
        var items = StarDraftCompiler.ToItems(session, file);
        var item = Assert.Single(items);
        Assert.Equal(MediaIntentCodes.StarsUpdate, item.Intent);
        Assert.Equal("demo-park", item.WorkId);
        Assert.Equal(3, item.Stars);
        Assert.Equal(file.Entries[0].Key, StarDraftCompiler.KeyOf(item));
    }

    [Fact]
    public void Script_WritesThreeStarsThenClearsTheField()
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
            var intentPath = Path.Combine(workDir, "stars.json");
            File.WriteAllText(intentPath, StarIntent(3), JsonUtil.Utf8NoBom);
            var apply = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var patched = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var media = FindMedia(patched, "demo-render/demo-park/02.png");
            Assert.Equal(3, media.GetProperty("stars").GetInt32());
            Assert.Equal("效果图 02", media.GetProperty("label").GetString());
            Assert.Equal("demo-render/demo-park/02.png", media.GetProperty("src").GetString());
            Assert.Equal(4, FindMedia(patched, "demo-render/demo-park/01.png").GetProperty("stars").GetInt32());

            File.WriteAllText(intentPath, StarIntent(0), JsonUtil.Utf8NoBom);
            var clear = RunPatch(script!, intentPath, profilePath, "--apply");
            Assert.True(clear.Ok, clear.StdOut + clear.StdErr);
            var cleared = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var gone = FindMedia(cleared, "demo-render/demo-park/02.png");
            Assert.False(gone.TryGetProperty("stars", out _));
            Assert.DoesNotContain("\"stars\": 0", File.ReadAllText(catalogPath), StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_GameRewriteKeepsStars()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = Path.Combine(Path.GetTempPath(), "sms-stars-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "games.ts"),
                """
                export const initialGameProjects = [
                  {
                    id: "demo-game",
                    title: "演示",
                    screenshots: [
                      ["game-dev/demo-game/01.webp", "开场"],
                      { "kind": "image", "label": "结算", "src": "game-dev/demo-game/02.webp", "stars": 4 }
                    ]
                  }
                ];
                """);
            File.WriteAllText(
                Path.Combine(workDir, "profile.json"),
                """
                {
                  "version": 1,
                  "name": "游戏星级",
                  "stageRoot": "stage",
                  "placeholdersRoot": "public/placeholders",
                  "ledgerPath": "content/media-ledger.json",
                  "siteCatalog": { "kind": "personalworks-ts", "path": "works.ts", "gameDataPath": "games.ts" },
                  "channels": [
                    { "key": "game-dev", "zh": "游戏开发", "en": "Game Development", "deco": "GAME DEV" }
                  ]
                }
                """);
            File.WriteAllText(Path.Combine(workDir, "works.ts"), "export const registeredWorks = [];\n");
            var intentPath = Path.Combine(workDir, "hide.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "demo-game",
                      "channel": "game-dev",
                      "object": "game-dev/demo-game/01.webp"
                    }
                  ]
                }
                """);
            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var text = File.ReadAllText(Path.Combine(workDir, "games.ts"));
            Assert.Contains("game-dev/demo-game/02.webp", text, StringComparison.Ordinal);
            Assert.Contains("\"stars\":4", text, StringComparison.Ordinal);
            Assert.DoesNotContain("game-dev/demo-game/01.webp", text, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_HideParksStarsAndSiteCardKeepsThem()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = Path.Combine(Path.GetTempPath(), "sms-stars-park-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "games.ts"),
                """
                export const initialGameProjects = [
                  {
                    id: "demo-game",
                    title: "演示",
                    screenshots: [
                      ["game-dev/demo-game/01.webp", "开场"],
                      { "kind": "image", "label": "结算", "src": "game-dev/demo-game/02.webp", "stars": 4 }
                    ]
                  }
                ];
                """);
            File.WriteAllText(Path.Combine(workDir, "works.ts"), "export const registeredWorks = [];\n");
            File.WriteAllText(
                Path.Combine(workDir, "site.ts"),
                """
                export const profile = {
                  portraitSrc: "profile/portrait.webp",
                  portraitSrcs: ["profile/portrait.webp"],
                };
                """);
            File.WriteAllText(
                Path.Combine(workDir, "ledger.json"),
                """
                {
                  "records": [
                    { "object": "game-dev/demo-game/01.webp", "status": "published" },
                    { "object": "game-dev/demo-game/02.webp", "status": "published" }
                  ]
                }
                """);
            var profilePath = Path.Combine(workDir, "profile.json");
            File.WriteAllText(
                profilePath,
                """
                {
                  "version": 1,
                  "name": "隐藏星级",
                  "stageRoot": "stage",
                  "placeholdersRoot": "public/placeholders",
                  "ledgerPath": "ledger.json",
                  "siteCatalog": { "kind": "personalworks-ts", "path": "works.ts", "sitePath": "site.ts", "gameDataPath": "games.ts" },
                  "channels": [
                    { "key": "game-dev", "zh": "游戏开发", "en": "Game Development", "deco": "GAME DEV" }
                  ]
                }
                """);
            var hidePath = Path.Combine(workDir, "hide.json");
            File.WriteAllText(
                hidePath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "site.hide",
                      "workId": "demo-game",
                      "channel": "game-dev",
                      "object": "game-dev/demo-game/02.webp"
                    }
                  ]
                }
                """);
            var hide = RunPatch(script!, hidePath, profilePath, "--apply");
            Assert.True(hide.Ok, hide.StdOut + hide.StdErr);
            var hiddenText = File.ReadAllText(Path.Combine(workDir, "games.ts"));
            Assert.Contains("hiddenStars", hiddenText, StringComparison.Ordinal);
            Assert.Contains("\"game-dev/demo-game/02.webp\": 4", hiddenText, StringComparison.Ordinal);

            var session = WorkspaceSession.Load(profilePath);
            var hiddenCard = session.SiteItems.Single(item => item.ObjectKey == "game-dev/demo-game/02.webp");
            Assert.True(hiddenCard.IsHidden);
            Assert.Equal(4, hiddenCard.Stars);
            Assert.DoesNotContain(
                session.Works.Single(item => item.Id == "demo-game").Media,
                item => item.Src == "game-dev/demo-game/02.webp");

            var restorePath = Path.Combine(workDir, "restore.json");
            File.WriteAllText(
                restorePath,
                """
                {
                  "version": 1,
                  "items": [
                    {
                      "intent": "site.restore",
                      "workId": "demo-game",
                      "channel": "game-dev",
                      "object": "game-dev/demo-game/02.webp"
                    }
                  ]
                }
                """);
            var restore = RunPatch(script!, restorePath, profilePath, "--apply");
            Assert.True(restore.Ok, restore.StdOut + restore.StdErr);
            var restored = File.ReadAllText(Path.Combine(workDir, "games.ts"));
            Assert.DoesNotContain("hiddenStars", restored, StringComparison.Ordinal);
            Assert.Contains("\"stars\":4", restored, StringComparison.Ordinal);
            Assert.Contains("game-dev/demo-game/02.webp", restored, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_PromotesTupleAndKeepsLabel()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = Path.Combine(Path.GetTempPath(), "sms-stars-ts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "works.ts"),
                """
                export const registeredWorks = [
                  {
                    id: "demo-park",
                    channel: "demo-render",
                    title: "演示公园",
                    media: [
                      ["demo-render/demo-park/02.png", "效果图 02"]
                    ]
                  }
                ];
                """);
            File.WriteAllText(
                Path.Combine(workDir, "profile.json"),
                """
                {
                  "version": 1,
                  "name": "星级隔离",
                  "stageRoot": "stage",
                  "placeholdersRoot": "public/placeholders",
                  "ledgerPath": "content/media-ledger.json",
                  "siteCatalog": { "kind": "personalworks-ts", "path": "works.ts" },
                  "channels": [
                    { "key": "demo-render", "zh": "演示", "en": "Demo", "deco": "DEMO" }
                  ]
                }
                """);
            var intentPath = Path.Combine(workDir, "stars.json");
            File.WriteAllText(intentPath, StarIntent(5), JsonUtil.Utf8NoBom);
            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var text = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            Assert.Contains("demo-render/demo-park/02.png", text, StringComparison.Ordinal);
            Assert.Contains("效果图 02", text, StringComparison.Ordinal);
            Assert.Contains("\"stars\": 5", text, StringComparison.Ordinal);
            Assert.DoesNotContain("[\"demo-render/demo-park/02.png\"", text, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_IngestWritesStageStar()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = Path.Combine(Path.GetTempPath(), "sms-stars-ingest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "works.ts"),
                """
                export const registeredWorks = [
                  {
                    id: "demo-park",
                    channel: "demo-render",
                    title: "演示公园",
                    media: [
                      { kind: "image", label: "效果图 02", src: "demo-render/demo-park/02.png" }
                    ]
                  }
                ];
                """);
            File.WriteAllText(
                Path.Combine(workDir, "profile.json"),
                """
                {
                  "version": 1,
                  "name": "星级隔离",
                  "stageRoot": "stage",
                  "placeholdersRoot": "public/placeholders",
                  "ledgerPath": "content/media-ledger.json",
                  "siteCatalog": { "kind": "personalworks-ts", "path": "works.ts" },
                  "channels": [
                    { "key": "demo-render", "zh": "演示", "en": "Demo", "deco": "DEMO" }
                  ]
                }
                """);
            var intentPath = Path.Combine(workDir, "ingest-star.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "stage.ingest",
                      "channel": "demo-render",
                      "workId": "demo-park",
                      "object": "demo-render/demo-park/03.png",
                      "sourceStageRel": "raw/demo.jpg",
                      "stageRel": "raw/demo.jpg"
                    },
                    {
                      "intent": "stars.update",
                      "stageRel": "raw/demo.jpg",
                      "stars": 4
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);
            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var text = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            Assert.Contains("demo-render/demo-park/03.png", text, StringComparison.Ordinal);
            Assert.Contains("stars: 4", text, StringComparison.Ordinal);
            Assert.Contains("效果图 02", text, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_ObjectStarWithoutWorkDoesNotAbort()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = Path.Combine(Path.GetTempPath(), "sms-stars-defer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "works.ts"),
                """
                export const registeredWorks = [
                  {
                    id: "demo-park",
                    channel: "demo-render",
                    title: "演示公园",
                    media: [
                      { kind: "image", label: "效果图 02", src: "demo-render/demo-park/02.png" }
                    ]
                  }
                ];
                """);
            File.WriteAllText(
                Path.Combine(workDir, "profile.json"),
                """
                {
                  "version": 1,
                  "name": "星级隔离",
                  "stageRoot": "stage",
                  "placeholdersRoot": "public/placeholders",
                  "ledgerPath": "content/media-ledger.json",
                  "siteCatalog": { "kind": "personalworks-ts", "path": "works.ts" },
                  "channels": [
                    { "key": "demo-render", "zh": "演示", "en": "Demo", "deco": "DEMO" }
                  ]
                }
                """);
            var intentPath = Path.Combine(workDir, "defer-star.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "stars.update",
                      "channel": "demo-render",
                      "workId": "demo-park",
                      "object": "demo-render/demo-park/02.png",
                      "stars": 5
                    },
                    {
                      "intent": "stars.update",
                      "object": "photo/real-world-photo/missing/01.webp",
                      "stars": 1
                    },
                    {
                      "intent": "stars.update",
                      "stageRel": "raw/unpaired.jpg",
                      "stars": 3
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);
            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var text = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            Assert.Contains("stars: 5", text, StringComparison.Ordinal);
            Assert.DoesNotContain("01.webp", text, StringComparison.Ordinal);
            Assert.DoesNotContain("找不到已入编作品", apply.StdErr, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Script_ProfilePortraitStarsStayBesideStringArray()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var workDir = Path.Combine(Path.GetTempPath(), "sms-stars-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "works.ts"),
                """
                export const registeredWorks = [
                  {
                    id: "demo-park",
                    channel: "demo-render",
                    title: "演示公园",
                    media: [
                      { kind: "image", label: "效果图 02", src: "demo-render/demo-park/02.png" }
                    ]
                  }
                ];
                """);
            File.WriteAllText(
                Path.Combine(workDir, "site.ts"),
                """
                export const profile = {
                  name: "测试",
                  portraitSrc: "profile/portrait.webp",
                  portraitSrcs: [
                    "profile/portrait.webp",
                    "profile/02.webp",
                  ],
                };
                """);
            File.WriteAllText(
                Path.Combine(workDir, "profile.json"),
                """
                {
                  "version": 1,
                  "name": "形象星级",
                  "stageRoot": "stage",
                  "placeholdersRoot": "public/placeholders",
                  "ledgerPath": "content/media-ledger.json",
                  "siteCatalog": {
                    "kind": "personalworks-ts",
                    "path": "works.ts",
                    "sitePath": "site.ts"
                  },
                  "channels": [
                    { "key": "demo-render", "zh": "演示", "en": "Demo", "deco": "DEMO" },
                    { "key": "profile", "zh": "形象照", "en": "Profile", "deco": "PROFILE" }
                  ]
                }
                """);
            var intentPath = Path.Combine(workDir, "profile-star.json");
            File.WriteAllText(
                intentPath,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "stars.update",
                      "channel": "demo-render",
                      "workId": "demo-park",
                      "object": "demo-render/demo-park/02.png",
                      "stars": 4
                    },
                    {
                      "intent": "stars.update",
                      "channel": "profile",
                      "workId": "仪摄影写真",
                      "object": "profile/portrait.webp",
                      "stars": 3
                    }
                  ],
                  "options": { "relabel": true, "deploy": "none" }
                }
                """,
                JsonUtil.Utf8NoBom);
            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var worksText = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            var siteText = File.ReadAllText(Path.Combine(workDir, "site.ts"));
            Assert.Contains("stars: 4", worksText, StringComparison.Ordinal);
            Assert.DoesNotContain("仪摄影写真", worksText, StringComparison.Ordinal);
            Assert.Contains("\"profile/portrait.webp\",", siteText, StringComparison.Ordinal);
            Assert.Contains("portraitStars:", siteText, StringComparison.Ordinal);
            Assert.Contains("\"profile/portrait.webp\": 3", siteText, StringComparison.Ordinal);
            var catalog = PersonalWorksSiteCatalogReader.Load(
                Path.Combine(workDir, "works.ts"),
                Path.Combine(workDir, "site.ts"));
            var portrait = Assert.Single(
                catalog.SelectMany(item => item.Media),
                item => item.Src == "profile/portrait.webp");
            Assert.Equal(3, portrait.Stars);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Compiler_KeepsStageDraftUntilIngest()
    {
        var document = new IntentDocument
        {
            Items =
            {
                new IntentItem
                {
                    Intent = MediaIntentCodes.StarsUpdate,
                    StageRel = "raw/demo.jpg",
                    Stars = 4
                },
                new IntentItem
                {
                    Intent = MediaIntentCodes.StarsUpdate,
                    Object = "photo/missing/01.webp",
                    Stars = 3
                },
                new IntentItem
                {
                    Intent = MediaIntentCodes.StarsUpdate,
                    Channel = "demo-render",
                    WorkId = "demo-park",
                    Object = "demo-render/demo-park/02.png",
                    Stars = 5
                }
            }
        };
        var kept = StarDraftCompiler.KeysWritten(document);
        Assert.Contains(StarDraftStore.ObjectKey("demo-render/demo-park/02.png"), kept);
        Assert.DoesNotContain(StarDraftStore.StageKey("raw/demo.jpg"), kept);
        Assert.DoesNotContain(StarDraftStore.ObjectKey("photo/missing/01.webp"), kept);

        document.Items.Add(new IntentItem
        {
            Intent = MediaIntentCodes.StageIngest,
            Object = "demo-render/demo-park/03.png",
            SourceStageRel = "raw/demo.jpg",
            StageRel = "raw/demo.jpg"
        });
        var written = StarDraftCompiler.KeysWritten(document);
        Assert.Contains(StarDraftStore.StageKey("raw/demo.jpg"), written);
    }

    [Fact]
    public void Compiler_ClearsOriginalStageKeyWhenIngestRewritesRel()
    {
        var prepared = WebpPrepareRules.PreparedStageRel(
            "raw/demo.jpg",
            "demo-park",
            "demo-render/demo-park/03.png");
        var document = new IntentDocument
        {
            Items =
            {
                new IntentItem
                {
                    Intent = MediaIntentCodes.StarsUpdate,
                    StageRel = "raw/demo.jpg",
                    Stars = 4
                },
                new IntentItem
                {
                    Intent = MediaIntentCodes.StageIngest,
                    Object = "demo-render/demo-park/03.webp",
                    SourceStageRel = "raw/demo.jpg",
                    StageRel = prepared
                }
            }
        };

        var written = StarDraftCompiler.KeysWritten(document);
        Assert.Contains(StarDraftStore.StageKey("raw/demo.jpg"), written);
        Assert.NotEqual("raw/demo.jpg", prepared);
    }

    [Fact]
    public void Script_IngestWritesStageStarWhenRelRewritten()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");
        var prepared = WebpPrepareRules.PreparedStageRel(
            "raw/demo.jpg",
            "demo-park",
            "demo-render/demo-park/03.png");
        var workDir = Path.Combine(Path.GetTempPath(), "sms-stars-rewrite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "works.ts"),
                """
                export const registeredWorks = [
                  {
                    id: "demo-park",
                    channel: "demo-render",
                    title: "演示公园",
                    media: [
                      { kind: "image", label: "效果图 02", src: "demo-render/demo-park/02.png" }
                    ]
                  }
                ];
                """);
            File.WriteAllText(
                Path.Combine(workDir, "profile.json"),
                """
                {
                  "version": 1,
                  "name": "星级隔离",
                  "stageRoot": "stage",
                  "placeholdersRoot": "public/placeholders",
                  "ledgerPath": "content/media-ledger.json",
                  "siteCatalog": { "kind": "personalworks-ts", "path": "works.ts" },
                  "channels": [
                    { "key": "demo-render", "zh": "演示", "en": "Demo", "deco": "DEMO" }
                  ]
                }
                """);
            var intentPath = Path.Combine(workDir, "ingest-star.json");
            File.WriteAllText(
                intentPath,
                JsonSerializer.Serialize(new
                {
                    version = 1,
                    mode = "direct",
                    items = new object[]
                    {
                        new
                        {
                            intent = "stage.ingest",
                            channel = "demo-render",
                            workId = "demo-park",
                            @object = "demo-render/demo-park/03.webp",
                            sourceStageRel = "raw/demo.jpg",
                            stageRel = prepared
                        },
                        new
                        {
                            intent = "stars.update",
                            stageRel = "raw/demo.jpg",
                            stars = 4
                        }
                    },
                    options = new { relabel = true, deploy = "none" }
                }, JsonUtil.Options),
                JsonUtil.Utf8NoBom);
            var apply = RunPatch(script!, intentPath, Path.Combine(workDir, "profile.json"), "--apply");
            Assert.True(apply.Ok, apply.StdOut + apply.StdErr);
            var text = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            Assert.Contains("demo-render/demo-park/03.webp", text, StringComparison.Ordinal);
            Assert.Contains("stars: 4", text, StringComparison.Ordinal);
            Assert.Contains("效果图 02", text, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void ContentWrite_RequiresCataloguedObject()
    {
        Assert.True(StarDraftCompiler.IsContentWrite(new IntentItem
        {
            Intent = MediaIntentCodes.StarsUpdate,
            Channel = "real-world-photo",
            WorkId = "demo",
            Object = "photo/real-world-photo/demo/09.webp",
            Stars = 5
        }));
        Assert.False(StarDraftCompiler.IsContentWrite(new IntentItem
        {
            Intent = MediaIntentCodes.StarsUpdate,
            Object = "photo/real-world-photo/demo/01.webp",
            Stars = 1
        }));
        Assert.False(StarDraftCompiler.IsContentWrite(new IntentItem
        {
            Intent = MediaIntentCodes.StarsUpdate,
            StageRel = "摄影（photo）/raw.jpg",
            Stars = 4
        }));
    }

    private static string StarIntent(int stars)
    {
        return JsonSerializer.Serialize(new
        {
            version = 1,
            mode = "direct",
            items = new[]
            {
                new
                {
                    intent = "stars.update",
                    channel = "demo-render",
                    workId = "demo-park",
                    @object = "demo-render/demo-park/02.png",
                    stars
                }
            },
            options = new { relabel = true, deploy = "none" }
        }, JsonUtil.Options);
    }

    private static JsonElement FindMedia(JsonDocument document, string src)
    {
        foreach (var work in document.RootElement.GetProperty("works").EnumerateArray())
        {
            foreach (var media in work.GetProperty("media").EnumerateArray())
            {
                if (media.GetProperty("src").GetString() == src)
                {
                    return media;
                }
            }
        }

        throw new InvalidOperationException("找不到 " + src);
    }

    private static string CopyDirectory(string source)
    {
        var dest = Path.Combine(Path.GetTempPath(), "sms-stars-fix-" + Guid.NewGuid().ToString("N"));
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
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch (IOException)
        {
        }
    }
}
