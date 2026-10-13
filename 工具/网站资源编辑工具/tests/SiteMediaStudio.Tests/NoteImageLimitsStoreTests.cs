using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteImageLimitsStoreTests
{
    [Fact]
    public void Load_MissingFileOrMissingFields_UsesDefaults()
    {
        var dir = TempDir();
        var missing = Path.Combine(dir, NoteImageLimitsStore.FileName);
        var defaults = NoteImageLimitsStore.LoadFile(missing);
        Assert.Equal(NoteImageThreshold.DefaultImageMaxBytes, defaults.ImageMaxBytes);
        Assert.Equal(NoteImageThreshold.DefaultGifMaxBytes, defaults.GifMaxBytes);

        var partial = Path.Combine(dir, "partial.json");
        File.WriteAllText(partial, "{\n  \"version\": 1\n}\n");
        var loaded = NoteImageLimitsStore.LoadFile(partial);
        Assert.Equal(NoteImageThreshold.DefaultImageMaxBytes, loaded.ImageMaxBytes);
        Assert.Equal(NoteImageThreshold.DefaultGifMaxBytes, loaded.GifMaxBytes);

        var profile = new WorkspaceProfile { ProfilePath = Path.Combine(dir, "personalworks.json") };
        Assert.Equal(missing, NoteImageLimitsStore.FilePath(profile));
        Assert.Equal(NoteImageThreshold.DefaultImageMaxBytes, NoteImageLimitsStore.Load(profile).ImageMaxBytes);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("<<<<<<< HEAD")]
    [InlineData("[]")]
    [InlineData("{\"imageMaxBytes\": -1}")]
    [InlineData("{\"imageMaxBytes\": \"500KB\"}")]
    [InlineData("{\"gifMaxBytes\": 1.5}")]
    [InlineData("{\"version\": 0}")]
    public void Load_InvalidFile_ThrowsAndDoesNotInventDefaults(string json)
    {
        var path = Path.Combine(TempDir(), NoteImageLimitsStore.FileName);
        File.WriteAllText(path, json);

        var error = Assert.Throws<NoteImageLimitsException>(() => NoteImageLimitsStore.LoadFile(path));

        Assert.Equal(path, error.ConfigPath);
        Assert.Contains(NoteImageLimitsException.DisplayText, error.Message, StringComparison.Ordinal);
        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Save_RefusesToOverwriteCorruptFile()
    {
        var path = Path.Combine(TempDir(), NoteImageLimitsStore.FileName);
        var original = "{ this is not json";
        File.WriteAllText(path, original);

        Assert.Throws<NoteImageLimitsException>(() => NoteImageLimitsStore.SaveFile(path, NoteImageLimits.CreateDefault()));

        Assert.Equal(original, File.ReadAllText(path));
        Assert.DoesNotContain(
            Directory.GetFiles(Path.GetDirectoryName(path)!),
            file => Path.GetFileName(file).Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public void Save_WritesAtomicallyAndRoundTrips()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, NoteImageLimitsStore.FileName);
        var limits = new NoteImageLimits
        {
            ImageMaxBytes = 300 * 1024,
            GifMaxBytes = 1024 * 1024
        };

        NoteImageLimitsStore.SaveFile(path, limits);

        Assert.DoesNotContain(Directory.GetFiles(dir), file => Path.GetFileName(file).Contains(".tmp-", StringComparison.Ordinal));
        var loaded = NoteImageLimitsStore.LoadFile(path);
        Assert.Equal(300 * 1024, loaded.ImageMaxBytes);
        Assert.Equal(1024 * 1024, loaded.GifMaxBytes);
        Assert.Equal(JsonSerializer.Serialize(limits, JsonUtil.Options) + "\n", File.ReadAllText(path));
    }

    [Fact]
    public void Save_WhenDirectoryIsNotWritable_LeavesOriginalIntact()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var dir = TempDir();
        var path = Path.Combine(dir, NoteImageLimitsStore.FileName);
        NoteImageLimitsStore.SaveFile(path, NoteImageLimits.CreateDefault());
        var original = File.ReadAllText(path);
        var mode = File.GetUnixFileMode(dir);
        try
        {
            File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var error = Record.Exception(() => NoteImageLimitsStore.SaveFile(path, new NoteImageLimits
            {
                ImageMaxBytes = 1,
                GifMaxBytes = 1
            }));
            Assert.NotNull(error);
        }
        finally
        {
            File.SetUnixFileMode(dir, mode);
        }

        Assert.Equal(original, File.ReadAllText(path));
        Assert.DoesNotContain(Directory.GetFiles(dir), file => Path.GetFileName(file).Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryFile_UsesDocumentedDefaults()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var path = Path.Combine(Path.GetDirectoryName(profilePath!)!, NoteImageLimitsStore.FileName);
        Assert.True(File.Exists(path), path);

        var limits = NoteImageLimitsStore.LoadFile(path);
        Assert.Equal(NoteImageThreshold.DefaultImageMaxBytes, limits.ImageMaxBytes);
        Assert.Equal(NoteImageThreshold.DefaultGifMaxBytes, limits.GifMaxBytes);
        var expected = JsonSerializer.Serialize(NoteImageLimits.CreateDefault(), JsonUtil.Options) + "\n";
        Assert.Equal(expected, File.ReadAllText(path));
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-limits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
