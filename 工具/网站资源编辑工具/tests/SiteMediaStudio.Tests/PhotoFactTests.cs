using System.Diagnostics;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PhotoFactTests
{
    [Fact]
    public void Register_MissingFacts_RejectsInOrder()
    {
        var session = LoadPatchCases();
        var missing = EvaluateRegister(session, startedOn: null, place: null, themes: null);
        Assert.False(missing.Allowed);
        Assert.Equal("缺年份、地点。", missing.Message);

        var facts = EvaluateRegister(session, startedOn: "2025-04-13", place: "上海", themes: null);
        Assert.True(facts.Allowed, facts.Message);

        var yearOnly = EvaluateRegister(session, startedOn: null, place: "上海", themes: null);
        Assert.False(yearOnly.Allowed);
        Assert.Equal("缺年份。", yearOnly.Message);
    }

    [Fact]
    public void Register_CompletePhotoFacts_AllowsAndWritesThemes()
    {
        var session = LoadPatchCases();
        var decision = EvaluateRegister(
            session,
            startedOn: "2025-04-13",
            place: "上海",
            themes: null);
        Assert.True(decision.Allowed, decision.Message);

        var text = ContentPatchPlanner.FormatTsWork(new IntentItem
        {
            Channel = "landscape-photo",
            WorkId = "hangzhou-night",
            Title = "杭州",
            StartedOn = "2025-04-13",
            Place = "上海",
            Year = "2025"
        });
        Assert.True(text.IndexOf("year:", StringComparison.Ordinal) < text.IndexOf("place:", StringComparison.Ordinal));
        Assert.DoesNotContain("themes:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ingest_MissingFacts_Rejects_OldChannelImplicitTypeAllows()
    {
        var file = CreateTempPng();
        var missing = IntentGate.EvaluateStage(MediaIntent.StageIngest, Stage("real-world-photo"), Context(
            new WorkCatalogItem
            {
                Id = "shoot",
                Channel = "real-world-photo",
                Title = "寺",
                Year = "",
                Place = null
            }));
        Assert.False(missing.Allowed);
        Assert.Equal("缺年份、地点。", missing.Message);

        var oldChannel = IntentGate.EvaluateStage(MediaIntent.StageIngest, Stage("landscape-photo"), Context(
            new WorkCatalogItem
            {
                Id = "shoot",
                Channel = "landscape-photo",
                Title = "寺",
                Year = "2019",
                Place = "新加坡"
            }));
        Assert.True(oldChannel.Allowed, oldChannel.Message);

        var realWorld = IntentGate.EvaluateStage(MediaIntent.StageIngest, Stage("real-world-photo"), Context(
            new WorkCatalogItem
            {
                Id = "shoot",
                Channel = "real-world-photo",
                Title = "寺",
                Year = "2025",
                Place = "上海"
            }));
        Assert.True(realWorld.Allowed, realWorld.Message);

        var demo = IntentGate.EvaluateStage(
            MediaIntent.StageIngest,
            new StageItem
            {
                StageRel = "demo-render/demo-park/01.png",
                FullPath = file,
                ChannelKey = "demo-render",
                IsStock = false
            },
            Context(new WorkCatalogItem
            {
                Id = "shoot",
                Channel = "demo-render",
                Title = "公园"
            }, "demo-render"));
        Assert.True(demo.Allowed, demo.Message);
    }

    [Fact]
    public void Tags_RejectsYearAndProfile_AllowsMultiFrame()
    {
        var work = PhotoWork();
        var session = TagSession(work);
        var year = IntentGate.EvaluateTags(TagItem(work, new[] { "2024" }, work.Media.Select(item => item.Src!).ToList()), ContextOf(session));
        Assert.False(year.Allowed);
        Assert.Contains("年份不是标签", year.Message, StringComparison.Ordinal);

        var profile = new WorkCatalogItem
        {
            Id = "portrait",
            Channel = "profile",
            Title = "形象",
            SourceKind = "works"
        };
        var profileSession = TagSession(profile, "profile");
        var profileDecision = IntentGate.EvaluateTags(
            TagItem(profile, Array.Empty<string>(), null),
            ContextOf(profileSession));
        Assert.False(profileDecision.Allowed);
        Assert.Contains("形象不打标签", profileDecision.Message, StringComparison.Ordinal);

        var batch = IntentGate.EvaluateTags(
            TagItem(work, new[] { "夜景" }, work.Media.Select(item => item.Src!).ToList(), new[] { "humanist-photo" }),
            ContextOf(session));
        Assert.True(batch.Allowed, batch.Message);
        Assert.Contains("2 张", batch.Message, StringComparison.Ordinal);

        var many = IntentGate.EvaluateTags(
            TagItem(work, Array.Empty<string>(), work.Media.Select(item => item.Src!).Take(1).ToList(), new[] { "landscape-photo", "humanist-photo" }),
            ContextOf(session));
        Assert.True(many.Allowed, many.Message);

        var none = IntentGate.EvaluateTags(
            TagItem(work, Array.Empty<string>(), work.Media.Select(item => item.Src!).Take(1).ToList(), Array.Empty<string>()),
            ContextOf(session));
        Assert.True(none.Allowed, none.Message);

        var onWork = IntentGate.EvaluateTags(
            TagItem(work, new[] { "夜景" }, Array.Empty<string>(), new[] { "landscape-photo" }),
            ContextOf(session));
        Assert.False(onWork.Allowed);
        Assert.Contains("项目不写主题类型", onWork.Message, StringComparison.Ordinal);

        var pending = PendingPublishStore.FromDocument(
            session.Profile,
            IntentDocumentBuilder.BuildTags(session, ExecutionMode.Direct, work, work.Media.Select(item => item.Src!).ToList(), new[] { "humanist-photo" }, new[] { "夜景" }),
            Array.Empty<string>(),
            session);
        Assert.NotNull(pending);
        Assert.Equal("spa", pending!.Deploy);
    }

    [Fact]
    public void Script_BatchTags_WritesEachFrameWithoutChangingSrc()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = Path.Combine(Path.GetTempPath(), "sms-photo-tags-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.WriteAllText(Path.Combine(workDir, "works.ts"), """
                export const placeholderWorks = [
                  {
                    id: "shoot",
                    channel: "landscape-photo",
                    title: "上海",
                    year: "2025",
                    place: "上海",
                    media: listed([
                      ["landscape-photo/shoot/01.webp", "甲"],
                      ["landscape-photo/shoot/02.webp", "乙"]
                    ])
                  }
                ];
                """, JsonUtil.Utf8NoBom);
            File.WriteAllText(Path.Combine(workDir, "profile.json"), """
                {
                  "version": 1,
                  "name": "photo-tags",
                  "root": ".",
                  "stageRoot": "stage",
                  "placeholdersRoot": "placeholders",
                  "siteCatalog": { "kind": "personalworks-ts", "path": "works.ts" }
                }
                """, JsonUtil.Utf8NoBom);
            var intentPath = Path.Combine(workDir, "tags.json");
            File.WriteAllText(intentPath, """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    {
                      "intent": "tags.update",
                      "channel": "landscape-photo",
                      "workId": "shoot",
                      "objectList": ["landscape-photo/shoot/01.webp", "landscape-photo/shoot/02.webp"],
                      "themes": ["portrait-photo"],
                      "tags": ["夜景"]
                    }
                  ]
                }
                """, JsonUtil.Utf8NoBom);

            var result = NodeHost.Run(
                script!,
                new[] { "--intent", intentPath, "--profile", Path.Combine(workDir, "profile.json"), "--apply" },
                Path.GetDirectoryName(script)!);
            Assert.True(result.Ok, result.StdOut + result.StdErr);
            var text = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            Assert.Contains("landscape-photo/shoot/01.webp", text, StringComparison.Ordinal);
            Assert.Contains("landscape-photo/shoot/02.webp", text, StringComparison.Ordinal);
            Assert.Contains("\"甲\"", text, StringComparison.Ordinal);
            Assert.Contains("\"乙\"", text, StringComparison.Ordinal);
            Assert.Equal(3, CountOf(text, "portrait-photo"));
            Assert.Contains("themes: [\"portrait-photo\"]", text, StringComparison.Ordinal);
            Assert.Equal(2, CountOf(text, "夜景"));
            Assert.DoesNotContain("[\"landscape-photo/shoot/01.webp\", \"甲\"]", text, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// 统计子串出现次数。
    /// </summary>
    private static int CountOf(string text, string token)
    {
        var count = 0;
        var from = 0;
        while (true)
        {
            var index = text.IndexOf(token, from, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            from = index + token.Length;
        }
    }

    /// <summary>
    /// 加载补丁样例，供登记闸门找到编目文件。
    /// </summary>
    private static WorkspaceSession LoadPatchCases()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        return WorkspaceSession.Load(profilePath!);
    }

    /// <summary>
    /// 对未登记夹做登记判定。
    /// </summary>
    private static GateDecision EvaluateRegister(
        WorkspaceSession session,
        string? startedOn,
        string? place,
        IReadOnlyList<string>? themes)
    {
        return IntentGate.EvaluateRegister(
            new IntentItem
            {
                Intent = MediaIntentCodes.WorkRegister,
                Channel = "landscape-photo",
                WorkId = "hangzhou-night",
                Title = "杭州",
                StageFolder = "landscape-photo/hangzhou-night",
                StartedOn = startedOn,
                Place = place,
                Themes = themes?.ToList()
            },
            new IntentContext
            {
                Profile = session.Profile,
                LedgerDict = session.LedgerDict,
                ContentRefCountDict = session.ContentRefCountDict,
                WorkList = new[]
                {
                    new WorkCatalogItem
                    {
                        Id = "hangzhou-night",
                        Channel = "landscape-photo",
                        Title = "杭州",
                        IsUnregistered = true,
                        StageFolder = "landscape-photo/hangzhou-night"
                    }
                }
            });
    }

    /// <summary>
    /// 上页判定用的临时文件与目标作品。
    /// </summary>
    private static StageItem Stage(string channel)
    {
        return new StageItem
        {
            StageRel = channel + "/shoot/01.jpg",
            FullPath = CreateTempPng(),
            ChannelKey = channel,
            IsStock = false
        };
    }

    /// <summary>
    /// 只含一条已入编作品的上页上下文。
    /// </summary>
    private static IntentContext Context(WorkCatalogItem work, string? channel = null)
    {
        return new IntentContext
        {
            LedgerDict = new Dictionary<string, LedgerRecord>(),
            ContentRefCountDict = new Dictionary<string, int>(),
            WorkList = new[] { work },
            TargetWorkId = "shoot",
            Profile = new WorkspaceProfile
            {
                Channels = new List<ChannelProfile>
                {
                    new()
                    {
                        Key = channel ?? work.Channel,
                        Capabilities = new ChannelCapabilities { Ingest = true }
                    }
                }
            }
        };
    }

    /// <summary>
    /// 两张已上页的摄影作品。
    /// </summary>
    private static WorkCatalogItem PhotoWork()
    {
        return new WorkCatalogItem
        {
            Id = "shoot",
            Channel = "landscape-photo",
            Title = "上海",
            Year = "2025",
            Place = "上海",
            SourceKind = "works",
            Media = new[]
            {
                new WorkMediaItem { Src = "landscape-photo/shoot/01.webp", Label = "甲" },
                new WorkMediaItem { Src = "landscape-photo/shoot/02.webp", Label = "乙" }
            }
        };
    }

    /// <summary>
    /// 标签闸门用的最小会话。
    /// </summary>
    private static WorkspaceSession TagSession(WorkCatalogItem work, string? extraChannel = null)
    {
        var channels = new List<ChannelProfile>
        {
            new()
            {
                Key = work.Channel,
                Capabilities = new ChannelCapabilities { Ingest = true }
            }
        };
        if (extraChannel != null && extraChannel != work.Channel)
        {
            channels.Add(new ChannelProfile { Key = extraChannel });
        }

        return new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                ProfilePath = "profile.json",
                ResolvedRoot = Path.GetTempPath(),
                Channels = channels
            },
            Works = new[] { work },
            LedgerDict = new Dictionary<string, LedgerRecord>(),
            ContentRefCountDict = new Dictionary<string, int>()
        };
    }

    /// <summary>
    /// 标签闸门上下文。
    /// </summary>
    private static IntentContext ContextOf(WorkspaceSession session)
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
    /// 一条标签意图。
    /// </summary>
    private static IntentItem TagItem(
        WorkCatalogItem work,
        IReadOnlyList<string> tags,
        IReadOnlyList<string>? objectList,
        IReadOnlyList<string>? themes = null)
    {
        return new IntentItem
        {
            Intent = MediaIntentCodes.TagsUpdate,
            Channel = work.Channel,
            WorkId = work.Id,
            Tags = tags.ToList(),
            Themes = themes?.ToList(),
            ObjectList = objectList?.ToList()
        };
    }

    /// <summary>
    /// 临时图片，供上页闸门确认文件存在。
    /// </summary>
    private static string CreateTempPng()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-photo-fact-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        return path;
    }

    /// <summary>
    /// 本机是否能启动 node。
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
