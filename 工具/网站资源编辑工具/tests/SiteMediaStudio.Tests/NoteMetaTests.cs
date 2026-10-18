using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteMetaTests
{
    [Fact]
    public void ParseFolderTitles_SplitsOnTheFirstDoubleEmDash()
    {
        var split = NoteRules.ParseFolderTitles("  甲——乙——丙  ");
        Assert.Equal("甲", split.MainTitle);
        Assert.Equal("乙——丙", split.Subtitle);
        Assert.Equal("甲——乙——丙", split.DisplayTitle);

        var plain = NoteRules.ParseFolderTitles("只有主标题");
        Assert.Equal("只有主标题", plain.MainTitle);
        Assert.Equal("", plain.Subtitle);
        Assert.Equal("只有主标题", plain.DisplayTitle);

        var ascii = NoteRules.ParseFolderTitles("甲--乙");
        Assert.Equal("甲--乙", ascii.MainTitle);
        Assert.Equal("", ascii.Subtitle);
        Assert.Contains("——", NoteRules.CheckFolderName("甲--乙").Message, StringComparison.Ordinal);

        var single = NoteRules.ParseFolderTitles("甲\u2014乙");
        Assert.Equal("甲\u2014乙", single.MainTitle);
        Assert.Equal("", single.Subtitle);
    }

    [Fact]
    public void CheckFolderName_WarnsAt60AndBlocksAt80()
    {
        Assert.Equal(NoteFolderNameVerdict.Ok, NoteRules.CheckFolderName(new string('字', 59)).Verdict);
        var mixed = new string('字', 58) + "Ab";
        Assert.Equal(60, mixed.Length);
        var warn = NoteRules.CheckFolderName(mixed);
        Assert.Equal(NoteFolderNameVerdict.Warn, warn.Verdict);
        Assert.Contains("夹名偏长", warn.Message, StringComparison.Ordinal);

        Assert.Equal(NoteFolderNameVerdict.Warn, NoteRules.CheckFolderName(new string('字', 79)).Verdict);
        var block = NoteRules.CheckFolderName(new string('字', 80));
        Assert.Equal(NoteFolderNameVerdict.Block, block.Verdict);
        Assert.Contains("80", block.Message, StringComparison.Ordinal);

        var emptyMain = NoteRules.CheckFolderName("——乙");
        Assert.Equal(NoteFolderNameVerdict.Block, emptyMain.Verdict);
        Assert.Contains("主标题不能为空", emptyMain.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MigratesLegacyModifiedAtIntoRevisions()
    {
        var folder = TempDir();
        var path = Path.Combine(folder, "心得.json");
        File.WriteAllText(path, """
            {
              "createdAt": "2026-10-05 12:56:03",
              "modifiedAt": ["2026-10-06 01:02:03", "  "],
              "title": "旧题",
              "slug": "n-20261005-125603",
              "summary": "导语"
            }
            """);
        File.WriteAllText(Path.Combine(folder, "正文.md"), "没有新字段\n");

        var loaded = NoteConfigStore.Load(path);
        Assert.Equal("旧题", loaded.Title);
        Assert.Equal("", loaded.MainTitle);
        Assert.Single(loaded.Revisions);
        Assert.Equal("2026-10-06 01:02:03", loaded.Revisions[0].At);
        Assert.Equal("", loaded.Revisions[0].Summary);
        Assert.Equal(NoteRevisionSource.Publish, loaded.Revisions[0].Source);

        var saved = NoteConfigStore.Ensure(folder, "20261005_125603");
        var json = File.ReadAllText(path);
        Assert.DoesNotContain("modifiedAt", json, StringComparison.Ordinal);
        Assert.Contains("\"revisions\"", json, StringComparison.Ordinal);
        Assert.Equal("n-20261005-125603", saved.Slug);
        Assert.Equal("2026-10-06 01:02:03", NoteConfigStore.RevisedAt(saved));
    }

    [Fact]
    public void Plan_UsesFolderTitleWithoutHeadingAndKeepsAsciiSlug()
    {
        var stage = TempDir();
        var folder = Path.Combine(stage, "心得（notes）", "甲——乙");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "正文.md"), "正文没有一级标题。\n");
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Slug = "n-20261005-125603",
            Title = "旧的一级标题",
            Revisions =
            {
                new NoteRevision
                {
                    At = "2026-10-06 01:02:03",
                    Summary = "",
                    Source = NoteRevisionSource.Publish
                }
            }
        });

        var plan = NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null);

        Assert.True(plan.Ok, plan.Error);
        Assert.Equal("甲", plan.MainTitle);
        Assert.Equal("乙", plan.Subtitle);
        Assert.Equal("甲——乙", plan.Title);
        Assert.Equal("n-20261005-125603", plan.Slug);
        Assert.Equal("2026-10-05", plan.Date);
        Assert.Equal("2026-10-05 12:56:03", plan.CreatedAt);
        Assert.Equal("2026-10-06 01:02:03", plan.RevisedAt);
        Assert.Equal("正文没有一级标题。\n", File.ReadAllText(Path.Combine(folder, "正文.md")));
        var stored = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Equal("甲——乙", stored.Title);
        Assert.Equal("n-20261005-125603", stored.Slug);
    }

    [Fact]
    public void Plan_StillRequiresHeadingWhenFolderIsOnlyATimestamp()
    {
        var stage = TempDir();
        var folder = Path.Combine(stage, "心得（notes）", "20261005_125603");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "正文.md"), "没有标题\n");
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Slug = "n-20261005-125603"
        });

        var plan = NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null);

        Assert.False(plan.Ok);
        Assert.Contains("一级标题", plan.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_BlocksEmptyMainTitleAndOverlongFolder()
    {
        var stage = TempDir();
        var empty = Path.Combine(stage, "——乙");
        Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(empty, "正文.md"), "# 一篇\n");
        NoteConfigStore.Save(empty, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Slug = "n-20261005-125603",
            Title = "一篇"
        });
        Assert.Contains("主标题不能为空", NoteRules.Plan(empty, stage, new NoteIndexFile(), null, null).Error, StringComparison.Ordinal);

        var longName = new string('字', 80);
        var longFolder = Path.Combine(stage, longName);
        Directory.CreateDirectory(longFolder);
        File.WriteAllText(Path.Combine(longFolder, "正文.md"), "正文\n");
        NoteConfigStore.Save(longFolder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Slug = "n-20261005-125603"
        });
        Assert.Contains("80", NoteRules.Plan(longFolder, stage, new NoteIndexFile(), null, null).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_RenamesFolderKeepsSlugAndProjectsIndex()
    {
        var root = TempDir();
        var channel = new ChannelProfile { Key = "notes", Zh = "心得" };
        var profile = new WorkspaceProfile
        {
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
        var config = NoteConfigStore.Load(Path.Combine(folder, NoteConfigStore.FileName));
        config.Title = "旧题";
        config.Revisions.Add(new NoteRevision
        {
            At = "2026-10-06 01:00:00",
            Summary = "旧说明",
            Source = NoteRevisionSource.Publish
        });
        NoteConfigStore.Save(folder, config);
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = "20261005_125603",
                    Slug = "n-20261005-125603",
                    Title = "旧题",
                    Date = "2026-10-05"
                }
            }
        };
        NoteIndexStore.Save(profile, index);

        var blocked = NoteMetaEditor.Save(profile, folder, new string('字', 80), new[] { "不会写入" }, new DateTime(2026, 10, 8, 9, 0, 0));
        Assert.False(blocked.Ok);
        Assert.True(Directory.Exists(folder));

        var warned = new string('字', 57) + "——乙";
        Assert.Equal(60, warned.Length);
        var saved = NoteMetaEditor.Save(
            profile,
            folder,
            warned,
            new[] { "改正后的说明" },
            new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.True(saved.Ok, saved.Message);
        Assert.Contains("夹名偏长", saved.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(folder));
        Assert.True(Directory.Exists(saved.FolderFullPath));
        var next = NoteConfigStore.Load(Path.Combine(saved.FolderFullPath, NoteConfigStore.FileName));
        Assert.Equal("n-20261005-125603", next.Slug);
        Assert.Equal(new string('字', 57), next.MainTitle);
        Assert.Equal("乙", next.Subtitle);
        Assert.Equal(warned, next.Title);
        Assert.Equal("改正后的说明", next.Revisions[0].Summary);
        Assert.Equal(NoteRevisionSource.Manual, next.Revisions[0].Source);
        Assert.Equal("2026-10-06 01:00:00", next.Revisions[0].At);
        Assert.Equal("更新标题", next.Revisions[1].Summary);
        Assert.Equal(NoteRevisionSource.Manual, next.Revisions[1].Source);
        Assert.Equal("2026-10-08 09:10:11", next.Revisions[1].At);

        var projected = NoteIndexStore.Load(profile);
        Assert.Equal(NoteIndexFile.CurrentVersion, projected.Version);
        Assert.Equal(warned, projected.Notes[0].Folder);
        Assert.Equal("n-20261005-125603", projected.Notes[0].Slug);
        Assert.Equal("2026-10-05", projected.Notes[0].Date);
        Assert.Equal(new string('字', 57), projected.Notes[0].MainTitle);
        Assert.Equal("乙", projected.Notes[0].Subtitle);
        Assert.Equal("2026-10-08 09:10:11", projected.Notes[0].RevisedAt);
    }

    [Fact]
    public void Publish_WritesTitlesAndRevisionsIntoIndex()
    {
        var root = TempDir();
        var stage = Path.Combine(root, "stage");
        var folder = Path.Combine(stage, "只有主标题");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "正文.md"), "没有一级标题也可以上页。\n");
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Slug = "n-20261005-125603",
            Summary = "导语"
        });
        var profile = new WorkspaceProfile
        {
            ResolvedRoot = root,
            StageRoot = "stage",
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" }
        };

        var outcome = NotePublisher.Publish(profile, folder, null, null, (_, _, _) => true, (_, _, _) => true);

        Assert.True(string.IsNullOrWhiteSpace(outcome.Error), outcome.Error);
        var index = NoteIndexStore.Load(profile);
        Assert.Equal(2, index.Version);
        var entry = Assert.Single(index.Notes);
        Assert.Equal("只有主标题", entry.MainTitle);
        Assert.Equal("", entry.Subtitle);
        Assert.Equal("只有主标题", entry.Title);
        Assert.Equal("n-20261005-125603", entry.Slug);
        Assert.Equal("2026-10-05", entry.Date);
        Assert.Equal("2026-10-05 12:56:03", entry.CreatedAt);
        Assert.Equal("2026-10-05 12:56:03", entry.RevisedAt);
        Assert.Empty(entry.Revisions);
        var json = File.ReadAllText(NoteIndexStore.IndexPath(profile)!);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("只有主标题", document.RootElement.GetProperty("notes")[0].GetProperty("mainTitle").GetString());
    }

    [Fact]
    public void AppendModified_AppendsEmptyPublishRevision()
    {
        var root = TempDir();
        var channel = new ChannelProfile { Key = "notes", Zh = "心得" };
        var profile = new WorkspaceProfile
        {
            ResolvedRoot = root,
            StageRoot = "stage",
            StageFolderPattern = "ZhKey",
            Channels = new List<ChannelProfile> { channel },
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" }
        };
        var channelDir = Path.Combine(
            WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot),
            WorkspaceProfileLoader.ChannelStageRelative(profile, channel));
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var body = Path.Combine(folder, "正文.md");
        File.WriteAllText(body, "有改动\n");
        File.SetLastWriteTime(body, new DateTime(2026, 10, 18, 12, 0, 0));

        var count = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.Equal(1, count);
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        var revision = Assert.Single(config.Revisions);
        Assert.Equal("2026-10-08 09:10:11", revision.At);
        Assert.Equal("", revision.Summary);
        Assert.Equal(NoteRevisionSource.Publish, revision.Source);
        Assert.DoesNotContain("modifiedAt", File.ReadAllText(Path.Combine(folder, "心得.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultSlug_KeepsTimestampShapeAndDoesNotEmbedChinese()
    {
        Assert.Equal("n-20261005-125603", NoteConfigStore.DefaultSlug("20261005_125603"));
        Assert.Equal(
            "n-20261005-125603",
            NoteConfigStore.DefaultSlug("甲——乙", "2026-10-05 12:56:03"));
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-note-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
