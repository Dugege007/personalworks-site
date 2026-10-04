using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PublishMatchAuditTests
{
    /// <summary>
    /// 旧实拍三栏已退出工具栏目，扫描不再把投放箱文件归到这三栏。
    /// </summary>
    [Fact]
    public void PersonalWorks_RetiredPhotoChannels_StayOutOfTheToolTree()
    {
        var profilePath = ToolPaths.FindPersonalWorksProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        Assert.DoesNotContain(session.Profile.Channels, item =>
            item.Key is "landscape-photo" or "humanist-photo" or "portrait-photo");
        Assert.DoesNotContain(session.StageItems, item =>
            item.ChannelKey is "landscape-photo" or "humanist-photo" or "portrait-photo");
    }
}
