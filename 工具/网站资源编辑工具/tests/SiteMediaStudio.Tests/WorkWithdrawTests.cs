using System.Diagnostics;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class WorkWithdrawTests
{
    [Fact]
    public void CollectObjectKeys_IncludesExplicitPoster()
    {
        var work = VideoWork();
        var keys = WorkWithdrawRules.CollectObjectKeys(work, LedgerOf(work), null);
        Assert.Equal(
            new[] { "digital-twin/clip/01.mp4", "digital-twin/clip/01.poster.webp" },
            keys);
    }

    [Fact]
    public void Gate_RejectsSharedRef_AllowsSoleOwner()
    {
        var work = VideoWork();
        var other = new WorkCatalogItem
        {
            Id = "other",
            Channel = "digital-twin",
            Title = "另一部",
            SourceKind = "works",
            Media = work.Media
        };
        var shared = SessionOf(new[] { work, other });
        var sharedReport = PreviewReporter.BuildFromDocument(
            shared,
            IntentDocumentBuilder.BuildWorkWithdraw(shared, ExecutionMode.Prompt, work.Channel, work.Id));
        Assert.True(sharedReport.HasHardError);
        Assert.Contains("仍有其它内容引用", sharedReport.Lines[0].Decision.Message, StringComparison.Ordinal);

        var sole = SessionOf(new[] { work });
        var soleReport = PreviewReporter.BuildFromDocument(
            sole,
            IntentDocumentBuilder.BuildWorkWithdraw(sole, ExecutionMode.Prompt, work.Channel, work.Id));
        Assert.False(soleReport.HasHardError);
        Assert.Contains("将删除内容记录", soleReport.PatchPreview, StringComparison.Ordinal);
        Assert.Contains("01.poster.webp", soleReport.PatchPreview, StringComparison.Ordinal);
        Assert.Contains("中转站文件保留", soleReport.PatchPreview, StringComparison.Ordinal);

        var pending = PendingPublishStore.FromDocument(sole.Profile, soleReport.Document, Array.Empty<string>(), sole);
        Assert.NotNull(pending);
        Assert.Equal("spa", pending!.Deploy);
        Assert.Equal(
            new[] { "digital-twin/clip/01.mp4", "digital-twin/clip/01.poster.webp" },
            pending.WithdrawObjectList);
    }

    [Fact]
    public void Gate_RejectsMissingWorkAndProfile()
    {
        var session = SessionOf(new[] { VideoWork() });
        var missing = IntentGate.EvaluateWorkWithdraw(
            new IntentItem
            {
                Intent = MediaIntentCodes.WorkWithdraw,
                Channel = "digital-twin",
                WorkId = "no-such"
            },
            ContextOf(session));
        Assert.Contains("找不到已入编作品", missing.Message, StringComparison.Ordinal);

        var profile = new WorkCatalogItem
        {
            Id = "portrait",
            Channel = "profile",
            Title = "形象",
            SourceKind = "site-profile"
        };
        var profileSession = SessionOf(new[] { profile });
        var rejected = IntentGate.EvaluateWorkWithdraw(
            new IntentItem
            {
                Intent = MediaIntentCodes.WorkWithdraw,
                Channel = "profile",
                WorkId = "portrait"
            },
            ContextOf(profileSession));
        Assert.Contains("内容池作品记录", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_DeletesTupleWorkAndVideoPoster_RefusesShared()
    {
        var script = ContentPatchClient.FindScript();
        Assert.NotNull(script);
        Assert.True(HasNode(), "本机需要 node 才能回归内容补丁脚本。");

        var workDir = Path.Combine(Path.GetTempPath(), "sms-withdraw-work-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            WriteFixture(workDir);
            var profilePath = Path.Combine(workDir, "profile.json");
            var photoIntent = Path.Combine(workDir, "withdraw-photo.json");
            File.WriteAllText(
                photoIntent,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    { "intent": "work.withdraw", "channel": "landscape-photo", "workId": "drop-me" }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);

            var photo = RunPatch(script!, photoIntent, profilePath, "--apply");
            Assert.True(photo.Ok, photo.StdOut + photo.StdErr);
            var projects = File.ReadAllText(Path.Combine(workDir, "initialWorkProjects.ts"));
            Assert.DoesNotContain("drop-me", projects, StringComparison.Ordinal);
            Assert.Contains("keep-me", projects, StringComparison.Ordinal);
            Assert.Contains("landscape-photo/keep-me/01.webp", projects, StringComparison.Ordinal);
            var sizes = File.ReadAllText(Path.Combine(workDir, "photoSizes.ts"));
            Assert.DoesNotContain("landscape-photo/drop-me/01.webp", sizes, StringComparison.Ordinal);
            Assert.Contains("landscape-photo/keep-me/01.webp", sizes, StringComparison.Ordinal);
            var exif = File.ReadAllText(Path.Combine(workDir, "photoExif.ts"));
            Assert.DoesNotContain("landscape-photo/drop-me/02.webp", exif, StringComparison.Ordinal);
            Assert.Contains("landscape-photo/keep-me/01.webp", exif, StringComparison.Ordinal);
            var works = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            Assert.Contains("clip", works, StringComparison.Ordinal);

            var videoIntent = Path.Combine(workDir, "withdraw-video.json");
            File.WriteAllText(
                videoIntent,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    { "intent": "work.withdraw", "channel": "digital-twin", "workId": "clip" }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);
            var video = RunPatch(script!, videoIntent, profilePath, "--apply");
            Assert.True(video.Ok, video.StdOut + video.StdErr);
            Assert.Contains("01.poster.webp", video.StdOut, StringComparison.Ordinal);
            var worksAfter = File.ReadAllText(Path.Combine(workDir, "works.ts"));
            Assert.DoesNotContain("digital-twin/clip/01.mp4", worksAfter, StringComparison.Ordinal);
            Assert.Contains("keep-me", File.ReadAllText(Path.Combine(workDir, "initialWorkProjects.ts")), StringComparison.Ordinal);

            WriteFixture(workDir);
            var sharedIntent = Path.Combine(workDir, "withdraw-shared.json");
            File.WriteAllText(
                Path.Combine(workDir, "initialWorkProjects.ts"),
                """
                export const initialWorkProjects = [
                  {
                    "id": "drop-me",
                    "channel": "landscape-photo",
                    "title": "要撤",
                    "media": [["landscape-photo/shared/01.webp", "甲"]]
                  },
                  {
                    "id": "keep-me",
                    "channel": "landscape-photo",
                    "title": "留下",
                    "media": [["landscape-photo/shared/01.webp", "乙"]]
                  }
                ];
                """,
                JsonUtil.Utf8NoBom);
            var before = File.ReadAllText(Path.Combine(workDir, "initialWorkProjects.ts"));
            File.WriteAllText(
                sharedIntent,
                """
                {
                  "version": 1,
                  "mode": "direct",
                  "items": [
                    { "intent": "work.withdraw", "channel": "landscape-photo", "workId": "drop-me" }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);
            var shared = RunPatch(script!, sharedIntent, profilePath, "--apply");
            Assert.False(shared.Ok);
            Assert.Contains("仍被未纳入批次的作品引用", shared.StdOut, StringComparison.Ordinal);
            Assert.Equal(before, File.ReadAllText(Path.Combine(workDir, "initialWorkProjects.ts")));
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

    private static WorkCatalogItem VideoWork()
    {
        return new WorkCatalogItem
        {
            Id = "clip",
            Channel = "digital-twin",
            Title = "片段",
            SourceKind = "works",
            Media = new[]
            {
                new WorkMediaItem
                {
                    Kind = "video",
                    Label = "片段 01",
                    Src = "digital-twin/clip/01.mp4",
                    Poster = "digital-twin/clip/01.poster.webp"
                }
            }
        };
    }

    private static Dictionary<string, LedgerRecord> LedgerOf(WorkCatalogItem work)
    {
        var dict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal);
        foreach (var key in WorkWithdrawRules.CollectObjectKeys(work))
        {
            dict[key] = new LedgerRecord { Object = key, Status = "published", StageRel = "来源/" + Path.GetFileName(key) };
        }

        return dict;
    }

    private static WorkspaceSession SessionOf(IReadOnlyList<WorkCatalogItem> works)
    {
        var ledger = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal);
        var refs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var work in works)
        {
            foreach (var key in WorkWithdrawRules.CollectObjectKeys(work))
            {
                ledger[key] = new LedgerRecord { Object = key, Status = "published" };
                refs[key] = refs.GetValueOrDefault(key) + 1;
            }
        }

        return new WorkspaceSession
        {
            Profile = new WorkspaceProfile { Name = "withdraw-test" },
            Works = works,
            LedgerDict = ledger,
            ContentRefCountDict = refs
        };
    }

    private static IntentContext ContextOf(WorkspaceSession session)
    {
        return new IntentContext
        {
            LedgerDict = session.LedgerDict,
            ContentRefCountDict = session.ContentRefCountDict,
            Profile = session.Profile,
            WorkList = session.Works,
            BatchRemoveCountDict = new Dictionary<string, int>()
        };
    }

    private static void WriteFixture(string workDir)
    {
        File.WriteAllText(
            Path.Combine(workDir, "works.ts"),
            """
            const lineSimulationWorks = [];
            const registeredWorks = [
              {
                id: "clip",
                channel: "digital-twin",
                title: "片段",
                media: [{ kind: "video", src: "digital-twin/clip/01.mp4", poster: "digital-twin/clip/01.poster.webp", label: "片段 01" }]
              }
            ];
            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(workDir, "initialWorkProjects.ts"),
            """
            export const initialWorkProjects = [
              {
                "id": "drop-me",
                "channel": "landscape-photo",
                "title": "要撤",
                "media": [
                  ["landscape-photo/drop-me/01.webp", "甲"],
                  ["landscape-photo/drop-me/02.webp", "乙"]
                ]
              },
              {
                "id": "keep-me",
                "channel": "landscape-photo",
                "title": "留下",
                "media": [
                  ["landscape-photo/keep-me/01.webp", "丙"]
                ]
              }
            ];
            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(workDir, "photoSizes.ts"),
            """
            export const photoSizes = {
              "landscape-photo/drop-me/01.webp": { width: 10, height: 20 },
              "landscape-photo/keep-me/01.webp": { width: 30, height: 40 },
            };
            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(workDir, "photoExif.ts"),
            """
            export const photoExif = {
              "landscape-photo/drop-me/02.webp": {
                camera: "Sim",
                iso: "100",
              },
              "landscape-photo/keep-me/01.webp": {
                camera: "Keep",
              },
            };
            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(workDir, "profile.json"),
            """
            {
              "version": 1,
              "name": "withdraw-iso",
              "root": ".",
              "stageRoot": "stage",
              "placeholdersRoot": "placeholders",
              "siteCatalog": {
                "kind": "personalworks-ts",
                "path": "works.ts",
                "workDataPath": "initialWorkProjects.ts"
              }
            }
            """,
            JsonUtil.Utf8NoBom);
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
}
