using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class VideoCatalogTests
{
    [Theory]
    [InlineData("clip.mp4", true)]
    [InlineData("clip.MOV", true)]
    [InlineData("clip.mkv", true)]
    [InlineData("clip.webm", true)]
    [InlineData("clip.jpg", false)]
    [InlineData("clip.gif", false)]
    [InlineData("clip.mp3", false)]
    public void PathRules_RecognizeVideoExt(string name, bool expected)
    {
        Assert.Equal(expected, MediaPathRules.IsVideoFile(name));
        Assert.Equal(expected || MediaPathRules.ImageExtSet.Contains(Path.GetExtension(name)), MediaPathRules.IsCatalogFile(name));
    }

    [Theory]
    [InlineData("workshop-overview-final.jpg", null, "workshop-overview-final", ".jpg")]
    [InlineData("CLIP.MP4", null, "CLIP", ".MP4")]
    [InlineData("(1/5) 总图", "digital-twin/sample/01.webp", "(1/5) 总图", ".webp")]
    [InlineData("readme", "folder/readme", "readme", "")]
    public void PathRules_SplitStickyExtension_KeepsMediaSuffix(
        string text,
        string? fallbackPath,
        string stem,
        string extension)
    {
        MediaPathRules.SplitStickyExtension(text, fallbackPath, out var actualStem, out var actualExt);
        Assert.Equal(stem, actualStem);
        Assert.Equal(extension, actualExt);
    }

    [Theory]
    [InlineData(120, 40, true)]
    [InlineData(40, 40, true)]
    [InlineData(39, 40, false)]
    [InlineData(0, 40, true)]
    public void PathRules_FitsWholeCaption_OnlyPinsWhenOverflow(double available, double fullWidth, bool expected)
    {
        Assert.Equal(expected, MediaPathRules.FitsWholeCaption(available, fullWidth));
        Assert.True(MediaPathRules.FitsWholeCaption(double.PositiveInfinity, 80));
    }

    [Fact]
    public void CoverPosition_UsesOneSecondOrTenth()
    {
        Assert.Equal(1, VideoCoverRules.DefaultPositionSec(null));
        Assert.Equal(1, VideoCoverRules.DefaultPositionSec(12));
        Assert.Equal(0.05, VideoCoverRules.DefaultPositionSec(0.5), 3);
        Assert.Equal(1, VideoCoverRules.DefaultPositionSec(0));
    }

    [Fact]
    public void PreviewRules_VideoGoesExternal_ImageGoesLightbox()
    {
        Assert.Equal(MediaPreviewKind.ExternalPlayer, MediaPreviewRules.KindForPath("a/b.mp4"));
        Assert.Equal(MediaPreviewKind.Lightbox, MediaPreviewRules.KindForPath("a/b.webp"));
    }

    [Fact]
    public void PreviewRules_PreferStageSourceWhenPresent()
    {
        var stage = Path.Combine(Path.GetTempPath(), "sms-src-" + Guid.NewGuid().ToString("N") + ".mp4");
        var preview = Path.Combine(Path.GetTempPath(), "sms-web-" + Guid.NewGuid().ToString("N") + ".mp4");
        File.WriteAllBytes(stage, [0]);
        File.WriteAllBytes(preview, [1]);
        try
        {
            Assert.Equal(stage, MediaPreviewRules.SourcePath(stage, preview));
            File.Delete(stage);
            Assert.Equal(preview, MediaPreviewRules.SourcePath(stage, preview));
        }
        finally
        {
            if (File.Exists(stage))
            {
                File.Delete(stage);
            }

            if (File.Exists(preview))
            {
                File.Delete(preview);
            }
        }
    }

    [Fact]
    public void ExternalPlayer_UsesConfiguredPathWhenPresent()
    {
        var fake = Path.Combine(Path.GetTempPath(), "sms-player-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllBytes(fake, [0]);
        try
        {
            Assert.Equal(fake, ExternalPlayer.ResolvePlayerPath(fake));
        }
        finally
        {
            File.Delete(fake);
        }
    }

    [Fact]
    public void Scan_Fixture_IncludesDemoClip()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        Assert.Contains(
            session.StageItems,
            item => item.StageRel.EndsWith("demo-park/clip.mp4", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ingest_VideoFile_IsAllowedWhenTargetCanAppend()
    {
        var path = Path.Combine(Path.GetTempPath(), "sms-" + Guid.NewGuid().ToString("N") + ".mp4");
        File.WriteAllBytes(path, [0]);
        try
        {
            var item = new StageItem
            {
                StageRel = "demo-render/demo-park/clip.mp4",
                FullPath = path,
                ChannelKey = "demo-render",
                IsStock = false
            };
            var decision = IntentGate.EvaluateStage(
                MediaIntent.StageIngest,
                item,
                new IntentContext
                {
                    LedgerDict = new Dictionary<string, LedgerRecord>(),
                    ContentRefCountDict = new Dictionary<string, int>(),
                    TargetWorkId = "demo-park"
                });
            Assert.True(decision.Allowed);
            Assert.DoesNotContain("视频上页未开通", decision.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PosterObjectKey_ReplacesMp4Extension()
    {
        Assert.Equal(
            "digital-twin/sample-2/03.poster.webp",
            VideoEncodeRules.PosterObjectKey("digital-twin/sample-2/03.mp4"));
    }

    [Fact]
    public void AudioBitrate_RejectsAbove192()
    {
        Assert.False(VideoEncodeRules.TryAudioBitrate(193, out _, out var text));
        Assert.Contains("192", text, StringComparison.Ordinal);
        Assert.True(VideoEncodeRules.TryAudioBitrate(128, out var value, out _));
        Assert.Equal(128, value);
    }

    [Fact]
    public void Preview_PersonalWorksVideo_RejectsWhenEncoderMissing()
    {
        if (VideoEncoderHost.CanEncode())
        {
            return;
        }

        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var path = Path.Combine(Path.GetTempPath(), "sms-" + Guid.NewGuid().ToString("N") + ".mp4");
        File.WriteAllBytes(path, [0]);
        try
        {
            var stage = new StageItem
            {
                StageRel = "数字孪生（digital-twin）/demo/clip.mp4",
                FullPath = path,
                ChannelKey = "digital-twin",
                IsStock = false
            };
            var item = new IntentItem
            {
                Intent = MediaIntentCodes.StageIngest,
                WorkId = "unit-test",
                Channel = "digital-twin",
                Object = "digital-twin/unit-test/01.mp4",
                StageRel = stage.StageRel
            };
            var document = new IntentDocument
            {
                Mode = "direct",
                Items = [item],
                Options = new IntentOptions { Deploy = "none" }
            };
            var report = PreviewReporter.Build(
                session,
                document,
                [(item, stage, (SiteItem?)null)],
                "unit-test");
            Assert.True(report.HasHardError);
            Assert.Contains("编码器", report.Lines[0].Decision.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
