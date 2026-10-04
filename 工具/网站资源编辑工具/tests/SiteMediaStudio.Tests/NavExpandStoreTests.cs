using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NavExpandStoreTests
{
    [Fact]
    public void ToExpandDict_MarksCollapsedKeysFalse()
    {
        var expandDict = NavExpandStore.ToExpandDict(new[] { "channel:photo", "", "channel:game-dev" });
        Assert.False(expandDict["channel:photo"]);
        Assert.False(expandDict["channel:game-dev"]);
        Assert.Equal(2, expandDict.Count);
    }

    [Fact]
    public void WriteAndRead_UsesNormalizedProfilePath()
    {
        var settings = new AppSettings();
        var path = @"D:\work\site-media-studio\profiles\personalworks.json";
        NavExpandStore.WriteCollapsed(settings, path, new[] { "channel:landscape-cds" });

        var slashPath = path.Replace('\\', '/');
        var collapsed = NavExpandStore.ReadCollapsed(settings, slashPath);
        Assert.Equal(new[] { "channel:landscape-cds" }, collapsed);
    }

    [Fact]
    public void ReadCollapsed_UnknownProfileIsEmpty()
    {
        var settings = new AppSettings();
        Assert.Empty(NavExpandStore.ReadCollapsed(settings, @"D:\other\profile.json"));
    }
}
