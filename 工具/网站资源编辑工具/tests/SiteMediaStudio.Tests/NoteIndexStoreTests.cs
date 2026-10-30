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

    [Fact]
    public void Load_ImagesAsStringPaths_FillsObjectKeyWithoutLeadingSlash()
    {
        var json = """
            {
              "version": 2,
              "notes": [
                {
                  "folder": "20261029_205600",
                  "slug": "demo",
                  "title": "Demo",
                  "images": [
                    "/notes-assets/n-20261029-205600/icp_demo.png",
                    "notes-assets/n-20261029-205600/other.webp"
                  ]
                }
              ]
            }
            """;
        var (_, path) = WriteIndex(json);

        var index = NoteIndexStore.LoadFile(path);

        Assert.Single(index.Notes);
        Assert.Equal(2, index.Notes[0].Images.Count);
        Assert.Equal("notes-assets/n-20261029-205600/icp_demo.png", index.Notes[0].Images[0].ObjectKey);
        Assert.Equal("", index.Notes[0].Images[0].StageRel);
        Assert.Equal("notes-assets/n-20261029-205600/other.webp", index.Notes[0].Images[1].ObjectKey);
    }

    [Fact]
    public void Load_ImagesAsObjects_KeepsExistingFields()
    {
        var json = """
            {
              "version": 2,
              "notes": [
                {
                  "folder": "20261029_205600",
                  "slug": "demo",
                  "title": "Demo",
                  "images": [
                    {
                      "stageRel": "心得（notes）/20261029_205600/images/icp_demo.png",
                      "objectKey": "notes-assets/n-20261029-205600/icp_demo.png",
                      "stars": 3,
                      "tags": ["图解"],
                      "displayName": "ICP",
                      "description": "演示图",
                      "hidden": true
                    }
                  ]
                }
              ]
            }
            """;
        var (_, path) = WriteIndex(json);

        var index = NoteIndexStore.LoadFile(path);
        var image = Assert.Single(index.Notes[0].Images);

        Assert.Equal("心得（notes）/20261029_205600/images/icp_demo.png", image.StageRel);
        Assert.Equal("notes-assets/n-20261029-205600/icp_demo.png", image.ObjectKey);
        Assert.Equal(3, image.Stars);
        Assert.Equal(new[] { "图解" }, image.Tags);
        Assert.Equal("ICP", image.DisplayName);
        Assert.Equal("演示图", image.Description);
        Assert.True(image.Hidden);
    }

    [Fact]
    public void Load_ImagesMixedStringAndObject_ParsesBoth()
    {
        var json = """
            {
              "version": 2,
              "notes": [
                {
                  "folder": "mixed",
                  "slug": "mixed",
                  "title": "Mixed",
                  "images": [
                    "/notes-assets/n-mixed/a.png",
                    {
                      "stageRel": "心得（notes）/mixed/images/b.png",
                      "objectKey": "/notes-assets/n-mixed/b.webp",
                      "stars": 1,
                      "tags": [],
                      "displayName": "",
                      "description": "",
                      "hidden": false
                    }
                  ]
                }
              ]
            }
            """;
        var (_, path) = WriteIndex(json);

        var index = NoteIndexStore.LoadFile(path);

        Assert.Equal(2, index.Notes[0].Images.Count);
        Assert.Equal("notes-assets/n-mixed/a.png", index.Notes[0].Images[0].ObjectKey);
        Assert.Equal("notes-assets/n-mixed/b.webp", index.Notes[0].Images[1].ObjectKey);
        Assert.Equal("心得（notes）/mixed/images/b.png", index.Notes[0].Images[1].StageRel);
        Assert.Equal(1, index.Notes[0].Images[1].Stars);
    }

    [Fact]
    public void Save_AfterLoadingStringImages_WritesObjectsNotStrings()
    {
        var json = """
            {
              "version": 2,
              "notes": [
                {
                  "folder": "20261029_205600",
                  "slug": "demo",
                  "title": "Demo",
                  "images": [ "/notes-assets/n-20261029-205600/icp_demo.png" ]
                }
              ]
            }
            """;
        var (_, path) = WriteIndex(json);
        var index = NoteIndexStore.LoadFile(path);

        NoteIndexStore.SaveFile(path, index);
        var written = File.ReadAllText(path);

        Assert.DoesNotContain("\"/notes-assets/", written, StringComparison.Ordinal);
        Assert.Contains("\"objectKey\": \"notes-assets/n-20261029-205600/icp_demo.png\"", written, StringComparison.Ordinal);
        Assert.Contains("\"stageRel\":", written, StringComparison.Ordinal);

        var reloaded = NoteIndexStore.LoadFile(path);
        Assert.Equal("notes-assets/n-20261029-205600/icp_demo.png", Assert.Single(reloaded.Notes[0].Images).ObjectKey);
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
