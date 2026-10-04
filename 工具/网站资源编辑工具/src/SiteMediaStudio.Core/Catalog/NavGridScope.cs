namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 左侧多选时，网格覆盖哪些已入编摄影项目。
/// </summary>
public static class NavGridScope
{
    /// <summary>
    /// 一个已选节点上的作品。没有作品时编号为空。
    /// </summary>
    public readonly record struct NavWorkPick(string Channel, string? WorkId, bool Registered);

    /// <summary>
    /// 网格要铺开的摄影项目。栏目与编号成对，顺序与选区一致。
    /// </summary>
    public readonly record struct PhotoWorkRef(string Channel, string WorkId);

    /// <summary>
    /// 一个已选中的摄影年份层。
    /// </summary>
    public readonly record struct YearRef(string Channel, string Year);

    /// <summary>
    /// 已入编摄影项目达到两个才合并。未登记、非摄影项目不计入。不足两个则返回空，网格仍跟焦点。
    /// </summary>
    public static IReadOnlyList<PhotoWorkRef> PhotoProjectsForGrid(IReadOnlyList<NavWorkPick> selected)
    {
        var list = new List<PhotoWorkRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pick in selected)
        {
            if (!pick.Registered || string.IsNullOrWhiteSpace(pick.WorkId))
            {
                continue;
            }

            if (!WorkRegisterRules.IsPhotoFolderChannel(pick.Channel))
            {
                continue;
            }

            var key = pick.Channel + "\n" + pick.WorkId;
            if (!seen.Add(key))
            {
                continue;
            }

            list.Add(new PhotoWorkRef(pick.Channel, pick.WorkId));
        }

        return list.Count >= 2 ? list : Array.Empty<PhotoWorkRef>();
    }

    /// <summary>
    /// 已选摄影年份达到两个才合并。不足两个则返回空，网格仍跟焦点年份。
    /// </summary>
    public static IReadOnlyList<YearRef> YearsForGrid(IReadOnlyList<YearRef> selected)
    {
        var list = new List<YearRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var year in selected)
        {
            if (!WorkRegisterRules.IsPhotoFolderChannel(year.Channel) || string.IsNullOrWhiteSpace(year.Year))
            {
                continue;
            }

            var key = year.Channel + "\n" + year.Year;
            if (!seen.Add(key))
            {
                continue;
            }

            list.Add(year);
        }

        return list.Count >= 2 ? list : Array.Empty<YearRef>();
    }
}
