using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ObjectKeyAllocatorTests
{
    [Fact]
    public void WorkDirectoryPrefix_PhotoDelivery_UsesProjectFolder()
    {
        Assert.Equal(
            "photo/real-world-photo/20250413 上海 静安寺/",
            ObjectKeyAllocator.WorkDirectoryPrefix("real-world-photo", "20250413 上海 静安寺"));
        Assert.Equal(
            "photo/game-photo/demo/",
            ObjectKeyAllocator.WorkDirectoryPrefix("game-photo", "demo"));
        Assert.Equal("landscape-photo/old-id/", ObjectKeyAllocator.WorkDirectoryPrefix("landscape-photo", "old-id"));
    }

    [Fact]
    public void Allocate_UsesStageFileName_SuffixWhenTaken()
    {
        var workDir = CreateTempWorkspace(
            """
            [
              { "kind": "image", "label": "效果图 01", "src": "demo-render/demo-park/01.png" },
              { "kind": "image", "label": "效果图 02", "src": "demo-render/demo-park/03.png" }
            ]
            """);
        try
        {
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var reservedSet = new HashSet<string>(StringComparer.Ordinal);
            var first = ObjectKeyAllocator.Allocate(
                session, "demo-render", "demo-park", ".png", "demo-render/demo-park/new.png", false, reservedSet);
            var second = ObjectKeyAllocator.Allocate(
                session, "demo-render", "demo-park", ".png", "demo-render/demo-park/newer.png", false, reservedSet);
            var taken = ObjectKeyAllocator.Allocate(
                session, "demo-render", "demo-park", ".png", "demo-render/demo-park/03.png", false, reservedSet);
            Assert.Equal("demo-render/demo-park/new.png", first);
            Assert.Equal("demo-render/demo-park/newer.png", second);
            Assert.Equal("demo-render/demo-park/03-02.png", taken);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void Allocate_ReusesWithdrawnObject()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var reservedSet = new HashSet<string>(StringComparer.Ordinal);
        var reused = ObjectKeyAllocator.Allocate(
            session,
            "demo-render",
            "demo-park",
            ".png",
            "demo-render/demo-park/old.png",
            bump: false,
            reservedSet);
        Assert.Equal("demo-render/demo-park/old.png", reused);
    }

    [Fact]
    public void Allocate_WithdrawnPathOnOtherWork_AllocatesNewKey()
    {
        var workDir = CreateTempWorkspace("[]");
        try
        {
            File.WriteAllText(
                Path.Combine(workDir, "content", "media-ledger.json"),
                """
                {
                  "version": 1,
                  "records": [
                    {
                      "object": "demo-render/demo-park/old.png",
                      "stageRel": "demo-render/demo-park/old.png",
                      "status": "withdrawn"
                    }
                  ]
                }
                """,
                JsonUtil.Utf8NoBom);
            var session = WorkspaceSession.Load(Path.Combine(workDir, "profile.json"));
            var reservedSet = new HashSet<string>(StringComparer.Ordinal);
            var objectKey = ObjectKeyAllocator.Allocate(
                session,
                "demo-render",
                "demo-yard",
                ".png",
                "demo-render/demo-park/old.png",
                bump: false,
                reservedSet);
            Assert.Equal("demo-render/demo-yard/old.png", objectKey);

            var document = IntentDocumentBuilder.Build(
                session,
                ExecutionMode.Direct,
                new[]
                {
                    (MediaIntent.StageIngest, new StageItem
                    {
                        StageRel = "demo-render/demo-park/old.png",
                        FullPath = "D:/tmp/old.png",
                        ChannelKey = "demo-render",
                        MatchedObject = "demo-render/demo-park/old.png",
                        LedgerStatus = "withdrawn"
                    }, (SiteItem?)null)
                },
                "demo-yard");
            Assert.Equal("demo-render/demo-yard/old.png", document.Items[0].Object);
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    [Fact]
    public void BuildIngest_AssignsNextObjectAndTargetWork()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var leftover = session.StageItems.FirstOrDefault(item =>
            item.StageRel.EndsWith("leftover/unused.png", StringComparison.OrdinalIgnoreCase));
        if (leftover == null)
        {
            return;
        }

        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Direct,
            new[] { (MediaIntent.StageIngest, leftover, (SiteItem?)null) },
            "demo-park");
        Assert.Equal("demo-park", document.Items[0].WorkId);
        Assert.Equal("demo-render/demo-park/03.png", document.Items[0].Object);
    }

    [Fact]
    public void BuildIngest_AlreadyPublished_KeepsMatchedObjectAndDoesNotConsumeNextSlot()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var published = session.StageItems.First(item =>
            item.StageRel == "demo-render/demo-park/01.png");
        var leftover = session.StageItems.First(item =>
            item.StageRel.EndsWith("leftover/unused.png", StringComparison.OrdinalIgnoreCase));

        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Direct,
            new[]
            {
                (MediaIntent.StageIngest, published, (SiteItem?)null),
                (MediaIntent.StageIngest, leftover, (SiteItem?)null)
            },
            "demo-park");

        Assert.Equal(published.MatchedObject, document.Items[0].Object);
        Assert.Equal("demo-render/demo-park/03.png", document.Items[1].Object);
    }

    [Fact]
    public void BuildIngest_PersonalWorksOriginal_AllocatesWebpObject()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var work = session.Works.FirstOrDefault(item =>
            item.Channel == "landscape-photo" && !item.IsUnregistered);
        Assert.NotNull(work);

        var document = IntentDocumentBuilder.Build(
            session,
            ExecutionMode.Direct,
            new[]
            {
                (MediaIntent.StageIngest, new StageItem
                {
                    StageRel = "风光摄影（landscape-photo）/原图.jpg",
                    FullPath = "D:/tmp/原图.jpg",
                    ChannelKey = "landscape-photo",
                    WorkIdGuess = work!.Id
                }, (SiteItem?)null)
            },
            work.Id);

        Assert.Equal(work.Id, document.Items[0].WorkId);
        Assert.EndsWith(".webp", document.Items[0].Object, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("landscape-photo/" + work.Id + "/", document.Items[0].Object, StringComparison.Ordinal);
    }

    [Fact]
    public void Allocate_ProfileUsesFlatFilename()
    {
        var profilePath = ToolPaths.FindPatchCasesProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var reservedSet = new HashSet<string>(StringComparer.Ordinal);

        var objectKey = ObjectKeyAllocator.Allocate(
            session,
            "profile",
            "portrait",
            ".jpg",
            "形象（profile）/new-portrait.jpg",
            bump: false,
            reservedSet);

        Assert.Equal("profile/new-portrait.jpg", objectKey);
        Assert.DoesNotContain("/portrait/01", objectKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// 最小 JSON 工作区，只用于对象键分配。
    /// </summary>
    private static string CreateTempWorkspace(string mediaJson)
    {
        var dest = Path.Combine(Path.GetTempPath(), "sms-alloc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dest, "content"));
        Directory.CreateDirectory(Path.Combine(dest, "stage"));
        Directory.CreateDirectory(Path.Combine(dest, "public", "placeholders"));
        File.WriteAllText(
            Path.Combine(dest, "profile.json"),
            """
            {
              "version": 1,
              "name": "alloc",
              "stageRoot": "stage",
              "placeholdersRoot": "public/placeholders",
              "ledgerPath": "content/media-ledger.json",
              "siteCatalog": { "kind": "json", "path": "content/catalog.json" },
              "channels": [{ "key": "demo-render", "zh": "演示", "en": "Demo", "deco": "DEMO" }]
            }
            """,
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(dest, "content", "catalog.json"),
            "{ \"works\": [{ \"id\": \"demo-park\", \"channel\": \"demo-render\", \"title\": \"公园\", \"media\": "
            + mediaJson
            + " }] }\n",
            JsonUtil.Utf8NoBom);
        File.WriteAllText(
            Path.Combine(dest, "content", "media-ledger.json"),
            "{ \"version\": 1, \"records\": [] }\n",
            JsonUtil.Utf8NoBom);
        return dest;
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
