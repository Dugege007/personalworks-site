using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class VideoCoverStoreTests
{
    [Fact]
    public void KeyFor_SameIdentity_IsStable()
    {
        var writeUtc = new DateTime(2026, 9, 17, 13, 28, 0, DateTimeKind.Utc);
        var first = VideoCoverStore.KeyFor(@"数字孪生（digital-twin）/demo/a.mp4", 100, writeUtc);
        var second = VideoCoverStore.KeyFor("数字孪生（digital-twin）/demo/a.mp4", 100, writeUtc);
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void KeyFor_DifferentMtime_Differs()
    {
        var a = new DateTime(2026, 9, 17, 13, 28, 0, DateTimeKind.Utc);
        var b = a.AddSeconds(1);
        Assert.NotEqual(
            VideoCoverStore.KeyFor("demo/a.mp4", 100, a),
            VideoCoverStore.KeyFor("demo/a.mp4", 100, b));
    }

    [Fact]
    public void WriteThenRead_KeepsPositionAndImage()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-covers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var writeUtc = new DateTime(2026, 9, 17, 13, 28, 0, DateTimeKind.Utc);
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            VideoCoverStore.Write("demo-render/demo-park/clip.mp4", 32, writeUtc, 4.5, 320, 180, bytes, false, root);
            var hit = VideoCoverStore.TryRead("demo-render/demo-park/clip.mp4", 32, writeUtc, root);
            Assert.NotNull(hit);
            Assert.Equal(4.5, hit!.Record.PositionSec);
            Assert.Equal(320, hit.Record.Width);
            Assert.True(File.Exists(hit.ImagePath));
            Assert.Equal(bytes, File.ReadAllBytes(hit.ImagePath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TryRead_MissingCache_IsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "sms-covers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Null(VideoCoverStore.TryRead("missing.mp4", 1, DateTime.UtcNow, root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
