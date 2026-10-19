using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 标题与副标题分开保存，展示名自动拼接。已发布篇保存后变脏，并可标成待更新。
/// </summary>
public sealed class NoteTitleSaveTests
{
    [Fact]
    public void ComposeDisplayTitle_JoinsWithEmDashOnlyWhenSubtitleExists()
    {
        Assert.Equal("主标题——副标题", NoteRules.ComposeDisplayTitle(" 主标题 ", " 副标题 "));
        Assert.Equal("主标题", NoteRules.ComposeDisplayTitle("主标题", "  "));
        Assert.Equal("", NoteRules.ComposeDisplayTitle("  ", "副标题"));
        Assert.Equal(NoteRules.TitleSeparator, "——");
    }

    [Fact]
    public void CheckDisplayTitle_RejectsManualSeparatorAndEmptyMain()
    {
        var split = NoteRules.CheckDisplayTitle("主标题——已经拼好", "副标题");
        Assert.Equal(NoteFolderNameVerdict.Block, split.Verdict);
        Assert.Contains("不要写", split.Message, StringComparison.Ordinal);

        var empty = NoteRules.CheckDisplayTitle("  ", "副标题");
        Assert.Equal(NoteFolderNameVerdict.Block, empty.Verdict);
        Assert.Contains("标题不能为空", empty.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncTitle_KeepsStoredTitlesWhenHeadingDiffers()
    {
        var folder = TempFolder("20261005_125603");
        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 正文里的旧标题\n\n原文。\n");
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Title = "主标题——副标题",
            MainTitle = "主标题",
            Subtitle = "副标题",
            Slug = "n-20261005-125603"
        });

        var synced = NoteConfigStore.SyncTitle(folder, "20261005_125603");

        Assert.Equal("主标题", synced.MainTitle);
        Assert.Equal("副标题", synced.Subtitle);
        Assert.Equal("主标题——副标题", synced.Title);
    }

    [Fact]
    public void Plan_UsesStoredTitlesInsteadOfHeading()
    {
        var stage = TempDir();
        var folder = Path.Combine(stage, "20261005_125603");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "正文.md"), "# 正文里的旧标题\n\n原文。\n");
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Title = "主标题——副标题",
            MainTitle = "主标题",
            Subtitle = "副标题",
            Slug = "n-20261005-125603"
        });

        var plan = NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null);

        Assert.True(plan.Ok, plan.Error);
        Assert.Equal("主标题", plan.MainTitle);
        Assert.Equal("副标题", plan.Subtitle);
        Assert.Equal("主标题——副标题", plan.Title);
    }

    [Fact]
    public void SaveTitles_PublishedNote_BecomesDirtyAndCanQueueUpdate()
    {
        var (profile, folder, slug) = CreatePublishedNote("旧标题", "");
        var notesDir = NoteIndexStore.NotesDir(profile)!;
        var sitePath = Path.Combine(notesDir, slug + ".md");
        File.WriteAllText(sitePath, "原文。\n");
        var beforeSite = File.ReadAllText(sitePath);
        var indexBefore = NoteIndexStore.Load(profile);
        Assert.False(NoteSiteDrift.HasUnsyncedSiteChanges(folder, indexBefore, notesDir));
        Assert.True(NotePublishCopy.IsPublished(indexBefore, "20261005_125603"));

        var saved = NoteMetaEditor.SaveTitles(
            profile,
            folder,
            "主标题",
            "副标题",
            Array.Empty<string>(),
            new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.True(saved.Ok, saved.Message);
        Assert.True(saved.Changed);
        Assert.True(saved.PendingUpdate);
        Assert.Equal("20261005_125603", saved.FolderName);
        Assert.True(Directory.Exists(folder));
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Equal("主标题", config.MainTitle);
        Assert.Equal("副标题", config.Subtitle);
        Assert.Equal("主标题——副标题", config.Title);
        Assert.Equal(beforeSite, File.ReadAllText(sitePath));

        var index = NoteIndexStore.Load(profile);
        Assert.Equal("旧标题", index.Notes[0].Title);
        Assert.Equal("", index.Notes[0].MainTitle);
        Assert.Equal("", index.Notes[0].Subtitle);
        Assert.Equal("20261005_125603", index.Notes[0].Folder);
        Assert.True(NotePublishCopy.IsPublished(index, saved.FolderName));
        Assert.Equal("更新", NotePublishCopy.Verb(true));
        Assert.True(NoteSiteDrift.HasUnsyncedSiteChanges(folder, index, notesDir));

        var markPath = Path.Combine(profile.ResolvedRoot, "marks.json");
        NoteUpdateQueue.MarkBody(profile, saved.FolderName, markPath);
        var mark = MarkDraftStore.Find(
            MarkDraftStore.Load(markPath),
            profile,
            MarkDraftStore.StageKey(NoteUpdateQueue.BodyStageRel(profile, saved.FolderName)));
        Assert.NotNull(mark);
        Assert.Equal(MediaIntentCodes.StageIngest, mark!.Intent);

        var outcome = NotePublisher.Publish(profile, folder, null, null, (_, _, _) => true, (_, _, _) => true);
        Assert.True(string.IsNullOrWhiteSpace(outcome.Error), outcome.Error);
        var published = NoteIndexStore.Load(profile);
        Assert.Equal("主标题——副标题", published.Notes[0].Title);
        Assert.Equal("主标题", published.Notes[0].MainTitle);
        Assert.Equal("副标题", published.Notes[0].Subtitle);
        Assert.False(NoteSiteDrift.HasUnsyncedSiteChanges(folder, published, notesDir));
    }

    [Fact]
    public void SaveTitles_UnpublishedOrUnchanged_DoesNotQueueUpdate()
    {
        var (profile, folder, _) = CreatePublishedNote("只有标题", "");
        var same = NoteMetaEditor.SaveTitles(
            profile,
            folder,
            "只有标题",
            "",
            Array.Empty<string>(),
            new DateTime(2026, 10, 8, 9, 0, 0));
        Assert.True(same.Ok, same.Message);
        Assert.False(same.Changed);
        Assert.False(same.PendingUpdate);

        var index = NoteIndexStore.Load(profile);
        index.Notes.Clear();
        NoteIndexStore.Save(profile, index);
        var saved = NoteMetaEditor.SaveTitles(
            profile,
            folder,
            "新的标题",
            "",
            Array.Empty<string>(),
            new DateTime(2026, 10, 8, 9, 10, 11));
        Assert.True(saved.Ok, saved.Message);
        Assert.True(saved.Changed);
        Assert.False(saved.PendingUpdate);
        Assert.False(NotePublishCopy.IsPublished(NoteIndexStore.Load(profile), saved.FolderName));
        Assert.Equal("上页", NotePublishCopy.Verb(false));
    }

    [Fact]
    public void SaveTitles_WithoutSubtitle_KeepsMainTitleOnly()
    {
        var (profile, folder, _) = CreatePublishedNote("旧标题", "旧副标题");
        var saved = NoteMetaEditor.SaveTitles(
            profile,
            folder,
            "只有标题",
            "   ",
            Array.Empty<string>(),
            new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.True(saved.Ok, saved.Message);
        Assert.True(saved.PendingUpdate);
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Equal("只有标题", config.MainTitle);
        Assert.Equal("", config.Subtitle);
        Assert.Equal("只有标题", config.Title);
    }

    /// <summary>
    /// 建一篇已发布心得。站点正文由调用方按需要补上。
    /// </summary>
    private static (WorkspaceProfile Profile, string Folder, string Slug) CreatePublishedNote(string title, string subtitle)
    {
        var root = TempDir();
        var channel = new ChannelProfile { Key = "notes", Zh = "心得" };
        var profile = new WorkspaceProfile
        {
            ProfilePath = Path.Combine(root, "profile.json"),
            ResolvedRoot = root,
            StageRoot = "stage",
            StageFolderPattern = "ZhKey",
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" },
            Channels = new List<ChannelProfile> { channel }
        };
        var channelDir = Path.Combine(
            WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot),
            WorkspaceProfileLoader.ChannelStageRelative(profile, channel));
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        File.WriteAllText(Path.Combine(folder, "正文.md"), "# " + title + "\n\n原文。\n");
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        config.Title = title;
        config.MainTitle = title;
        config.Subtitle = subtitle;
        config.Summary = "";
        NoteConfigStore.Save(folder, config);
        NoteIndexStore.Save(profile, new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = "20261005_125603",
                    Slug = config.Slug,
                    Title = title,
                    MainTitle = string.IsNullOrWhiteSpace(subtitle) ? "" : title,
                    Subtitle = subtitle,
                    Draft = false,
                    Summary = ""
                }
            }
        });
        return (profile, folder, config.Slug);
    }

    private static string TempFolder(string folderName)
    {
        var folder = Path.Combine(TempDir(), folderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-note-title-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
