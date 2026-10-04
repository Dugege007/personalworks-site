using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 标签组件的候选、提交与删除预设文案。
/// </summary>
public sealed class TagEditorCatalogTests
{
    [Fact]
    public void Suggestions_PinTypes_ThenFreeTagsByUseCount()
    {
        var works = new[]
        {
            Work("甲", new[] { "夜景", "夜景" }, new[] { "landscape-photo" }, new[] { "展陈" }),
            Work("乙", Array.Empty<string>(), new[] { "landscape-photo", "humanist-photo" }, new[] { "夜景" })
        };
        var list = TagEditorCatalog.BuildSuggestions(works, new[] { "街拍", "2024", "landscape-photo" }, includeTypes: true);
        Assert.Equal(
            PhotoFactRules.PrimaryFacetList.Where(item => item.IsType).Select(item => item.Id),
            list.Where(item => item.Kind == TagEditorKind.Type).Select(item => item.Id));
        Assert.DoesNotContain(list, item => item.Kind == TagEditorKind.Type && item.AllowPresetDelete);
        Assert.Equal(
            new[] { "风光", "展馆", "人像", "人文", "随拍", "游戏", "AI", "夜景", "展陈", "街拍" },
            list.Select(item => item.Label));
        var pinned = list.Where(item => item.Label is "展馆" or "随拍").ToList();
        Assert.All(pinned, item => Assert.False(item.AllowPresetDelete));
        var free = list.Where(item => item.Kind == TagEditorKind.Free && item.AllowPresetDelete).ToList();
        Assert.Equal(new[] { "夜景", "展陈", "街拍" }, free.Select(item => item.Label));
        Assert.Equal(2, free[0].UseCount);
        Assert.DoesNotContain(list, item => item.Label == "2024");
    }

    [Fact]
    public void Filter_MatchesContainedText()
    {
        var source = TagEditorCatalog.BuildSuggestions(Array.Empty<WorkCatalogItem>(), new[] { "夜景", "展陈" }, includeTypes: true);
        var byScene = TagEditorCatalog.Filter(source, "景");
        Assert.Equal(new[] { "夜景" }, byScene.Where(item => item.Kind == TagEditorKind.Free).Select(item => item.Label));
        var byType = TagEditorCatalog.Filter(source, "风");
        Assert.Contains(byType, item => item.Id == "landscape-photo");
        Assert.DoesNotContain(byType, item => item.Label == "夜景");
        var byLetter = TagEditorCatalog.Filter(source, "a");
        Assert.Contains(byLetter, item => item.Id == "ai-photo");
        Assert.DoesNotContain(byLetter, item => item.Id is "portrait-photo" or "humanist-photo" or "game-photo" or "landscape-photo");
    }

    [Fact]
    public void Commit_RejectsYear_AcceptsExactTypeAndNewWord()
    {
        var source = TagEditorCatalog.BuildSuggestions(Array.Empty<WorkCatalogItem>(), new[] { "夜景" }, includeTypes: true);
        Assert.False(TagEditorCatalog.TryCommitText("  ", source, out _, out _, out var emptyError));
        Assert.Null(emptyError);
        Assert.False(TagEditorCatalog.TryCommitText("2024", source, out _, out _, out var yearError));
        Assert.Contains("年份不是标签", yearError, StringComparison.Ordinal);
        Assert.True(TagEditorCatalog.TryCommitText("风光", source, out var typeHit, out var typeNew, out _));
        Assert.Equal("landscape-photo", typeHit!.Id);
        Assert.Null(typeNew);
        Assert.True(TagEditorCatalog.TryCommitText("街拍", source, out var freeHit, out var freeNew, out _));
        Assert.Null(freeHit);
        Assert.Equal("街拍", freeNew);
    }

    [Fact]
    public void Chips_ShowOnlySharedTags_AddKeepsTheOtherFrame()
    {
        var suggestions = TagEditorCatalog.BuildSuggestions(Array.Empty<WorkCatalogItem>(), new[] { "夜景", "展陈" }, includeTypes: true);
        var chips = TagEditorCatalog.ChipsForFrames(suggestions, new[]
        {
            (Themes: (IReadOnlyList<string>)new[] { "landscape-photo" }, Tags: (IReadOnlyList<string>)new[] { "夜景", "展陈" }),
            (Themes: (IReadOnlyList<string>)new[] { "landscape-photo", "humanist-photo" }, Tags: (IReadOnlyList<string>)new[] { "夜景" })
        });
        Assert.Equal(new[] { "风光", "夜景" }, chips.Select(item => item.Label));
        var added = TagEditorCatalog.WithAdded(new[] { "humanist-photo" }, new[] { "夜景" }, suggestions.First(item => item.Id == "landscape-photo"));
        Assert.Equal(new[] { "landscape-photo", "humanist-photo" }, added.Themes);
        Assert.Equal(new[] { "夜景" }, added.Tags);
    }

    [Fact]
    public void PresetDelete_ListsWorks_AndStripsOnlyThatTag()
    {
        var works = new[]
        {
            Work("静安寺", new[] { "夜景" }, new[] { "landscape-photo" }, new[] { "夜景", "展陈" }),
            Work("空", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
        };
        var hits = TagEditorCatalog.FindFreeTagUsages(works, "夜景");
        Assert.Single(hits);
        Assert.Contains("静安寺", TagEditorCatalog.DescribePresetDelete("夜景", hits), StringComparison.Ordinal);
        Assert.Contains("没有作品在使用", TagEditorCatalog.DescribePresetDelete("街拍", Array.Empty<TagUsageHit>()), StringComparison.Ordinal);
        var writes = TagEditorCatalog.StripFreeTag(works, "夜景");
        Assert.Contains(writes, item => item.ObjectKey == null && item.Tags.Count == 0);
        var frame = Assert.Single(writes, item => item.ObjectKey != null);
        Assert.Equal(new[] { "展陈" }, frame.Tags);
        Assert.Equal(new[] { "landscape-photo" }, frame.Themes);
    }

    [Fact]
    public void Vocab_RemembersPerChannel_AndForgets()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-vocab-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            TagVocabStore.Remember(@"D:\works", "real-world-photo", "夜景", path);
            TagVocabStore.Remember(@"D:\works", "real-world-photo", "2024", path);
            TagVocabStore.Remember(@"D:\works", "demo-render", "日式禅意", path);
            Assert.Equal(new[] { "夜景" }, TagVocabStore.Read(@"D:\works", "real-world-photo", path));
            Assert.Equal(new[] { "日式禅意" }, TagVocabStore.Read(@"D:\works", "demo-render", path));
            TagVocabStore.Forget(@"D:\works", "real-world-photo", "夜景", path);
            Assert.Empty(TagVocabStore.Read(@"D:\works", "real-world-photo", path));
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
    public void SummarizeWork_CountsResourcesInThisWork()
    {
        var work = new WorkCatalogItem
        {
            Id = "甲",
            Channel = "real-world-photo",
            Title = "甲",
            SourceKind = "works",
            Tags = new[] { "作品级" },
            Media = new[]
            {
                new WorkMediaItem
                {
                    Src = "photo/real-world-photo/甲/01.webp",
                    Themes = new[] { "landscape-photo" },
                    Tags = new[] { "夜色", "夜色" }
                },
                new WorkMediaItem
                {
                    Src = "photo/real-world-photo/甲/02.webp",
                    Themes = new[] { "landscape-photo", "humanist-photo" },
                    Tags = new[] { "夜色", "展陈" }
                },
                new WorkMediaItem
                {
                    Src = "",
                    Themes = new[] { "portrait-photo" },
                    Tags = new[] { "街拍" }
                }
            }
        };

        var chips = TagEditorCatalog.SummarizeWork(work);
        Assert.Equal(new[] { "风光", "人文", "夜色", "展陈" }, chips.Select(item => item.Label));
        Assert.Equal(new[] { 2, 1, 2, 1 }, chips.Select(item => item.UseCount));
        Assert.DoesNotContain(chips, item => item.Id == PhotoFactRules.UntaggedId);
        var night = chips.Single(item => item.Label == "夜色");
        Assert.Equal(2, TagEditorCatalog.ObjectKeysWithTag(work, night).Count);
        var exhibit = chips.Single(item => item.Label == "展陈");
        Assert.Equal("photo/real-world-photo/甲/02.webp", Assert.Single(TagEditorCatalog.ObjectKeysWithTag(work, exhibit)));
    }

    [Fact]
    public void UntaggedChip_SelectsFramesWithoutThemeOrTag()
    {
        var work = new WorkCatalogItem
        {
            Id = "乙",
            Channel = "real-world-photo",
            Title = "乙",
            SourceKind = "works",
            Media = new[]
            {
                new WorkMediaItem
                {
                    Src = "photo/real-world-photo/乙/01.webp",
                    Themes = Array.Empty<string>(),
                    Tags = Array.Empty<string>()
                },
                new WorkMediaItem
                {
                    Src = "photo/real-world-photo/乙/02.webp",
                    Themes = new[] { "landscape-photo" },
                    Tags = Array.Empty<string>()
                }
            }
        };

        var chip = TagEditorCatalog.SummarizeWork(work).Single(item => item.Id == PhotoFactRules.UntaggedId);
        Assert.Equal(1, chip.UseCount);
        Assert.Equal("photo/real-world-photo/乙/01.webp", Assert.Single(TagEditorCatalog.ObjectKeysWithTag(work, chip)));
    }

    /// <summary>
    /// 一条带作品级标签和一张帧的作品。
    /// </summary>
    private static WorkCatalogItem Work(string title, IReadOnlyList<string> workTags, IReadOnlyList<string> themes, IReadOnlyList<string> tags)
    {
        return new WorkCatalogItem
        {
            Id = title,
            Channel = "real-world-photo",
            Title = title,
            SourceKind = "works",
            Tags = workTags,
            Media = new[]
            {
                new WorkMediaItem
                {
                    Src = "photo/real-world-photo/" + title + "/01.webp",
                    Themes = themes,
                    Tags = tags
                }
            }
        };
    }
}
