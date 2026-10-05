using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteRulesTests
{
    [Fact]
    public void Resolve_FindsProgramFilesTyporaWhenInstalled()
    {
        var exe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Typora",
            "Typora.exe");
        if (!File.Exists(exe))
        {
            return;
        }

        Assert.Equal(exe, NoteTypora.Resolve(null));
    }

    [Fact]
    public void DiskSearch_FindsExeUnderNestedFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-exe-search-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "skip", "apps");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(Path.Combine(root, "$Recycle.Bin"));
        var exe = Path.Combine(nested, "Typora.exe");
        File.WriteAllBytes(exe, [0]);
        File.WriteAllBytes(Path.Combine(root, "$Recycle.Bin", "Typora.exe"), [0]);
        try
        {
            Assert.Equal(exe, ExeDiskSearch.FindFirst(new[] { "Typora.exe" }, new[] { root }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AllocateFolderName_SkipsExistingSecond()
    {
        var root = TempDir();
        var now = new DateTime(2026, 10, 5, 12, 56, 3);
        Directory.CreateDirectory(Path.Combine(root, "20261005_125603"));
        var name = NoteRules.AllocateFolderName(root, now);
        Assert.Equal("20261005_125604", name);
    }

    [Fact]
    public void CreateFolder_WritesBodyAndReadyDir()
    {
        var root = TempDir();
        var folder = NoteRules.CreateFolder(root, new DateTime(2026, 10, 5, 12, 56, 3));
        Assert.Equal("", File.ReadAllText(Path.Combine(folder, "正文.md")));
        Assert.True(File.Exists(Path.Combine(folder, "心得.json")));
        Assert.True(Directory.Exists(Path.Combine(folder, ".site-ready")));
        Assert.Contains("2026-10-05 12:56:03", File.ReadAllText(Path.Combine(folder, "心得.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_RejectsDraftEmptySlugOutsideImageAndBlockedExt()
    {
        var stage = TempDir();
        var folder = Path.Combine(stage, "心得（notes）", "20261005_125603");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "正文.md"), "没有标题\n");
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Slug = "hello"
        });
        Assert.Contains("一级标题", NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null).Error, StringComparison.Ordinal);

        WriteNote(folder, "Bad_Slug", "# 一篇\n\n文字");
        Assert.Contains("slug", NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null).Error, StringComparison.Ordinal);

        WriteNote(folder, "hello", "# 一篇\n\n![外](../secret.png)");
        var outside = NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null);
        Assert.False(outside.Ok);
        Assert.Contains("夹外", outside.Error, StringComparison.Ordinal);

        File.WriteAllBytes(Path.Combine(folder, "clip.mp4"), new byte[] { 0 });
        WriteNote(folder, "hello", "# 一篇\n\n文字");
        Assert.Contains("不能上页", NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_PublishesEveryImageButBodyOnlyRewritesReferenced()
    {
        var stage = TempDir();
        var folder = Path.Combine(stage, "心得（notes）", "20261005_125603");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "used.png"), TinyPng);
        File.WriteAllBytes(Path.Combine(folder, "extra.png"), TinyPng);
        var source = "# 一篇\n\n见 ![用](used.png)\n";
        WriteNote(folder, "hello", source);
        var plan = NoteRules.Plan(folder, stage, new NoteIndexFile(), null, null);
        Assert.True(plan.Ok, plan.Error);
        Assert.Equal(2, plan.Images.Count);
        Assert.Contains("notes/hello/used.webp", plan.RewrittenMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("extra.webp", plan.RewrittenMarkdown, StringComparison.Ordinal);
        Assert.Equal(new[] { "notes/hello/used.webp" }, plan.PreviewObjects);
        Assert.Equal(source, File.ReadAllText(Path.Combine(folder, "正文.md")));
    }

    [Fact]
    public void Plan_KeepsStarsAcrossRepublish()
    {
        var stage = TempDir();
        var folder = Path.Combine(stage, "心得（notes）", "20261005_125603");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "used.png"), TinyPng);
        WriteNote(folder, "hello", "# 一篇\n\n![用](used.png)\n");
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = "20261005_125603",
                    Slug = "hello",
                    Images =
                    {
                        new NoteImageMeta
                        {
                            StageRel = "心得（notes）/20261005_125603/used.png",
                            ObjectKey = "notes/hello/used.webp",
                            Stars = 4,
                            Tags = new List<string> { "层" }
                        }
                    },
                    Body = new NoteResourceMeta { Stars = 2 }
                }
            }
        };
        var plan = NoteRules.Plan(folder, stage, index, null, null);
        Assert.True(plan.Ok, plan.Error);
        Assert.Equal(4, plan.Images[0].Meta.Stars);
        Assert.Equal("层", plan.Images[0].Meta.Tags[0]);
        Assert.Equal(2, plan.Body.Stars);
    }

    [Fact]
    public void Plan_RejectsHiddenNote()
    {
        var stage = TempDir();
        var folder = Path.Combine(stage, "心得（notes）", "20261005_125603");
        Directory.CreateDirectory(folder);
        WriteNote(folder, "hello", "# 一篇\n\n文字\n");
        var index = new NoteIndexFile
        {
            Notes = { new NoteEntry { Folder = "20261005_125603", Slug = "hello", Hidden = true } }
        };
        Assert.Contains("隐藏", NoteRules.Plan(folder, stage, index, null, null).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Recycle_BlocksPublishedFolder()
    {
        var index = new NoteIndexFile
        {
            Notes = { new NoteEntry { Folder = "20261005_125603", Slug = "hello", Draft = false } }
        };
        Assert.NotNull(NoteRules.RecycleBlockReason(index, "20261005_125603"));
        Assert.Null(NoteRules.RecycleBlockReason(new NoteIndexFile(), "20261005_125603"));
    }

    [Fact]
    public void Hide_DropsVisitorFlagAndKeepsFolder()
    {
        var root = TempDir();
        Directory.CreateDirectory(Path.Combine(root, "PersonalSite", "src", "content"));
        var profile = new WorkspaceProfile
        {
            ResolvedRoot = root,
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" }
        };
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = "20261005_125603",
                    Slug = "hello",
                    Draft = false,
                    Images = { new NoteImageMeta { ObjectKey = "notes/hello/a.webp" } }
                }
            }
        };
        NoteIndexStore.Save(profile, index);
        Assert.Null(NoteCommands.Hide(profile, "20261005_125603"));
        var saved = NoteIndexStore.Load(profile);
        Assert.True(saved.Notes[0].Hidden);
        Assert.True(saved.Notes[0].Images[0].Hidden);
        Assert.Null(NoteCommands.Restore(profile, "20261005_125603"));
        Assert.False(NoteIndexStore.Load(profile).Notes[0].Hidden);
    }

    [Fact]
    public void ApplyPublishState_ListedBodyWithoutLedger_ShowsPublished()
    {
        var body = Stage("心得（notes）/20261005_021341/正文.md");
        var extra = Stage("心得（notes）/20261005_021341/extra.jpg");
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = "20261005_021341",
                    Slug = "n-20261005-021341",
                    Images =
                    {
                        new NoteImageMeta { StageRel = "心得（notes）/20261005_021341/shot.jpg" }
                    }
                }
            }
        };

        NoteCatalog.ApplyPublishState(new[] { body, extra }, index, "心得（notes）");

        Assert.Equal("已发布", PublishStatus.ForStage(body));
        Assert.False(PublishStatus.IsStageRemarkLocked(body));
        Assert.Equal("未上页", PublishStatus.ForStage(extra));
        Assert.False(PublishStatus.IsStageRemarkLocked(extra));
    }

    [Fact]
    public void ApplyPublishState_HiddenListedImage_ShowsHiddenOverLedger()
    {
        var shot = Stage("心得（notes）/20261005_021341/shot.jpg", "published");
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = "20261005_021341",
                    Slug = "n-20261005-021341",
                    Hidden = true,
                    Images =
                    {
                        new NoteImageMeta { StageRel = "心得（notes）/20261005_021341/shot.jpg" }
                    }
                }
            }
        };

        NoteCatalog.ApplyPublishState(new[] { shot }, index, "心得（notes）");

        Assert.Equal("已隐藏", PublishStatus.ForStage(shot));
    }

    [Fact]
    public void ApplyPublishState_Draft_StaysUnpublished()
    {
        var body = Stage("心得（notes）/20261005_021341/正文.md");
        var index = new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry { Folder = "20261005_021341", Slug = "n-20261005-021341", Draft = true }
            }
        };

        NoteCatalog.ApplyPublishState(new[] { body }, index, "心得（notes）");

        Assert.Equal("未上页", PublishStatus.ForStage(body));
    }

    private static StageItem Stage(string stageRel, string? ledgerStatus = null)
    {
        return new StageItem
        {
            ChannelKey = "notes",
            StageRel = stageRel,
            FullPath = "x",
            LedgerStatus = ledgerStatus
        };
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-note-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteNote(string folder, string slug, string markdown)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "正文.md"), markdown);
        NoteConfigStore.Save(folder, new NoteConfig
        {
            CreatedAt = "2026-10-05 12:56:03",
            Slug = slug,
            Title = "一篇"
        });
    }

    private static string Front(bool draft, string slug, string body)
    {
        return "---\n"
            + "title: \"一篇\"\n"
            + "date: 2026-10-05\n"
            + "summary: \"摘要\"\n"
            + "slug: \"" + slug + "\"\n"
            + "draft: " + (draft ? "true" : "false") + "\n"
            + "---\n\n"
            + body + "\n";
    }

    private static readonly byte[] TinyPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE,
        0x02, 0xFE, 0xDC, 0xCC, 0x59, 0xE7, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42,
        0x60, 0x82
    };
}
