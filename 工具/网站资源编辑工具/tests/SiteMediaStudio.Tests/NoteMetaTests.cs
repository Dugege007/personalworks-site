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
    public void AppendModified_BodyChange_WritesAssistantSummaryAndSkipsNoOp()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var body = Path.Combine(folder, "正文.md");
        File.WriteAllText(body, "# 传感器阈值\n\n补了一段说明。\n\n![](图.png)\n");
        File.WriteAllBytes(Path.Combine(folder, "图.png"), [1, 2, 3]);

        var count = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.Equal(1, count);
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        var revision = Assert.Single(config.Revisions);
        Assert.Equal("2026-10-08 09:10:11", revision.At);
        Assert.Equal("新增小节「传感器阈值」，新增 1 张配图，更新了正文。", revision.Summary);
        Assert.Equal(NoteRevisionSource.Assistant, revision.Source);
        Assert.DoesNotContain("modifiedAt", File.ReadAllText(Path.Combine(folder, "心得.json")), StringComparison.Ordinal);

        File.WriteAllText(body, "# 传感器阈值\r\n\r\n补了一段说明。  \r\n\r\n![](图.png)\r\n");
        File.SetLastWriteTime(body, new DateTime(2026, 10, 18, 12, 0, 0));
        var logPath = Path.Combine(folder, "run.log");
        int again;
        using (RunLog.UsePath(logPath))
        {
            again = NoteConfigStore.AppendModified(
                profile,
                new DateTime(2026, 10, 8, 10, 0, 0),
                new ThrowingRevisionSummaryGenerator());
        }

        Assert.Equal(0, again);
        Assert.False(File.Exists(logPath));
        var skipped = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Single(skipped.Revisions);
        Assert.Equal("新增小节「传感器阈值」，新增 1 张配图，更新了正文。", skipped.Revisions[0].Summary);
    }

    [Fact]
    public void AppendModified_EditedSummary_StaysManualWhenContentChangesAgain()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var body = Path.Combine(folder, "正文.md");
        File.WriteAllText(body, "第一段。\n");
        var first = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));
        Assert.Equal(1, first);

        var saved = NoteMetaEditor.Save(
            profile,
            folder,
            Path.GetFileName(folder),
            new[] { "手写的说明" },
            new DateTime(2026, 10, 8, 9, 30, 0));
        Assert.True(saved.Ok, saved.Message);

        File.WriteAllText(body, "第一段。\n\n第二段。\n");
        var second = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 11, 0, 0));

        Assert.Equal(1, second);
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Equal(2, config.Revisions.Count);
        Assert.Equal("手写的说明", config.Revisions[0].Summary);
        Assert.Equal(NoteRevisionSource.Manual, config.Revisions[0].Source);
        Assert.Equal("2026-10-08 09:10:11", config.Revisions[0].At);
        Assert.Equal("更新了正文。", config.Revisions[1].Summary);
        Assert.Equal(NoteRevisionSource.Assistant, config.Revisions[1].Source);
        Assert.Equal("2026-10-08 11:00:00", config.Revisions[1].At);
    }

    [Fact]
    public void AppendModified_KeepsEmptyLegacySummary()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        config.Revisions.Add(new NoteRevision
        {
            At = "2026-10-06 01:02:03",
            Summary = "",
            Source = NoteRevisionSource.Publish
        });
        NoteConfigStore.Save(folder, config);
        File.WriteAllText(Path.Combine(folder, "正文.md"), "有改动\n");

        var count = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.Equal(1, count);
        var stored = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Equal("", stored.Revisions[0].Summary);
        Assert.Equal(NoteRevisionSource.Publish, stored.Revisions[0].Source);
        Assert.Equal("更新了正文。", stored.Revisions[1].Summary);
        Assert.Equal(NoteRevisionSource.Assistant, stored.Revisions[1].Source);
    }

    [Fact]
    public void AppendModified_WithoutBaseline_UsesFallbackWithoutInventingHeadings()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        config.RevisionBaseline = null;
        NoteConfigStore.Save(folder, config);
        var body = Path.Combine(folder, "正文.md");
        File.WriteAllText(body, "# 传感器阈值\n\n正文\n");
        File.SetLastWriteTime(body, new DateTime(2026, 10, 18, 12, 0, 0));
        var logPath = Path.Combine(folder, "run.log");

        int count;
        using (RunLog.UsePath(logPath))
        {
            count = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));
        }

        Assert.Equal(1, count);
        var stored = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        var revision = Assert.Single(stored.Revisions);
        Assert.Equal(NoteRevisionSummaries.Fallback, revision.Summary);
        Assert.Equal(NoteRevisionSource.Assistant, revision.Source);
        Assert.DoesNotContain("传感器阈值", revision.Summary, StringComparison.Ordinal);
        Assert.Contains("没有可对照的上一版", File.ReadAllText(logPath), StringComparison.Ordinal);
        Assert.NotNull(stored.RevisionBaseline);
    }

    [Fact]
    public void AppendModified_UnchangedLegacy_SeedsBaselineWithoutRevision()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var body = Path.Combine(folder, "正文.md");
        File.WriteAllText(body, "原来的正文\n");
        File.SetLastWriteTime(body, new DateTime(2026, 10, 5, 12, 56, 2));
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        config.RevisionBaseline = null;
        NoteConfigStore.Save(folder, config);

        var seeded = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 0, 0));

        Assert.Equal(0, seeded);
        var baseline = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Empty(baseline.Revisions);
        Assert.Equal("原来的正文", baseline.RevisionBaseline?.Body);

        File.WriteAllText(body, "# 后来的小节\n\n原来的正文\n");
        var appended = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.Equal(1, appended);
        var stored = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        var revision = Assert.Single(stored.Revisions);
        Assert.Equal("新增小节「后来的小节」。", revision.Summary);
        Assert.Equal(NoteRevisionSource.Assistant, revision.Source);
    }

    [Fact]
    public void AppendModified_GeneratorFailure_StillSavesFallback()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        File.WriteAllText(Path.Combine(folder, "正文.md"), "有改动\n");
        var logPath = Path.Combine(folder, "run.log");

        int count;
        using (RunLog.UsePath(logPath))
        {
            count = NoteConfigStore.AppendModified(
                profile,
                new DateTime(2026, 10, 8, 9, 10, 11),
                new ThrowingRevisionSummaryGenerator());
        }

        Assert.Equal(1, count);
        var revision = Assert.Single(NoteConfigStore.Load(Path.Combine(folder, "心得.json")).Revisions);
        Assert.Equal(NoteRevisionSummaries.Fallback, revision.Summary);
        Assert.Equal(NoteRevisionSource.Assistant, revision.Source);
        var log = File.ReadAllText(logPath);
        Assert.Contains("生成失败", log, StringComparison.Ordinal);
        Assert.Contains("模型不可用", log, StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(folder, "正文.md"), "又改了一段。\n");
        using (RunLog.UsePath(logPath))
        {
            count = NoteConfigStore.AppendModified(
                profile,
                new DateTime(2026, 10, 8, 9, 20, 0),
                new BlankRevisionSummaryGenerator());
        }

        Assert.Equal(1, count);
        var stored = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        Assert.Equal(NoteRevisionSummaries.Fallback, stored.Revisions[1].Summary);
        Assert.Equal(NoteRevisionSource.Assistant, stored.Revisions[1].Source);
        Assert.Contains("生成结果为空", File.ReadAllText(logPath), StringComparison.Ordinal);
    }

    [Fact]
    public void AppendModified_LongSummary_IsCapped()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        File.WriteAllText(Path.Combine(folder, "正文.md"), "有改动\n");

        var count = NoteConfigStore.AppendModified(
            profile,
            new DateTime(2026, 10, 8, 9, 10, 11),
            new LongRevisionSummaryGenerator());

        Assert.Equal(1, count);
        var revision = Assert.Single(NoteConfigStore.Load(Path.Combine(folder, "心得.json")).Revisions);
        Assert.Equal(NoteRevisionSummaries.MaxLength, revision.Summary.Length);
        Assert.EndsWith("…", revision.Summary);
        Assert.Equal(NoteRevisionSource.Assistant, revision.Source);
    }

    [Fact]
    public void AppendModified_UsesReplacementGenerator()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        File.WriteAllText(Path.Combine(folder, "正文.md"), "有改动\n");
        var previous = NoteConfigStore.SummaryGenerator;
        try
        {
            NoteConfigStore.SummaryGenerator = new FixedRevisionSummaryGenerator("换成了另一套生成器。");
            var count = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));
            Assert.Equal(1, count);
            var revision = Assert.Single(NoteConfigStore.Load(Path.Combine(folder, "心得.json")).Revisions);
            Assert.Equal("换成了另一套生成器。", revision.Summary);
            Assert.Equal(NoteRevisionSource.Assistant, revision.Source);
        }
        finally
        {
            NoteConfigStore.SummaryGenerator = previous;
        }
    }

    [Fact]
    public void Save_TitleRevision_DoesNotDuplicateOnNextPublish()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        config.Title = "旧题";
        config.RevisionBaseline!.Title = "旧题";
        NoteConfigStore.Save(folder, config);

        var saved = NoteMetaEditor.Save(
            profile,
            folder,
            "新题",
            Array.Empty<string>(),
            new DateTime(2026, 10, 8, 9, 10, 11));

        Assert.True(saved.Ok, saved.Message);
        var renamed = NoteConfigStore.Load(Path.Combine(saved.FolderFullPath, "心得.json"));
        var titleRevision = Assert.Single(renamed.Revisions);
        Assert.Equal("更新标题", titleRevision.Summary);
        Assert.Equal(NoteRevisionSource.Manual, titleRevision.Source);
        Assert.Equal("新题", renamed.RevisionBaseline?.Title);

        var again = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 10, 0, 0));

        Assert.Equal(0, again);
        Assert.Single(NoteConfigStore.Load(Path.Combine(saved.FolderFullPath, "心得.json")).Revisions);
    }

    [Fact]
    public void AppendModified_TitleChange_WritesAssistantSummaryOnce()
    {
        var (profile, channelDir) = CreateNotesWorkspace();
        var folder = NoteRules.CreateFolder(channelDir, new DateTime(2026, 10, 5, 12, 56, 3));
        var config = NoteConfigStore.Load(Path.Combine(folder, "心得.json"));
        config.Title = "新的标题";
        NoteConfigStore.Save(folder, config);

        var count = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 9, 10, 11));
        var again = NoteConfigStore.AppendModified(profile, new DateTime(2026, 10, 8, 10, 0, 0));

        Assert.Equal(1, count);
        Assert.Equal(0, again);
        var revision = Assert.Single(NoteConfigStore.Load(Path.Combine(folder, "心得.json")).Revisions);
        Assert.Equal("设置展示标题「新的标题」。", revision.Summary);
        Assert.Equal(NoteRevisionSource.Assistant, revision.Source);
    }

    [Fact]
    public void DefaultSlug_KeepsTimestampShapeAndDoesNotEmbedChinese()
    {
        Assert.Equal("n-20261005-125603", NoteConfigStore.DefaultSlug("20261005_125603"));
        Assert.Equal(
            "n-20261005-125603",
            NoteConfigStore.DefaultSlug("甲——乙", "2026-10-05 12:56:03"));
    }

    private static (WorkspaceProfile Profile, string ChannelDir) CreateNotesWorkspace()
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
        return (profile, channelDir);
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-note-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 用来确认失败不会挡住保存。
    /// </summary>
    private sealed class ThrowingRevisionSummaryGenerator : IRevisionSummaryGenerator
    {
        public string Generate(NoteRevisionDiff diff)
        {
            throw new InvalidOperationException("模型不可用");
        }
    }

    /// <summary>
    /// 返回空白，调用方应改用回退句。无差异时不应被调用。
    /// </summary>
    private sealed class BlankRevisionSummaryGenerator : IRevisionSummaryGenerator
    {
        public string Generate(NoteRevisionDiff diff)
        {
            return "  ";
        }
    }

    /// <summary>
    /// 返回超长说明，调用方应截断。
    /// </summary>
    private sealed class LongRevisionSummaryGenerator : IRevisionSummaryGenerator
    {
        public string Generate(NoteRevisionDiff diff)
        {
            return new string('改', 120);
        }
    }

    /// <summary>
    /// 固定文案，用来确认启动时换上的生成器会接到发布路径。
    /// </summary>
    private sealed class FixedRevisionSummaryGenerator : IRevisionSummaryGenerator
    {
        private readonly string mSummary;

        public FixedRevisionSummaryGenerator(string summary)
        {
            mSummary = summary;
        }

        public string Generate(NoteRevisionDiff diff)
        {
            return mSummary;
        }
    }
}
