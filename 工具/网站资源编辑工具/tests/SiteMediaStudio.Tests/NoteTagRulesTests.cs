using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 心得标签与摄影分库：建议列表、词库栏目和正文索引。
/// </summary>
public sealed class NoteTagRulesTests
{
    [Fact]
    public void Suggestions_OmitPhotoDefaultsThemesAndImageTags()
    {
        var notes = new[]
        {
            new WorkCatalogItem
            {
                Id = "甲",
                Channel = NoteRules.ChannelKey,
                Title = "甲",
                SourceKind = "note",
                NoteBodyInIndex = true,
                Tags = new[] { "算法", "算法" },
                Media = new[]
                {
                    new WorkMediaItem
                    {
                        Src = "notes/甲/01.webp",
                        Themes = new[] { "landscape-photo" },
                        Tags = new[] { "展馆", "随拍" }
                    }
                }
            },
            new WorkCatalogItem
            {
                Id = "乙",
                Channel = NoteRules.ChannelKey,
                Title = "乙",
                SourceKind = "note",
                NoteBodyInIndex = true,
                Tags = new[] { "算法", "统计" }
            }
        };
        var vocab = new[]
        {
            "展馆",
            "随拍",
            "landscape-photo",
            "风光",
            "人文",
            "人像",
            "游戏",
            "AI",
            "humanist-photo",
            "无标签",
            "untagged",
            "2024",
            "算法",
            "时序"
        };

        var list = NoteTagRules.BuildSuggestions(notes, vocab);

        Assert.Empty(NoteTagRules.DefaultTagList);
        Assert.DoesNotContain(list, item => item.Kind == TagEditorKind.Type);
        Assert.Equal(new[] { "算法", "统计", "时序" }, list.Select(item => item.Label));
        Assert.Equal(2, list[0].UseCount);
        Assert.All(list, item => Assert.True(item.AllowPresetDelete));
        foreach (var banned in new[] { "展馆", "随拍", "风光", "人文", "人像", "游戏", "AI", "landscape-photo", "humanist-photo", "无标签", "untagged", "2024" })
        {
            Assert.DoesNotContain(list, item => item.Id == banned || item.Label == banned);
        }

        var photo = TagEditorCatalog.BuildSuggestions(Array.Empty<WorkCatalogItem>(), new[] { "展馆" }, includeTypes: false);
        Assert.Contains(photo, item => item.Label == "展馆");
    }

    [Fact]
    public void Normalize_RejectsYearThemeKeyAndUntaggedLiteral()
    {
        Assert.True(NoteTagRules.TryNormalizeTags(new[] { " 算法 ", "算法", "统计" }, out var tags, out var okError));
        Assert.Null(okError);
        Assert.Equal(new[] { "算法", "统计" }, tags);

        Assert.False(NoteTagRules.TryNormalizeTags(new[] { "2024" }, out _, out var yearError));
        Assert.Contains("年份不是标签", yearError, StringComparison.Ordinal);
        Assert.False(NoteTagRules.TryNormalizeTags(new[] { "landscape-photo" }, out _, out var themeError));
        Assert.Contains("类型不进自由标签", themeError, StringComparison.Ordinal);
        Assert.False(NoteTagRules.TryNormalizeTags(new[] { "无标签" }, out _, out var literalError));
        Assert.Contains("无标签是筛选", literalError, StringComparison.Ordinal);
        Assert.False(NoteTagRules.TryNormalizeTags(new[] { PhotoFactRules.UntaggedId }, out _, out var idError));
        Assert.Contains("无标签是筛选", idError, StringComparison.Ordinal);
        Assert.False(NoteTagRules.CanRemember("展馆"));
        Assert.False(NoteTagRules.CanRemember("随拍"));
        Assert.True(NoteTagRules.CanRemember("算法"));
    }

    [Fact]
    public void Vocab_RememberUsesNotesChannel_AndDoesNotLeak()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-note-vocab-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            TagVocabStore.Remember(@"D:\works", "landscape-photo", "展馆", path);
            TagVocabStore.Remember(@"D:\works", "landscape-photo", "随拍", path);
            TagVocabStore.Remember(@"D:\works", NoteRules.ChannelKey, "算法", path);
            TagVocabStore.Remember(@"D:\works", NoteRules.ChannelKey, "展馆", path);
            TagVocabStore.Remember(@"D:\works", NoteRules.ChannelKey, "landscape-photo", path);
            TagVocabStore.Remember(@"D:\works", NoteRules.ChannelKey, "2024", path);
            TagVocabStore.Remember(@"D:\works", NoteRules.ChannelKey, "无标签", path);
            TagVocabStore.Remember(@"D:\works", NoteRules.ChannelKey, PhotoFactRules.UntaggedId, path);

            var notes = TagVocabStore.Read(@"D:\works", NoteRules.ChannelKey, path);
            var photo = TagVocabStore.Read(@"D:\works", "landscape-photo", path);
            Assert.Equal(new[] { "算法" }, notes);
            Assert.Equal(new[] { "展馆", "随拍" }, photo);
            Assert.DoesNotContain(notes, tag => tag is "展馆" or "随拍");
            Assert.DoesNotContain(photo, tag => tag == "算法");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ApplyMeta_WritesBodyTags_AndLeavesImagesAndConfig()
    {
        var root = TempRoot();
        var profile = Profile(root);
        var folderName = "20261007_015206";
        var noteDir = NoteDir(profile, folderName);
        Directory.CreateDirectory(noteDir);
        var configPath = Path.Combine(noteDir, NoteConfigStore.FileName);
        File.WriteAllText(configPath, """
            {
              "createdAt": "2026-10-07 01:52:06",
              "title": "控制图",
              "slug": "n-20261007-015206"
            }
            """);
        var configBefore = File.ReadAllBytes(configPath);
        var index = new NoteIndexFile
        {
            Version = 1,
            Notes =
            {
                new NoteEntry
                {
                    Folder = folderName,
                    Slug = "n-20261007-015206",
                    Title = "控制图",
                    Body = new NoteResourceMeta(),
                    Images =
                    {
                        new NoteImageMeta
                        {
                            ObjectKey = "notes/n-20261007-015206/01.webp",
                            StageRel = "心得（notes）/" + folderName + "/01.webp",
                            Tags = new List<string> { "旧配图" }
                        }
                    }
                }
            }
        };
        NoteIndexStore.Save(profile, index);

        var document = new IntentDocument
        {
            Options = new IntentOptions { Deploy = "none" },
            Items =
            {
                new IntentItem
                {
                    Intent = MediaIntentCodes.TagsUpdate,
                    Channel = NoteRules.ChannelKey,
                    WorkId = folderName,
                    StageRel = NoteTagRules.BodyStageRel("notes/" + folderName, folderName),
                    Tags = new List<string> { " 算法 ", "算法", "统计" }
                }
            }
        };

        Assert.True(NoteCommands.PullMeta(profile, document));
        Assert.Empty(document.Items);

        var saved = NoteIndexStore.Load(profile);
        var entry = Assert.Single(saved.Notes);
        Assert.Equal(new[] { "算法", "统计" }, entry.Body.Tags);
        Assert.Equal(new[] { "旧配图" }, Assert.Single(entry.Images).Tags);
        Assert.Equal(configBefore, File.ReadAllBytes(configPath));

        var rejected = new IntentDocument
        {
            Items =
            {
                new IntentItem
                {
                    Intent = MediaIntentCodes.TagsUpdate,
                    Channel = NoteRules.ChannelKey,
                    WorkId = folderName,
                    StageRel = "notes/" + folderName + "/正文.md",
                    Tags = new List<string> { "landscape-photo" }
                }
            }
        };
        var gate = IntentGate.EvaluateTags(rejected.Items[0], new IntentContext
        {
            LedgerDict = new Dictionary<string, LedgerRecord>(),
            ContentRefCountDict = new Dictionary<string, int>()
        });
        Assert.False(gate.Allowed);
        Assert.Contains("类型不进自由标签", gate.Message, StringComparison.Ordinal);
        Assert.True(NoteCommands.PullMeta(profile, rejected));
        Assert.Equal(new[] { "算法", "统计" }, NoteIndexStore.Load(profile).Notes[0].Body.Tags);
    }

    [Fact]
    public void Discover_CopiesBodyTags_OnlyWhenIndexed()
    {
        var root = TempRoot();
        var profile = Profile(root);
        var channel = profile.Channels[0];
        var published = "20261007_015206";
        var draftFolder = "20261008_010101";
        WriteFolder(profile, channel, published);
        WriteFolder(profile, channel, draftFolder);
        NoteIndexStore.Save(profile, new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = published,
                    Slug = "n-20261007-015206",
                    Title = "控制图",
                    Draft = false,
                    Body = new NoteResourceMeta { Tags = new List<string> { "算法", "时序" } },
                    Images =
                    {
                        new NoteImageMeta
                        {
                            ObjectKey = "notes/n-20261007-015206/01.webp",
                            Tags = new List<string> { "配图词" }
                        }
                    }
                },
                new NoteEntry
                {
                    Folder = draftFolder,
                    Slug = "n-draft",
                    Draft = true,
                    Body = new NoteResourceMeta { Tags = new List<string> { "草稿词" } }
                }
            }
        });

        var found = NoteCatalog.Discover(profile, Array.Empty<StageItem>());
        var listed = Assert.Single(found, item => item.Id == published);
        Assert.True(listed.NoteBodyInIndex);
        Assert.Equal(new[] { "算法", "时序" }, listed.Tags);
        Assert.Equal(new[] { "配图词" }, Assert.Single(listed.Media).Tags);
        var draft = Assert.Single(found, item => item.Id == draftFolder);
        Assert.False(draft.NoteBodyInIndex);
    }

    [Fact]
    public void BuildNoteBodyTags_TargetsBody_AndRunQueuesPublish()
    {
        var root = TempRoot();
        var profile = Profile(root);
        var folderName = "20261007_015206";
        WriteFolder(profile, profile.Channels[0], folderName);
        NoteIndexStore.Save(profile, new NoteIndexFile
        {
            Notes =
            {
                new NoteEntry
                {
                    Folder = folderName,
                    Slug = "n-20261007-015206",
                    Title = "控制图",
                    Body = new NoteResourceMeta { Tags = new List<string> { "算法" } },
                    Images =
                    {
                        new NoteImageMeta
                        {
                            ObjectKey = "notes/n-20261007-015206/01.webp",
                            Tags = new List<string> { "配图词" }
                        }
                    }
                }
            }
        });
        var session = new WorkspaceSession
        {
            Profile = profile,
            Works = NoteCatalog.Discover(profile, Array.Empty<StageItem>())
        };
        var document = IntentDocumentBuilder.BuildNoteBodyTags(
            session,
            ExecutionMode.Direct,
            new[]
            {
                new TagFrameWrite
                {
                    WorkId = folderName,
                    ObjectKey = "notes/n-20261007-015206/01.webp",
                    Tags = new List<string> { "不该写配图" }
                },
                new TagFrameWrite
                {
                    WorkId = folderName,
                    Tags = new List<string> { "算法", "统计" }
                }
            });
        var item = Assert.Single(document.Items);
        Assert.Equal(NoteRules.ChannelKey, item.Channel);
        Assert.Equal(MediaIntentCodes.TagsUpdate, item.Intent);
        Assert.Null(item.ObjectList);
        Assert.EndsWith(NoteRules.BodyFileName, item.StageRel, StringComparison.Ordinal);
        Assert.Equal(new[] { "算法", "统计" }, item.Tags);

        var intentPath = Path.Combine(root, "intent-tags.json");
        File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document));
        var result = BatchExecutor.Run(session, document, intentPath);

        Assert.True(result.Ok, result.FailureText);
        Assert.NotNull(result.Pending);
        Assert.True(result.Pending!.CopyOnly);
        Assert.Equal("spa", result.Pending.Deploy);
        var saved = NoteIndexStore.Load(profile).Notes[0];
        Assert.Equal(new[] { "算法", "统计" }, saved.Body.Tags);
        Assert.Equal(new[] { "配图词" }, Assert.Single(saved.Images).Tags);
    }

    /// <summary>
    /// 临时工作区根。
    /// </summary>
    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-note-tags-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// 带心得栏目和站点索引路径的配置。
    /// </summary>
    private static WorkspaceProfile Profile(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "PersonalSite", "src", "content"));
        return new WorkspaceProfile
        {
            Name = "心得标签",
            ProfilePath = Path.Combine(root, "profile.json"),
            ResolvedRoot = root,
            StageRoot = "stage",
            StageFolderPattern = "ZhKey",
            SiteCatalog = new SiteCatalogConfig { SitePath = "PersonalSite/src/content/site.ts" },
            Channels = new List<ChannelProfile>
            {
                new()
                {
                    Key = NoteRules.ChannelKey,
                    Zh = "心得",
                    Capabilities = new ChannelCapabilities { Catalog = true, Ingest = true }
                }
            }
        };
    }

    /// <summary>
    /// 这篇心得的夹路径。
    /// </summary>
    private static string NoteDir(WorkspaceProfile profile, string folderName)
    {
        var channel = profile.Channels[0];
        return Path.Combine(
            WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot),
            WorkspaceProfileLoader.ChannelStageRelative(profile, channel).Replace('/', Path.DirectorySeparatorChar),
            folderName);
    }

    /// <summary>
    /// 写一篇最小心得夹。
    /// </summary>
    private static void WriteFolder(WorkspaceProfile profile, ChannelProfile channel, string folderName)
    {
        _ = channel;
        var dir = NoteDir(profile, folderName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, NoteRules.BodyFileName), "# 一篇\n");
    }
}
