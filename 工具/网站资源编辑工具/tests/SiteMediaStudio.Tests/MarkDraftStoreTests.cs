using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class MarkDraftStoreTests
{
    [Fact]
    public void WriteThenRead_KeepsIntentAndWork()
    {
        WithTempMarks((path, profile) =>
        {
            var key = MarkDraftStore.StageKey(@"landscape-design\xiaowayao\01.jpg");
            MarkDraftStore.Commit(
                profile,
                key,
                MediaIntent.StageIngest,
                "landscape-design",
                "xiaowayao",
                path);

            var file = MarkDraftStore.Load(path);
            var entry = MarkDraftStore.Find(file, profile, key);
            Assert.NotNull(entry);
            Assert.Equal(MediaIntentCodes.StageIngest, entry!.Intent);
            Assert.Equal("landscape-design", entry.Channel);
            Assert.Equal("xiaowayao", entry.WorkId);
            Assert.False(string.IsNullOrWhiteSpace(entry.UpdatedAt));
        });
    }

    [Fact]
    public void Load_MissingOrCorrupt_ReturnsEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sms-mark-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var empty = MarkDraftStore.Load(missing);
        Assert.Empty(empty.Entries);
        Assert.Equal("", empty.ProfilePath);

        WithTempMarks((path, _) =>
        {
            File.WriteAllText(path, "{not-json");
            var damaged = MarkDraftStore.Load(path);
            Assert.Empty(damaged.Entries);
            Assert.Equal("", damaged.ProfilePath);
        });
    }

    [Fact]
    public void Find_OtherProfile_DoesNotShareMarks()
    {
        WithTempMarks((path, profileA) =>
        {
            var key = MarkDraftStore.SiteKey("landscape-design/xiaowayao/01.webp");
            MarkDraftStore.Commit(profileA, key, MediaIntent.SiteHide, "landscape-design", "xiaowayao", path);

            var profileB = new WorkspaceProfile
            {
                ProfilePath = Path.Combine(Path.GetTempPath(), "other-profile.json"),
                ResolvedRoot = Path.Combine(Path.GetTempPath(), "other-root")
            };
            var file = MarkDraftStore.Load(path);
            Assert.NotNull(MarkDraftStore.Find(file, profileA, key));
            Assert.Null(MarkDraftStore.Find(file, profileB, key));

            MarkDraftStore.Commit(profileB, key, MediaIntent.SiteWithdraw, "landscape-design", "other", path);
            var afterSwitch = MarkDraftStore.Load(path);
            Assert.Null(MarkDraftStore.Find(afterSwitch, profileA, key));
            Assert.Equal(MediaIntentCodes.SiteWithdraw, MarkDraftStore.Find(afterSwitch, profileB, key)?.Intent);
        });
    }

    [Fact]
    public void CommitNone_RemovesEntry()
    {
        WithTempMarks((path, profile) =>
        {
            var key = MarkDraftStore.StageKey("demo/a.png");
            MarkDraftStore.Commit(profile, key, MediaIntent.StageRecycle, "demo", "demo-park", path);
            MarkDraftStore.Commit(profile, key, MediaIntent.None, "demo", "demo-park", path);
            Assert.False(File.Exists(path));
        });
    }

    [Fact]
    public void VisibleParts_OmitsZerosAndKeepsOrder()
    {
        var full = MarkDraftStore.VisibleParts(new MarkCountTally(5, 3, 1, 2));
        Assert.Equal(4, full.Count);
        Assert.Equal((MediaIntent.StageIngest, 5), full[0]);
        Assert.Equal((MediaIntent.SiteHide, 3), full[1]);
        Assert.Equal((MediaIntent.SiteWithdraw, 1), full[2]);
        Assert.Equal((MediaIntent.StageRecycle, 2), full[3]);

        var partial = MarkDraftStore.VisibleParts(new MarkCountTally(0, 3, 1, 2));
        Assert.Equal(3, partial.Count);
        Assert.Equal((MediaIntent.SiteHide, 3), partial[0]);
        Assert.Equal((MediaIntent.SiteWithdraw, 1), partial[1]);
        Assert.Equal((MediaIntent.StageRecycle, 2), partial[2]);

        Assert.Empty(MarkDraftStore.VisibleParts(default));
    }

    [Fact]
    public void Tally_CountsRestoreAsIngest()
    {
        var tally = MarkDraftStore.Tally(new[]
        {
            MediaIntent.StageIngest,
            MediaIntent.SiteRestore,
            MediaIntent.SiteHide,
            MediaIntent.SiteWithdraw,
            MediaIntent.StageRecycle,
            MediaIntent.None
        });
        Assert.Equal(2, tally.Ingest);
        Assert.Equal(1, tally.Hide);
        Assert.Equal(1, tally.Withdraw);
        Assert.Equal(1, tally.Recycle);
    }

    [Fact]
    public void TallyForNav_WorkSeesOwnMarks_ChannelSeesAll()
    {
        WithTempMarks((path, profile) =>
        {
            MarkDraftStore.CommitMany(
                profile,
                new[]
                {
                    new MarkDraftWrite(MarkDraftStore.StageKey("a/1.jpg"), MediaIntent.StageIngest, "landscape-design", "xiaowayao"),
                    new MarkDraftWrite(MarkDraftStore.StageKey("a/2.jpg"), MediaIntent.StageIngest, "landscape-design", "xiaowayao"),
                    new MarkDraftWrite(MarkDraftStore.SiteKey("a/3.webp"), MediaIntent.SiteHide, "landscape-design", "xiaowayao"),
                    new MarkDraftWrite(MarkDraftStore.StageKey("b/1.jpg"), MediaIntent.StageRecycle, "landscape-design", "other-work"),
                    new MarkDraftWrite(MarkDraftStore.StageKey("c/1.jpg"), MediaIntent.SiteWithdraw, "demo-photo", "album")
                },
                path);

            var file = MarkDraftStore.Load(path);
            var work = MarkDraftStore.TallyForNav(file, profile, "landscape-design", "xiaowayao", null);
            Assert.Equal(new MarkCountTally(2, 1, 0, 0), work);

            var channel = MarkDraftStore.TallyForNav(file, profile, "landscape-design", null, null);
            Assert.Equal(new MarkCountTally(2, 1, 0, 1), channel);

            var group = MarkDraftStore.TallyForNav(
                file,
                profile,
                "landscape-design",
                null,
                new HashSet<string>(StringComparer.Ordinal) { "other-work" });
            Assert.Equal(new MarkCountTally(0, 0, 0, 1), group);
        });
    }

    [Fact]
    public void KeyOf_IntentItem_UsesSiteOrStage()
    {
        Assert.Equal(
            "site:landscape-design/xiaowayao/01.webp",
            MarkDraftStore.KeyOf(new IntentItem
            {
                Intent = MediaIntentCodes.SiteHide,
                Object = @"landscape-design\xiaowayao\01.webp"
            }));
        Assert.Equal(
            "stage:landscape-design/xiaowayao/01.jpg",
            MarkDraftStore.KeyOf(new IntentItem
            {
                Intent = MediaIntentCodes.StageIngest,
                StageRel = @"landscape-design\xiaowayao\01.jpg"
            }));
        Assert.Null(MarkDraftStore.KeyOf(new IntentItem { Intent = MediaIntentCodes.CopyUpdate }));
    }

    [Fact]
    public void KeyOf_IntentItem_PrefersSourceStageRelAfterPrepare()
    {
        Assert.Equal(
            "stage:humanist-photo/album/01_37.jpg",
            MarkDraftStore.KeyOf(new IntentItem
            {
                Intent = MediaIntentCodes.StageIngest,
                StageRel = @"humanist-photo\album\01_37.site-ready.webp",
                SourceStageRel = @"humanist-photo\album\01_37.jpg"
            }));
    }

    [Fact]
    public void KeysOf_SkipsCopyAndDedups()
    {
        var keys = MarkDraftStore.KeysOf(new IntentDocument
        {
            Items =
            [
                new IntentItem
                {
                    Intent = MediaIntentCodes.StageIngest,
                    StageRel = @"a\1.jpg"
                },
                new IntentItem
                {
                    Intent = MediaIntentCodes.SiteHide,
                    Object = "a/2.webp"
                },
                new IntentItem { Intent = MediaIntentCodes.CopyUpdate }
            ]
        });
        Assert.Equal(new[] { "stage:a/1.jpg", "site:a/2.webp" }, keys);
    }

    [Fact]
    public void Keys_MatchGridCardKeys()
    {
        Assert.Equal("stage:landscape-design/xiaowayao/01.jpg", MarkDraftStore.StageKey(@"landscape-design\xiaowayao\01.jpg"));
        Assert.Equal("site:landscape-design/xiaowayao/01.webp", MarkDraftStore.SiteKey(@"landscape-design\xiaowayao\01.webp"));
        Assert.Equal(
            "stage:a/b.jpg",
            MarkDraftStore.KeyOf(new StageItem
            {
                StageRel = @"a\b.jpg",
                FullPath = "C:/a/b.jpg",
                ChannelKey = "a"
            }, null));
    }

    private static void WithTempMarks(Action<string, WorkspaceProfile> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-mark-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "marks.json");
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
