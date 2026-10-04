using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class WorkRegisterFieldHelpTests
{
    [Fact]
    public void Labels_AreShortNames()
    {
        Assert.Equal("栏目", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.Channel));
        Assert.Equal("投放夹", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.StageFolder));
        Assert.Equal("作品 id", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.WorkId));
        Assert.Equal("题名", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.Title));
        Assert.Equal("开始日", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.StartedOn));
        Assert.Equal("地点", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.Place));
        Assert.DoesNotContain("已冻结", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.Channel));
        Assert.DoesNotContain("小写短横线", WorkRegisterFieldHelp.Label(WorkRegisterFieldHelp.Field.WorkId));
    }

    [Fact]
    public void Tooltip_HasExplainAndThreeExamples()
    {
        var text = WorkRegisterFieldHelp.FormatTooltip(
            WorkRegisterFieldHelp.Field.WorkId,
            session: null,
            random: new Random(7));
        Assert.Contains("内部编号", text, StringComparison.Ordinal);
        Assert.Contains("例如：", text, StringComparison.Ordinal);
        Assert.Equal(3, CountExamples(text));
        var typicalList = WorkRegisterFieldHelp.TypicalExamples(WorkRegisterFieldHelp.Field.WorkId);
        foreach (var line in text.Split('\n').Where(item => item.TrimStart().StartsWith("· ", StringComparison.Ordinal)))
        {
            Assert.Contains(line.Trim()[2..], typicalList);
        }
    }

    [Fact]
    public void PickRandom_UsesSeedAndDoesNotRepeat()
    {
        var pool = new[] { "a", "b", "c", "d", "e" };
        var first = WorkRegisterFieldHelp.PickRandom(pool, 3, new Random(11));
        var second = WorkRegisterFieldHelp.PickRandom(pool, 3, new Random(11));
        Assert.Equal(first, second);
        Assert.Equal(3, first.Count);
        Assert.Equal(first.Distinct(StringComparer.Ordinal).Count(), first.Count);
        Assert.All(first, item => Assert.Contains(item, pool));
    }

    [Fact]
    public void CollectExamples_IncludesSessionWorksAndTypical()
    {
        var session = new WorkspaceSession
        {
            Profile = new WorkspaceProfile
            {
                Channels =
                {
                    new ChannelProfile { Key = "digital-twin", Zh = "数字孪生" }
                }
            },
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "ningbo-siweier",
                    Channel = "digital-twin",
                    Title = "宁波四维尔",
                    StageFolder = "digital-twin/20240530_宁波四维尔"
                },
                new WorkCatalogItem
                {
                    Id = "leftover",
                    Channel = "digital-twin",
                    Title = "剩余",
                    IsUnregistered = true
                }
            ]
        };

        var channelList = WorkRegisterFieldHelp.CollectExamples(WorkRegisterFieldHelp.Field.Channel, session);
        Assert.Contains("digital-twin（数字孪生）", channelList);

        var folderList = WorkRegisterFieldHelp.CollectExamples(WorkRegisterFieldHelp.Field.StageFolder, session);
        Assert.Contains("digital-twin/20240530_宁波四维尔", folderList);
        Assert.Contains(
            "landscape-rendering/上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑",
            folderList);
        Assert.DoesNotContain("leftover", folderList);

        var titleList = WorkRegisterFieldHelp.CollectExamples(WorkRegisterFieldHelp.Field.Title, session);
        Assert.Contains("宁波四维尔", titleList);
        Assert.DoesNotContain("剩余", titleList);
    }

    [Fact]
    public void WorkIdTooltip_FollowsChannel()
    {
        var photo = WorkRegisterFieldHelp.FormatTooltip(
            WorkRegisterFieldHelp.Field.WorkId,
            session: null,
            random: new Random(3),
            channelKey: "real-world-photo");
        Assert.Contains("项目夹名", photo, StringComparison.Ordinal);
        Assert.DoesNotContain("不能直接当编号", photo, StringComparison.Ordinal);
        Assert.Equal(3, CountExamples(photo));
        var photoTypical = WorkRegisterFieldHelp.TypicalExamples(
            WorkRegisterFieldHelp.Field.WorkId,
            "real-world-photo");
        Assert.Contains("20250413 上海 静安寺", photoTypical);
        Assert.DoesNotContain("xiaowayao", photoTypical);
        foreach (var line in photo.Split('\n').Where(item => item.TrimStart().StartsWith("· ", StringComparison.Ordinal)))
        {
            Assert.Contains(line.Trim()[2..], photoTypical);
        }

        var rendering = WorkRegisterFieldHelp.FormatTooltip(
            WorkRegisterFieldHelp.Field.WorkId,
            session: null,
            random: new Random(7),
            channelKey: "landscape-rendering");
        Assert.Contains("不能直接当编号", rendering, StringComparison.Ordinal);
        Assert.Contains(
            "xiaowayao",
            WorkRegisterFieldHelp.TypicalExamples(WorkRegisterFieldHelp.Field.WorkId, "landscape-rendering"));

        var session = new WorkspaceSession
        {
            Profile = new WorkspaceProfile(),
            Works =
            [
                new WorkCatalogItem
                {
                    Id = "20191125 新加坡",
                    Channel = "real-world-photo",
                    Title = "新加坡"
                },
                new WorkCatalogItem
                {
                    Id = "ningbo-siweier",
                    Channel = "digital-twin",
                    Title = "宁波四维尔"
                }
            ]
        };
        var photoPool = WorkRegisterFieldHelp.CollectExamples(
            WorkRegisterFieldHelp.Field.WorkId,
            session,
            "real-world-photo");
        Assert.Contains("20191125 新加坡", photoPool);
        Assert.DoesNotContain("ningbo-siweier", photoPool);
        var renderingPool = WorkRegisterFieldHelp.CollectExamples(
            WorkRegisterFieldHelp.Field.WorkId,
            session,
            "landscape-rendering");
        Assert.Contains("ningbo-siweier", renderingPool);
        Assert.DoesNotContain("20191125 新加坡", renderingPool);
    }

    [Fact]
    public void TypicalExamples_MatchPersonalWorksStyle()
    {
        Assert.Contains("xiaowayao", WorkRegisterFieldHelp.TypicalExamples(WorkRegisterFieldHelp.Field.WorkId));
        Assert.Contains("北京丰台小瓦窑", WorkRegisterFieldHelp.TypicalExamples(WorkRegisterFieldHelp.Field.Title));
        Assert.Contains(
            "landscape-photo/20220912 鹤壁 星空",
            WorkRegisterFieldHelp.TypicalExamples(WorkRegisterFieldHelp.Field.StageFolder));
    }

    /// <summary>
    /// 提示里「· 」开头的示例行数。
    /// </summary>
    private static int CountExamples(string text)
    {
        return text.Split('\n').Count(line => line.TrimStart().StartsWith("· ", StringComparison.Ordinal));
    }
}
