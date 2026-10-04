using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PhotoStageParentTests
{
    [Fact]
    public void PersonalWorks_PhotoChannels_SitUnderPhotoFolder()
    {
        var profile = LoadPersonalWorks();
        var real = Assert.Single(profile.Channels, item => item.Key == "real-world-photo");
        Assert.Equal("摄影（photo）", real.StageParent);
        Assert.Equal(
            "摄影（photo）/现实摄影（real-world-photo）",
            WorkspaceProfileLoader.ChannelStageRelative(profile, real));

        var game = Assert.Single(profile.Channels, item => item.Key == "game-photo");
        Assert.Equal(
            "摄影（photo）/游戏摄影（game-photo）",
            WorkspaceProfileLoader.ChannelStageRelative(profile, game));

        Assert.DoesNotContain(profile.Channels, item =>
            item.Key is "landscape-photo" or "humanist-photo" or "portrait-photo");
    }

    [Fact]
    public void TryRead_SkipsPhotoParent_KeepsLegacyChannelSegment()
    {
        var channel = new ChannelProfile
        {
            Key = "real-world-photo",
            Zh = "现实摄影",
            YearContainers = true,
            StageParent = "摄影（photo）"
        };
        Assert.True(StageWorkFolder.TryReadFromStageRel(
            "摄影（photo）/现实摄影（real-world-photo）/20250413 上海 静安寺/DSC04068.jpg",
            channel,
            out var workId,
            out var folder,
            out var channelRoot));
        Assert.False(channelRoot);
        Assert.Equal("20250413 上海 静安寺", workId);
        Assert.Equal("20250413 上海 静安寺", folder);

        Assert.True(StageWorkFolder.TryReadFromStageRel(
            "摄影（photo）/现实摄影（real-world-photo）/20250413 上海 静安寺/.site-ready/old/01.webp",
            channel,
            out workId,
            out _,
            out channelRoot));
        Assert.False(channelRoot);
        Assert.Equal("20250413 上海 静安寺", workId);

        var legacy = new ChannelProfile
        {
            Key = "landscape-photo",
            Zh = "风光摄影",
            YearContainers = true
        };
        Assert.True(StageWorkFolder.TryReadFromStageRel(
            "风光摄影（landscape-photo）/20191125 新加坡/P1330899-3.jpg",
            legacy,
            out var legacyId,
            out _,
            out _));
        Assert.Equal("20191125 新加坡", legacyId);
    }

    [Fact]
    public void Scan_RealWorldPhoto_FindsShootsUnderPhotoFolder()
    {
        var profile = LoadPersonalWorks();
        var items = StageCatalogScanner.Scan(profile, new Dictionary<string, LedgerRecord>());
        var jingan = items
            .Where(item => item.ChannelKey == "real-world-photo" && item.WorkIdGuess == "20250413 上海 静安寺")
            .ToList();
        Assert.Equal(4, jingan.Count);
        Assert.All(jingan, item => Assert.StartsWith(
            "摄影（photo）/现实摄影（real-world-photo）/20250413 上海 静安寺/",
            item.StageRel));
        Assert.DoesNotContain(items, item => item.StageRel.StartsWith("现实摄影（real-world-photo）/", StringComparison.Ordinal));

        var found = UnregisteredWorkDiscovery.Discover([], items, profile: profile);
        var ids = found.Where(work => work.Channel == "real-world-photo").Select(work => work.Id).ToHashSet();
        Assert.Equal(24, ids.Count);
        Assert.Contains("20250413 上海 静安寺", ids);
        Assert.Contains("20260814 舟山 东极岛 东福山", ids);
        Assert.Contains("20260815 舟山 东极岛 庙子湖", ids);
        Assert.DoesNotContain(ids, id => id is "摄影（photo）" or "现实摄影（real-world-photo）");
    }

    private static WorkspaceProfile LoadPersonalWorks()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        return WorkspaceProfileLoader.Load(profilePath!);
    }
}
