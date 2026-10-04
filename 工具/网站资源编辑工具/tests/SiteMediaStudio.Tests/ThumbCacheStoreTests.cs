using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ThumbCacheStoreTests
{
    [Fact]
    public void FileNameFor_SameSourceAndSize_IsStable()
    {
        var writeUtc = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var first = ThumbCacheStore.FileNameFor(@"D:\stage\a.jpg", 240, writeUtc);
        var second = ThumbCacheStore.FileNameFor(@"D:/stage/a.jpg", 240, writeUtc);
        Assert.Equal(first, second);
        Assert.EndsWith(".jpg", first);
    }

    [Fact]
    public void FileNameFor_DifferentDecodePx_Differs()
    {
        var writeUtc = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var small = ThumbCacheStore.FileNameFor(@"D:\stage\a.jpg", 120, writeUtc);
        var large = ThumbCacheStore.FileNameFor(@"D:\stage\a.jpg", 240, writeUtc);
        Assert.NotEqual(small, large);
    }

    [Fact]
    public void MeasureAndClear_OnlyTouchGivenDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-thumbs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "a.jpg"), new byte[1500]);
            File.WriteAllBytes(Path.Combine(root, "b.jpg"), new byte[500]);
            Assert.Equal(2000, ThumbCacheStore.MeasureBytes(root));
            ThumbCacheStore.Clear(root);
            Assert.Equal(0, ThumbCacheStore.MeasureBytes(root));
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Clear_DoesNotDeleteCopyDraftsFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-thumbs-draft-" + Guid.NewGuid().ToString("N"));
        var thumbs = Path.Combine(root, "thumbs");
        var draftPath = Path.Combine(root, "copy-drafts.json");
        Directory.CreateDirectory(thumbs);
        try
        {
            File.WriteAllBytes(Path.Combine(thumbs, "a.jpg"), new byte[200]);
            File.WriteAllText(draftPath, "{\"version\":1,\"entries\":[]}");
            var starPath = Path.Combine(root, "star-drafts.json");
            File.WriteAllText(starPath, "{\"version\":1,\"entries\":[]}");
            ThumbCacheStore.Clear(thumbs);
            Assert.Equal(0, ThumbCacheStore.MeasureBytes(thumbs));
            Assert.True(File.Exists(draftPath));
            Assert.True(File.Exists(starPath));
            Assert.Contains("\"version\":1", File.ReadAllText(draftPath), StringComparison.Ordinal);
            Assert.Contains("\"version\":1", File.ReadAllText(starPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
