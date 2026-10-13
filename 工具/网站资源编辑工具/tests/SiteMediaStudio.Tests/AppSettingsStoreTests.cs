using System.Text.Json;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void PhotoSwitches_MissingFields_StayOn_AndPathsStayEmpty()
    {
        Assert.True(new AppSettings().StripGpsEnabled);
        Assert.True(new AppSettings().KeepCaptureParamsEnabled);
        Assert.Null(new AppSettings().PngquantPath);
        Assert.Null(new AppSettings().OxipngPath);
        Assert.Null(new AppSettings().GifsiclePath);

        var loaded = JsonSerializer.Deserialize<AppSettings>("{}", JsonUtil.Options);
        Assert.NotNull(loaded);
        Assert.True(loaded!.StripGpsEnabled);
        Assert.True(loaded.KeepCaptureParamsEnabled);
        Assert.Null(loaded.PngquantPath);
        Assert.Null(loaded.OxipngPath);
        Assert.Null(loaded.GifsiclePath);
    }

    [Fact]
    public void Save_RoundTripsPhotoSwitchesAndToolPaths_WithoutLeavingTempFiles()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "settings.json");
        var settings = new AppSettings
        {
            ThumbnailPx = 320,
            StripGps = false,
            KeepCaptureParams = true,
            PngquantPath = @"C:\Tools\pngquant.exe",
            OxipngPath = @"C:\Tools\oxipng.exe",
            GifsiclePath = @"C:\Tools\gifsicle.exe"
        };

        AppSettingsStore.Save(settings, path);

        Assert.DoesNotContain(Directory.GetFiles(dir), file => Path.GetFileName(file).Contains(".tmp-", StringComparison.Ordinal));
        var loaded = AppSettingsStore.Load(path);
        Assert.Equal(320, loaded.ThumbnailPx);
        Assert.False(loaded.StripGpsEnabled);
        Assert.True(loaded.KeepCaptureParamsEnabled);
        Assert.Equal(settings.PngquantPath, loaded.PngquantPath);
        Assert.Equal(settings.OxipngPath, loaded.OxipngPath);
        Assert.Equal(settings.GifsiclePath, loaded.GifsiclePath);
        Assert.Equal(JsonSerializer.Serialize(settings, JsonUtil.Options), File.ReadAllText(path));

        settings.ThumbnailPx = 160;
        AppSettingsStore.Save(settings, path);
        loaded = AppSettingsStore.Load(path);
        Assert.Equal(160, loaded.ThumbnailPx);
        Assert.Equal(JsonSerializer.Serialize(settings, JsonUtil.Options), File.ReadAllText(path));
    }

    [Fact]
    public void Load_MissingFile_UsesDefaults()
    {
        var path = Path.Combine(TempDir(), "missing.json");
        var loaded = AppSettingsStore.Load(path);
        Assert.Equal(240, loaded.ThumbnailPx);
        Assert.True(loaded.WatchStageEnabled);
        Assert.True(loaded.StripGpsEnabled);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("<<<<<<< HEAD")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Load_InvalidFile_ThrowsAndDoesNotInventDefaults(string json)
    {
        var path = Path.Combine(TempDir(), "settings.json");
        File.WriteAllText(path, json);

        var error = Assert.Throws<AppSettingsException>(() => AppSettingsStore.Load(path));

        Assert.Equal(path, error.ConfigPath);
        Assert.Contains(AppSettingsException.DisplayText, error.Message, StringComparison.Ordinal);
        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Save_RefusesToOverwriteCorruptFile()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "settings.json");
        var original = "{ this is not json";
        File.WriteAllText(path, original);

        var error = Assert.Throws<AppSettingsException>(() => AppSettingsStore.Save(new AppSettings { ThumbnailPx = 120 }, path));

        Assert.Equal(path, error.ConfigPath);
        Assert.Contains(AppSettingsException.DisplayText, error.Message, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.DoesNotContain(Directory.GetFiles(dir), file => Path.GetFileName(file).Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public void Save_WhenDirectoryIsNotWritable_LeavesOriginalIntact()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var dir = TempDir();
        var path = Path.Combine(dir, "settings.json");
        var original = "{\"thumbnailPx\": 180}";
        File.WriteAllText(path, original);
        var mode = File.GetUnixFileMode(dir);
        try
        {
            File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var error = Record.Exception(() => AppSettingsStore.Save(new AppSettings { ThumbnailPx = 120 }, path));
            Assert.NotNull(error);
        }
        finally
        {
            File.SetUnixFileMode(dir, mode);
        }

        Assert.Equal(original, File.ReadAllText(path));
        Assert.DoesNotContain(Directory.GetFiles(dir), file => Path.GetFileName(file).Contains(".tmp-", StringComparison.Ordinal));
    }

    private static string TempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
