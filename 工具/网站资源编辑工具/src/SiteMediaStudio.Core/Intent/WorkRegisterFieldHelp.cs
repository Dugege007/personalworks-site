namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 登记窗字段的短标签、说明与示例。
/// </summary>
public static class WorkRegisterFieldHelp
{
    public const int ExampleCount = 3;

    /// <summary>
    /// 登记窗上的一项。
    /// </summary>
    public enum Field
    {
        Channel,
        StageFolder,
        WorkId,
        Title,
        StartedOn,
        Place
    }

    /// <summary>
    /// 窗内短标签，细则放到说明里。
    /// </summary>
    public static string Label(Field field)
    {
        return field switch
        {
            Field.Channel => "栏目",
            Field.StageFolder => "投放夹",
            Field.WorkId => "作品 id",
            Field.Title => "题名",
            Field.StartedOn => "开始日",
            Field.Place => "地点",
            _ => ""
        };
    }

    /// <summary>
    /// 该项在登记里起什么作用。作品编号不看栏目时，按技术键说明。
    /// </summary>
    public static string Explain(Field field)
    {
        return Explain(field, null);
    }

    /// <summary>
    /// 该项在登记里起什么作用。现实摄影、游戏摄影、AI摄影的编号用项目夹名。
    /// </summary>
    public static string Explain(Field field, string? channelKey)
    {
        if (field == Field.WorkId && WorkRegisterRules.IsPhotoFolderChannel(channelKey))
        {
            return "作品的内部编号，与中转站项目夹名相同，可含中文、空格和日期。详情与对象键也用这个名字。登记后一般不再改。";
        }

        return field switch
        {
            Field.Channel => "作品会出现在网站的哪一栏。这里已经定好，不能改。",
            Field.StageFolder => "中转站里这个作品对应的文件夹。用来认领夹里的图，不是网页上的名字。",
            Field.WorkId => "作品的内部编号。只用小写英文、数字和短横线；中文夹名不能直接当编号。登记后一般不再改。",
            Field.Title => "网页上看到的作品名称。可以用中文，写清楚项目或地点即可。",
            Field.StartedOn => "列表右侧的日期。可写 YYYY-MM-DD、YYYY-MM 或 YYYY。夹名开头的日期会先填在这里，仍可改。",
            Field.Place => "列表右侧斜杠后面的城市。只写城市名，例如无锡、上海。夹名里没有地点时须手填。",
            _ => ""
        };
    }

    /// <summary>
    /// 组装悬停说明：作用 + 随机三则示例。摄影投放夹的作品编号按夹名举例。
    /// </summary>
    public static string FormatTooltip(
        Field field,
        WorkspaceSession? session,
        Random? random = null,
        string? channelKey = null)
    {
        var exampleList = PickExamples(field, session, random, channelKey);
        return FormatTooltip(Explain(field, channelKey), exampleList);
    }

    /// <summary>
    /// 把说明与示例拼成一段提示。
    /// </summary>
    public static string FormatTooltip(string explain, IReadOnlyList<string> exampleList)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(explain.Trim());
        if (exampleList.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine();
        builder.Append("例如：");
        foreach (var example in exampleList)
        {
            builder.AppendLine();
            builder.Append("· ").Append(example);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 从已入编作品与典型写法里抽出三则示例。
    /// </summary>
    public static IReadOnlyList<string> PickExamples(
        Field field,
        WorkspaceSession? session,
        Random? random = null,
        string? channelKey = null)
    {
        var pool = CollectExamples(field, session, channelKey);
        return PickRandom(pool, ExampleCount, random);
    }

    /// <summary>
    /// 汇合当前工作区已入编写法与站点典型写法。
    /// </summary>
    public static IReadOnlyList<string> CollectExamples(
        Field field,
        WorkspaceSession? session,
        string? channelKey = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pool = new List<string>();
        foreach (var example in EnumerateSessionExamples(field, session, channelKey)
                     .Concat(TypicalExamples(field, channelKey)))
        {
            if (string.IsNullOrWhiteSpace(example) || !seen.Add(example))
            {
                continue;
            }

            pool.Add(example);
        }

        return pool;
    }

    /// <summary>
    /// 从列表中不重复抽取指定条数。
    /// </summary>
    public static IReadOnlyList<string> PickRandom(
        IReadOnlyList<string> pool,
        int count,
        Random? random = null)
    {
        var candidateList = pool
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (candidateList.Count <= count)
        {
            return candidateList;
        }

        var picker = random ?? Random.Shared;
        for (var i = candidateList.Count - 1; i > 0; i--)
        {
            var j = picker.Next(i + 1);
            (candidateList[i], candidateList[j]) = (candidateList[j], candidateList[i]);
        }

        return candidateList.Take(count).ToList();
    }

    /// <summary>
    /// 站点里已经用过的典型填法，作示例底池。
    /// </summary>
    public static IReadOnlyList<string> TypicalExamples(Field field, string? channelKey = null)
    {
        if (field == Field.WorkId && WorkRegisterRules.IsPhotoFolderChannel(channelKey))
        {
            return new[]
            {
                "20250413 上海 静安寺",
                "20191125 新加坡",
                "20250519 安阳 殷墟博物馆",
                "20211119 南京"
            };
        }

        return field switch
        {
            Field.Channel => new[]
            {
                "digital-twin（数字孪生）",
                "landscape-rendering（景观效果图）",
                "landscape-photo（风光摄影）",
                "humanist-photo（人文摄影）",
                "line-sim（产线仿真）"
            },
            Field.StageFolder => new[]
            {
                "digital-twin/20240530_宁波四维尔",
                "landscape-rendering/上海道田景观工程咨询有限公司/201804 北京丰台小瓦窑",
                "landscape-photo/20220912 鹤壁 星空",
                "humanist-photo/2024 安阳 殷墟博物馆"
            },
            Field.WorkId => new[]
            {
                "ningbo-siweier",
                "xiaowayao",
                "singapore-20191125",
                "huaian-fukang-15",
                "anyang-yinxu-museum",
                "shanghai-mansheng-packaging"
            },
            Field.Title => new[]
            {
                "宁波四维尔",
                "北京丰台小瓦窑",
                "新加坡",
                "淮安富康城15#",
                "安阳 殷墟博物馆",
                "上海曼盛包装"
            },
            Field.StartedOn => new[]
            {
                "2025-02-20",
                "2024-09-26",
                "2018-04",
                "2025"
            },
            Field.Place => new[]
            {
                "无锡",
                "上海",
                "株洲",
                "宁波",
                "北京"
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// 当前工作区已入编作品里的填法。
    /// </summary>
    private static IEnumerable<string> EnumerateSessionExamples(
        Field field,
        WorkspaceSession? session,
        string? channelKey)
    {
        if (session == null)
        {
            yield break;
        }

        if (field == Field.Channel)
        {
            foreach (var channel in session.Profile.Channels)
            {
                if (string.IsNullOrWhiteSpace(channel.Key))
                {
                    continue;
                }

                yield return string.IsNullOrWhiteSpace(channel.Zh)
                    ? channel.Key
                    : channel.Key + "（" + channel.Zh + "）";
            }

            yield break;
        }

        foreach (var work in session.Works)
        {
            if (work.IsUnregistered)
            {
                continue;
            }

            switch (field)
            {
                case Field.StageFolder:
                    if (!string.IsNullOrWhiteSpace(work.StageFolder))
                    {
                        yield return JsonUtil.ToRel(work.StageFolder);
                    }
                    else if (!string.IsNullOrWhiteSpace(work.Id))
                    {
                        yield return WorkRegisterRules.StageFolder(work.Channel, work.Id);
                    }

                    break;
                case Field.WorkId:
                    if (WorkRegisterRules.IsPhotoFolderChannel(channelKey))
                    {
                        if (WorkRegisterRules.IsPhotoFolderChannel(work.Channel)
                            && WorkRegisterRules.IsProjectFolderName(work.Id))
                        {
                            yield return work.Id;
                        }
                    }
                    else if (WorkRegisterRules.IsTechnicalKey(work.Id))
                    {
                        yield return work.Id;
                    }

                    break;
                case Field.Title:
                    if (!string.IsNullOrWhiteSpace(work.Title))
                    {
                        yield return work.Title;
                    }

                    break;
                case Field.StartedOn:
                    if (!string.IsNullOrWhiteSpace(work.StartedOn))
                    {
                        yield return work.StartedOn;
                    }

                    break;
                case Field.Place:
                    if (!string.IsNullOrWhiteSpace(work.Place))
                    {
                        yield return work.Place;
                    }

                    break;
            }
        }
    }
}
