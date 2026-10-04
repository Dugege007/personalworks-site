using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class WebpPrepareTests
{
    [Fact]
    public void PreparedStageRel_MatchesPythonLayout()
    {
        var rel = WebpPrepareRules.PreparedStageRel(
            "形象照（profile）/20260905 徐汇区/R0008304.jpg",
            "徐汇区",
            "profile/R0008304.webp");
        Assert.Equal("形象照（profile）/20260905 徐汇区/.site-ready/徐汇区/R0008304.webp", rel);
    }

    [Fact]
    public void SidesFor_ProfileUsesShorterEdge()
    {
        var profile = WebpPrepareRules.SidesFor("profile");
        var other = WebpPrepareRules.SidesFor("landscape-photo");
        var construction = WebpPrepareRules.SidesFor("landscape-cds");
        Assert.Equal(1600, profile.MaxSide);
        Assert.Equal(1200, profile.MinSide);
        Assert.Equal(2560, other.MaxSide);
        Assert.Equal(1920, other.MinSide);
        Assert.Equal(0, construction.MaxSide);
        Assert.Equal(0, construction.MinSide);
        Assert.Equal(20, WebpPrepareRules.FixedQualityFor("landscape-cds"));
        Assert.Null(WebpPrepareRules.FixedQualityFor("landscape-photo"));
        Assert.True(WebpPrepareRules.KeepsSourcePixels("landscape-cds"));
    }

    [Fact]
    public void NeedsPrepare_OnlyPersonalWorksOriginals()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var original = new StageItem
        {
            StageRel = "风光摄影（landscape-photo）/原图.jpg",
            FullPath = "D:/tmp/原图.jpg",
            ChannelKey = "landscape-photo"
        };
        var webp = new StageItem
        {
            StageRel = "风光摄影（landscape-photo）/.site-ready/demo/01.webp",
            FullPath = "D:/tmp/01.webp",
            ChannelKey = "landscape-photo"
        };
        var construction = new StageItem
        {
            StageRel = "景观施工图（landscape-cds）/sample.desense.jpg",
            FullPath = "D:/tmp/sample.desense.jpg",
            ChannelKey = "landscape-cds"
        };

        Assert.True(WebpPrepareRules.NeedsPrepare(original, session.Profile));
        Assert.False(WebpPrepareRules.NeedsPrepare(webp, session.Profile));
        Assert.True(WebpPrepareRules.NeedsPrepare(construction, session.Profile));
        Assert.Equal(".webp", WebpPrepareRules.AllocateExtension(original, session.Profile));
        Assert.Equal(".webp", WebpPrepareRules.AllocateExtension(construction, session.Profile));
    }

    [Fact]
    public void Prepare_OnePng_WritesWebp()
    {
        Assert.True(PythonHost.CanRunPrepare(), "本机需要带 Pillow 的 Python 才能回归压图。");
        Assert.NotNull(ToolPaths.FindPrepareScript());

        var source = Path.Combine(Path.GetTempPath(), "sms-prep-" + Guid.NewGuid().ToString("N") + ".png");
        var output = Path.Combine(Path.GetTempPath(), "sms-prep-" + Guid.NewGuid().ToString("N") + ".webp");
        File.WriteAllBytes(source, MinimalPng);
        try
        {
            var result = WebpPrepareClient.Prepare(source, output, "landscape-photo");
            Assert.True(result.Ok, result.FailureText);
            Assert.True(File.Exists(output));
            var header = File.ReadAllBytes(output);
            Assert.True(header.Length > 12);
            Assert.Equal((byte)'R', header[0]);
            Assert.Equal((byte)'I', header[1]);
            Assert.Equal((byte)'F', header[2]);
            Assert.Equal((byte)'F', header[3]);
            Assert.Equal((byte)'W', header[8]);
            Assert.Equal((byte)'E', header[9]);
            Assert.Equal((byte)'B', header[10]);
            Assert.Equal((byte)'P', header[11]);
            Assert.True(File.Exists(source));
        }
        finally
        {
            TryDelete(source);
            TryDelete(output);
        }
    }

    [Fact]
    public void Prepare_Construction_KeepsPixels_WritesQuality20Webp()
    {
        Assert.True(PythonHost.CanRunPrepare(), "本机需要带 Pillow 的 Python 才能回归压图。");
        Assert.NotNull(ToolPaths.FindPrepareScript());

        var source = Path.Combine(Path.GetTempPath(), "sms-cds-" + Guid.NewGuid().ToString("N") + ".png");
        var output = Path.Combine(Path.GetTempPath(), "sms-cds-" + Guid.NewGuid().ToString("N") + ".webp");
        var makeScript = Path.Combine(Path.GetTempPath(), "sms-cds-make-" + Guid.NewGuid().ToString("N") + ".py");
        var checkScript = Path.Combine(Path.GetTempPath(), "sms-cds-check-" + Guid.NewGuid().ToString("N") + ".py");
        File.WriteAllText(
            makeScript,
            "from PIL import Image\nImage.new('RGB', (2800, 32), (255, 255, 255)).save(r'''" + source + "''')\n");
        File.WriteAllText(
            checkScript,
            "from PIL import Image\n"
            +             "im = Image.open(r'''" + output + "''')\n"
            + "assert im.format == 'WEBP'\n"
            + "assert im.size == (2800, 32)\n"
            + "raw = open(r'''" + output + "''', 'rb').read(16)\n"
            + "assert raw[12:16] != b'VP8L'\n");
        try
        {
            var made = PythonHost.Run(makeScript, Array.Empty<string>(), Path.GetTempPath());
            Assert.True(made.Ok, made.StdErr);
            var result = WebpPrepareClient.Prepare(source, output, "landscape-cds");
            Assert.True(result.Ok, result.FailureText);
            Assert.True(File.Exists(output));
            Assert.True(File.Exists(source));
            Assert.Contains("20 2800x32", result.StdOut, StringComparison.Ordinal);
            var checkedSize = PythonHost.Run(checkScript, Array.Empty<string>(), Path.GetTempPath());
            Assert.True(checkedSize.Ok, checkedSize.StdErr);
        }
        finally
        {
            TryDelete(source);
            TryDelete(output);
            TryDelete(makeScript);
            TryDelete(checkScript);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static readonly byte[] MinimalPng =
        Convert.FromHexString(
            "89504e470d0a1a0a0000000d494844520000001000000010080200000090916836" +
            "0000002349444154789c633c11a0c1400a60224935c3a806e2001391eae0605403" +
            "3180e4500200066c0160f2d04a0b0000000049454e44ae426082");
}
