using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 心得上页文案、相对站点稿的未同步，以及索引有条目但中转站缺夹。
/// </summary>
public sealed class NotePublishExperienceTests
{
    [Fact]
    public void Copy_UsesPublishVerbWhenIndexHasNoFolder()
    {
        var index = new NoteIndexFile();
        Assert.False(NotePublishCopy.IsPublished(index, "20261005_125603"));
        Assert.Equal("上页", NotePublishCopy.Verb(false));
        Assert.Equal("上页心得", NotePublishCopy.MenuHeader(false));
        Assert.Equal("上页心得", NotePublishCopy.DialogTitle(false));
        Assert.Equal("上页", NotePublishCopy.ConfirmButton(false));
        Assert.Equal("无法上页", NotePublishCopy.FailureTitle(false));
        Assert.Equal("正在上页心得", NotePublishCopy.ProgressTitle(false));
        Assert.Equal("将上页整篇心得，与右键「上页心得」相同。", NotePublishCopy.PreviewAllow(false));

        var message = NotePublishCopy.ConfirmMessage("清晨", false);
        Assert.StartsWith("将上页「清晨」。", message, StringComparison.Ordinal);
        Assert.DoesNotContain("将更新", message, StringComparison.Ordinal);
        Assert.DoesNotContain("更新上页", message, StringComparison.Ordinal);
        Assert.Contains(NotePublishCopy.TwoStepExplanation, message, StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_UsesUpdateVerbWhenFolderIsPublished()
    {
        var index = IndexWith("20261005_125603", "n-hello", hidden: true);
        Assert.True(NotePublishCopy.IsPublished(index, "20261005_125603"));
        Assert.Equal("更新", NotePublishCopy.Verb(true));
        Assert.Equal("更新心得", NotePublishCopy.MenuHeader(true));
        Assert.Equal("更新", NotePublishCopy.ConfirmButton(true));
        Assert.Equal("无法更新", NotePublishCopy.FailureTitle(true));
        Assert.Equal("正在更新心得", NotePublishCopy.ProgressTitle(true));
        Assert.Equal("将更新整篇心得，与右键「更新心得」相同。", NotePublishCopy.PreviewAllow(true));

        var message = NotePublishCopy.ConfirmMessage("清晨", true);
        Assert.StartsWith("将更新「清晨」。", message, StringComparison.Ordinal);
        Assert.DoesNotContain("将上页", message, StringComparison.Ordinal);
        Assert.DoesNotContain("更新上页", message, StringComparison.Ordinal);
        Assert.Contains("不会自动发布上线", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Copy_DraftOrEmptySlugStaysUnpublished()
    {
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry { Folder = "draft-folder", Slug = "n-draft", Draft = true },
                new NoteEntry { Folder = "bare-folder", Slug = "", Draft = false }
            }
        };
        Assert.False(NotePublishCopy.IsPublished(index, "draft-folder"));
        Assert.False(NotePublishCopy.IsPublished(index, "bare-folder"));
        Assert.Equal("上页", NotePublishCopy.ConfirmButton(false));
    }

    [Fact]
    public void Drift_IsFalseWhenSiteMatchesDespiteFrontmatterAndImageKeys()
    {
        var folder = TempFolder("20261005_125603");
        File.WriteAllBytes(Path.Combine(folder, "used.png"), new byte[] { 1 });
        var body = "# 一篇\n\n见 ![用](used.png)\n";
        File.WriteAllText(Path.Combine(folder, "正文.md"), body);
        NoteConfigStore.Save(folder, Config("一篇", "hello"));
        var before = File.ReadAllText(Path.Combine(folder, "心得.json"));
        var entry = PublishedEntry("20261005_125603", "hello", "一篇", "导语在索引里");
        entry.Images.Add(new NoteImageMeta
        {
            StageRel = "心得（notes）/20261005_125603/used.png",
            ObjectKey = "notes/hello/used.webp"
        });
        var site = """
            ---
            title: "一篇"
            date: 2026-10-05
            summary: "导语在索引里"
            ---
            见 ![用](notes/hello/used.webp)
            """;

        Assert.False(NoteSiteDrift.HasUnsyncedSiteChanges(folder, entry, site));
        Assert.Equal(before, File.ReadAllText(Path.Combine(folder, "心得.json")));
    }

    [Fact]
    public void Drift_IsTrueWhenStagingBodyOrImageChanges()
    {
        var folder = TempFolder("20261005_125603");
        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 一篇\n\n原文。\n");
        NoteConfigStore.Save(folder, Config("一篇", "hello"));
        var entry = PublishedEntry("20261005_125603", "hello", "一篇", "");
        var site = "原文。\n";
        Assert.False(NoteSiteDrift.HasUnsyncedSiteChanges(folder, entry, site));

        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 一篇\n\n原文。\n\n又写了一句。\n");
        Assert.True(NoteSiteDrift.HasUnsyncedSiteChanges(folder, entry, site));

        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 一篇\n\n原文。\n");
        File.WriteAllBytes(Path.Combine(folder, "extra.png"), new byte[] { 1 });
        Assert.True(NoteSiteDrift.HasUnsyncedSiteChanges(folder, entry, site));
    }

    [Fact]
    public void Drift_ClearsWhenSiteMatchesEvenIfRevisionBaselineIsOlder()
    {
        var folder = TempFolder("20261005_125603");
        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 一篇\n\n已经更新的正文。\n");
        var config = Config("一篇", "hello");
        config.RevisionBaseline = new NoteRevisionBaseline
        {
            Title = "一篇",
            Lead = "",
            Body = "# 一篇\n\n旧正文。\n",
            Images = new List<string>()
        };
        NoteConfigStore.Save(folder, config);
        var entry = PublishedEntry("20261005_125603", "hello", "一篇", "");

        Assert.False(NoteSiteDrift.HasUnsyncedSiteChanges(folder, entry, "已经更新的正文。\n"));
        Assert.True(NoteSiteDrift.HasUnsyncedSiteChanges(folder, entry, "旧正文。\n"));
    }

    [Fact]
    public void Drift_IgnoresUnpublishedAndTreatsMissingSiteFileAsDirty()
    {
        var folder = TempFolder("20261005_125603");
        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 一篇\n\n原文。\n");
        NoteConfigStore.Save(folder, Config("一篇", "hello"));
        var draft = PublishedEntry("20261005_125603", "hello", "一篇", "");
        draft.Draft = true;
        Assert.False(NoteSiteDrift.HasUnsyncedSiteChanges(folder, draft, "别的正文。\n"));

        var root = Path.Combine(Path.GetTempPath(), "sms-note-drift-site-" + Guid.NewGuid().ToString("N"));
        var notesDir = Path.Combine(root, "notes");
        Directory.CreateDirectory(notesDir);
        var index = IndexWith("20261005_125603", "hello", hidden: false);
        Assert.True(NoteSiteDrift.HasUnsyncedSiteChanges(folder, index, notesDir));
        File.WriteAllText(Path.Combine(notesDir, "hello.md"), "原文。\n");
        Assert.False(NoteSiteDrift.HasUnsyncedSiteChanges(folder, index, notesDir));
    }

    [Fact]
    public void Drift_IsTrueWhenHeadingChangesBeforeIndex()
    {
        var folder = TempFolder("20261005_125603");
        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 新标题\n\n原文。\n");
        NoteConfigStore.Save(folder, Config("旧标题", "hello"));
        var entry = PublishedEntry("20261005_125603", "hello", "旧标题", "");
        Assert.True(NoteSiteDrift.HasUnsyncedSiteChanges(folder, entry, "原文。\n"));
    }

    [Fact]
    public void MissingFolder_ListsIndexEntriesWithoutStageDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-note-missing-" + Guid.NewGuid().ToString("N"));
        var channelDir = Path.Combine(root, "stage", "notes");
        var present = Path.Combine(channelDir, "20261005_125603");
        Directory.CreateDirectory(present);
        File.WriteAllText(Path.Combine(present, "正文.md"), "# 已有\n\n文字。\n");
        var content = Path.Combine(root, "PersonalSite", "src", "content");
        Directory.CreateDirectory(content);
        var profile = new WorkspaceProfile
        {
            ResolvedRoot = root,
            StageRoot = "stage",
            StageFolderPattern = "Key",
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" },
            Channels =
            {
                new ChannelProfile { Key = "notes", Zh = "心得" }
            }
        };
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry { Folder = "20261005_125603", Slug = "n-have", Title = "已有", Draft = false },
                new NoteEntry { Folder = "20261007_205600", Slug = "n-gone", Title = "缺夹的一篇", Draft = false },
                new NoteEntry { Folder = "20261007_205600", Slug = "n-gone-dup", Title = "重复", Draft = false }
            }
        };
        NoteIndexStore.Save(profile, index);

        var missingList = NoteStagePresence.MissingEntries(index, channelDir);
        var missing = Assert.Single(missingList);
        Assert.Equal("20261007_205600", missing.Folder);
        Assert.Equal("请先回填中转站", NoteStagePresence.MissingFolderNotice);
        Assert.Contains("请先回填中转站", NoteStagePresence.MissingFolderDetail, StringComparison.Ordinal);

        var workList = NoteCatalog.Discover(profile, Array.Empty<StageItem>());
        var presentWork = Assert.Single(workList, item => item.Id == "20261005_125603");
        var missingWork = Assert.Single(workList, item => item.Id == "20261007_205600");
        Assert.False(presentWork.MissingStageFolder);
        Assert.True(missingWork.MissingStageFolder);
        Assert.Equal("缺夹的一篇", missingWork.Title);
        Assert.Equal("note", missingWork.SourceKind);
        Assert.False(missingWork.SupportsAppend);
        Assert.Empty(missingWork.Media);
    }

    [Fact]
    public void MissingFolder_ListsEveryIndexEntryWhenChannelDirectoryIsAbsent()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-note-missing-all-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "PersonalSite", "src", "content"));
        var profile = new WorkspaceProfile
        {
            ResolvedRoot = root,
            StageRoot = "stage",
            StageFolderPattern = "Key",
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" },
            Channels =
            {
                new ChannelProfile { Key = "notes", Zh = "心得" }
            }
        };
        NoteIndexStore.Save(profile, IndexWith("20261018_205600", "n-only", hidden: false));

        var work = Assert.Single(NoteCatalog.Discover(profile, Array.Empty<StageItem>()));
        Assert.True(work.MissingStageFolder);
        Assert.Equal("20261018_205600", work.Id);
    }

    private static NoteIndexFile IndexWith(string folder, string slug, bool hidden)
    {
        return new NoteIndexFile
        {
            Notes = { PublishedEntry(folder, slug, "一篇", "", hidden) }
        };
    }

    private static NoteEntry PublishedEntry(string folder, string slug, string title, string summary, bool hidden = false)
    {
        return new NoteEntry
        {
            Folder = folder,
            Slug = slug,
            Title = title,
            Summary = summary,
            Draft = false,
            Hidden = hidden
        };
    }

    private static NoteConfig Config(string title, string slug)
    {
        return new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Title = title,
            Slug = slug,
            Summary = ""
        };
    }

    private static string TempFolder(string folderName)
    {
        var folder = Path.Combine(Path.GetTempPath(), "sms-note-drift-" + Guid.NewGuid().ToString("N"), folderName);
        Directory.CreateDirectory(folder);
        return folder;
    }
}
