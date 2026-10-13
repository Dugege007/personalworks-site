using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteIndexStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsEmptyIndex()
    {
        var profile = Profile();
        var index = NoteIndexStore.Load(profile);

        Assert.Empty(index.Notes);
        Assert.False(File.Exists(NoteIndexStore.IndexPath(profile)));
    }

    [Fact]
    public void Load_EmptyObject_KeepsEmptyNotes()
    {
        var (profile, path) = WriteIndex("{}");

        var index = NoteIndexStore.LoadFile(path);

        Assert.Empty(index.Notes);
        Assert.Equal(path, NoteIndexStore.IndexPath(profile));
    }

    [Theory]
    [InlineData("<<<<<<< HEAD\n{\n  \"version\": 1,\n  \"notes\": []\n}\n=======\n{\n  \"version\": 1,\n  \"notes\": []\n}\n>>>>>>> main\n")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"notes\":null}")]
    public void Load_UnreadableContent_ThrowsWithPathAndLeavesFile(string content)
    {
        var (profile, path) = WriteIndex(content);
        var before = File.ReadAllBytes(path);

        var ex = Assert.Throws<NoteIndexCorruptException>(() => NoteIndexStore.Load(profile));

        Assert.Equal(path, ex.IndexPath);
        Assert.Contains(NoteIndexCorruptException.DisplayText, ex.Message, StringComparison.Ordinal);
        Assert.Contains(path, ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Save_CorruptFile_RefusesToOverwrite()
    {
        var (_, path) = WriteIndex("<<<<<<< HEAD\n=======\n>>>>>>> main\n");
        var before = File.ReadAllBytes(path);

        var ex = Assert.Throws<NoteIndexCorruptException>(() =>
            NoteIndexStore.SaveFile(path, new NoteIndexFile()));

        Assert.Contains(NoteIndexCorruptException.DisplayText, ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void HideRestorePublishAndMeta_CorruptIndex_DoNotWrite()
    {
        var content = "<<<<<<< HEAD\n{\n}\n=======\n{\n}\n>>>>>>> main\n";
        var (profile, path) = WriteIndex(content);
        var before = File.ReadAllBytes(path);
        var folder = Path.Combine(profile.ResolvedRoot, "note-folder");
        Directory.CreateDirectory(folder);
        var prepared = false;

        var hide = NoteCommands.Hide(profile, "20261005_125603");
        var restore = NoteCommands.Restore(profile, "20261005_125603");
        var recycle = NoteCommands.RecycleFolder(profile, folder);
        var publish = NotePublisher.Publish(
            profile,
            folder,
            null,
            null,
            (_, _, _) =>
            {
                prepared = true;
                return true;
            },
            (_, _, _) =>
            {
                prepared = true;
                return true;
            });
        var document = new IntentDocument
        {
            Items =
            {
                new IntentItem
                {
                    Intent = MediaIntentCodes.StarsUpdate,
                    Channel = NoteRules.ChannelKey,
                    WorkId = "20261005_125603",
                    Stars = 4,
                    StageRel = "心得（notes）/20261005_125603/正文.md"
                }
            }
        };

        var meta = Assert.Throws<NoteIndexCorruptException>(() => NoteCommands.PullMeta(profile, document));

        Assert.Contains(NoteIndexCorruptException.DisplayText, hide, StringComparison.Ordinal);
        Assert.Contains(path, hide, StringComparison.Ordinal);
        Assert.DoesNotContain("还没上页", hide, StringComparison.Ordinal);
        Assert.Contains(NoteIndexCorruptException.DisplayText, restore, StringComparison.Ordinal);
        Assert.DoesNotContain("没有处于隐藏", restore, StringComparison.Ordinal);
        Assert.Contains(NoteIndexCorruptException.DisplayText, recycle, StringComparison.Ordinal);
        Assert.Contains(NoteIndexCorruptException.DisplayText, publish.Error, StringComparison.Ordinal);
        Assert.Contains(NoteIndexCorruptException.DisplayText, meta.Message, StringComparison.Ordinal);
        Assert.False(prepared);
        Assert.Empty(publish.Ingested);
        Assert.True(Directory.Exists(folder));
        Assert.Single(document.Items);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    private static (WorkspaceProfile Profile, string Path) WriteIndex(string content)
    {
        var profile = Profile();
        var path = NoteIndexStore.IndexPath(profile)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return (profile, path);
    }

    private static WorkspaceProfile Profile()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-note-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "PersonalSite", "src", "content"));
        return new WorkspaceProfile
        {
            ResolvedRoot = root,
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" }
        };
    }
}
