using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class CopyDraftStoreTests
{
    [Fact]
    public void WriteThenRead_KeepsTitleAndDescription()
    {
        WithTempDraft((path, profile) =>
        {
            var key = CopyDraftStore.WorkKey("demo-render", "demo-park");
            CopyDraftStore.Commit(profile, key, "", "草稿说明。", "", "公园项目说明。", path);

            var file = CopyDraftStore.Load(path);
            var entry = CopyDraftStore.Find(file, profile, key);
            Assert.NotNull(entry);
            Assert.Equal("草稿说明。", entry!.Description);
            Assert.Equal("", entry.Title);
            Assert.Equal("公园项目说明。", entry.PublishedDescription);
            Assert.False(string.IsNullOrWhiteSpace(entry.UpdatedAt));
        });
    }

    [Fact]
    public void Load_MissingOrCorrupt_ReturnsEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sms-copy-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var empty = CopyDraftStore.Load(missing);
        Assert.Empty(empty.Entries);
        Assert.Equal("", empty.ProfilePath);

        WithTempDraft((path, _) =>
        {
            File.WriteAllText(path, "{not-json");
            var damaged = CopyDraftStore.Load(path);
            Assert.Empty(damaged.Entries);
            Assert.Equal("", damaged.ProfilePath);
        });
    }

    [Fact]
    public void Find_OtherProfile_DoesNotShareCopy()
    {
        WithTempDraft((path, profileA) =>
        {
            var key = CopyDraftStore.ChannelKey("demo-photo");
            CopyDraftStore.Commit(profileA, key, "", "风光草稿。", "", "演示风光栏目。", path);

            var profileB = new WorkspaceProfile
            {
                ProfilePath = Path.Combine(Path.GetTempPath(), "other-profile.json"),
                ResolvedRoot = Path.Combine(Path.GetTempPath(), "other-root")
            };
            var file = CopyDraftStore.Load(path);
            Assert.NotNull(CopyDraftStore.Find(file, profileA, key));
            Assert.Null(CopyDraftStore.Find(file, profileB, key));

            CopyDraftStore.Commit(profileB, key, "", "另一工作区。", "", "", path);
            var afterSwitch = CopyDraftStore.Load(path);
            Assert.Null(CopyDraftStore.Find(afterSwitch, profileA, key));
            Assert.Equal("另一工作区。", CopyDraftStore.Find(afterSwitch, profileB, key)?.Description);
        });
    }

    [Fact]
    public void ResolveEditorText_DraftOverridesPublished()
    {
        var publishedTitle = "";
        var publishedDescription = "公园项目说明。";
        Assert.Equal(
            (publishedTitle, publishedDescription),
            CopyDraftStore.ResolveEditorText(publishedTitle, publishedDescription, null));

        var draft = new CopyDraftEntry
        {
            Key = CopyDraftStore.WorkKey("demo-render", "demo-park"),
            Title = "",
            Description = "草稿说明。"
        };
        var resolved = CopyDraftStore.ResolveEditorText(publishedTitle, publishedDescription, draft);
        Assert.Equal("", resolved.Title);
        Assert.Equal("草稿说明。", resolved.Description);
    }

    [Fact]
    public void DeleteFile_FallsBackToPublished()
    {
        WithTempDraft((path, profile) =>
        {
            var session = LoadFixtureSession();
            var park = Assert.Single(session.Works, item => item.Id == "demo-park");
            var published = CopyText.ForEditor(park.Summary);
            var key = CopyDraftStore.WorkKey("demo-render", "demo-park");
            CopyDraftStore.Commit(profile, key, "", "草稿说明。", "", published, path);

            var withDraft = CopyDraftStore.ResolveEditorText(
                "",
                published,
                CopyDraftStore.Find(CopyDraftStore.Load(path), profile, key));
            Assert.Equal("草稿说明。", withDraft.Description);

            CopyDraftStore.Delete(path);
            Assert.False(File.Exists(path));
            var afterDelete = CopyDraftStore.ResolveEditorText(
                "",
                published,
                CopyDraftStore.Find(CopyDraftStore.Load(path), profile, key));
            Assert.Equal(published, afterDelete.Description);
        });
    }

    [Fact]
    public void Commit_MatchingPublished_RemovesEntry()
    {
        WithTempDraft((path, profile) =>
        {
            var key = CopyDraftStore.WorkKey("demo-render", "demo-park");
            CopyDraftStore.Commit(profile, key, "", "草稿说明。", "", "公园项目说明。", path);
            CopyDraftStore.Commit(profile, key, "", "公园项目说明。", "", "公园项目说明。", path);
            Assert.False(File.Exists(path));
        });
    }

    [Fact]
    public void Commit_DoesNotTouchWorksOrSite()
    {
        var personal = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(personal);
        var profile = WorkspaceProfileLoader.Load(personal!);
        var worksPath = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.SiteCatalog.Path);
        var sitePath = ResolveOptional(profile, profile.SiteCatalog.SitePath);
        var workDataPath = ResolveOptional(profile, profile.SiteCatalog.WorkDataPath);
        Assert.True(File.Exists(worksPath));
        var worksBefore = File.ReadAllText(worksPath);
        var siteBefore = ReadIfExists(sitePath);
        var workDataBefore = ReadIfExists(workDataPath);

        WithTempDraft((path, draftProfile) =>
        {
            draftProfile.ProfilePath = profile.ProfilePath;
            draftProfile.ResolvedRoot = profile.ResolvedRoot;
            CopyDraftStore.Commit(
                draftProfile,
                CopyDraftStore.WorkKey("digital-twin", "shanghai-mansheng-packaging"),
                "",
                "本机草稿，不应写进内容层。",
                "",
                "",
                path);
            Assert.True(File.Exists(path));
        });

        Assert.Equal(worksBefore, File.ReadAllText(worksPath));
        AssertUnchanged(sitePath, siteBefore);
        AssertUnchanged(workDataPath, workDataBefore);
    }

    [Fact]
    public void Keys_UseFrozenShapes()
    {
        Assert.Equal("channel:demo-photo", CopyDraftStore.ChannelKey("demo-photo"));
        Assert.Equal("work:demo-render:demo-park", CopyDraftStore.WorkKey("demo-render", "demo-park"));
        Assert.Equal(
            "media:demo-render:demo-park:demo-render/demo-park/01.png",
            CopyDraftStore.MediaKey("demo-render", "demo-park", @"demo-render\demo-park\01.png"));
    }

    private static string? ResolveOptional(WorkspaceProfile profile, string? relative)
    {
        return string.IsNullOrWhiteSpace(relative)
            ? null
            : WorkspaceProfileLoader.ResolveUnderRoot(profile, relative);
    }

    private static string? ReadIfExists(string? path)
    {
        return path != null && File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static void AssertUnchanged(string? path, string? before)
    {
        if (before != null && path != null)
        {
            Assert.Equal(before, File.ReadAllText(path));
        }
    }

    private static WorkspaceSession LoadFixtureSession()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        return WorkspaceSession.Load(profilePath!);
    }

    private static void WithTempDraft(Action<string, WorkspaceProfile> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "copy-drafts.json");
        var profile = new WorkspaceProfile
        {
            ProfilePath = Path.Combine(dir, "profile.json"),
            ResolvedRoot = dir
        };
        try
        {
            body(path, profile);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
