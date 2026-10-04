using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 左侧栏目或作品节点。
/// </summary>
public sealed class NavNodeViewModel : ViewModelBase
{
    private bool mIsSelected;
    private bool mIsExpanded = true;
    private string mTitle;
    private string mSubtitle;
    private WorkCatalogItem? mWork;

    public NavNodeViewModel(ChannelProfile channel, IEnumerable<NavNodeViewModel> children)
    {
        Channel = channel;
        mTitle = channel.Zh;
        mSubtitle = channel.Deco;
        Children = new ObservableCollection<NavNodeViewModel>(children);
    }

    public NavNodeViewModel(ChannelProfile channel, WorkCatalogItem work)
    {
        Channel = channel;
        mWork = work;
        mTitle = work.Title;
        mSubtitle = work.IsUnregistered ? "未登记" : work.Id;
        Children = new ObservableCollection<NavNodeViewModel>();
    }

    /// <summary>
    /// 栏目下的年份夹。只占一行，子节点是该年的作品。
    /// </summary>
    public NavNodeViewModel(ChannelProfile channel, string yearFolder, IEnumerable<NavNodeViewModel> children)
    {
        Channel = channel;
        YearFolder = yearFolder;
        mTitle = yearFolder;
        mSubtitle = "";
        Children = new ObservableCollection<NavNodeViewModel>(children);
    }

    /// <summary>
    /// 栏目下未登记作品的分组节点。
    /// </summary>
    public NavNodeViewModel(
        ChannelProfile channel,
        string title,
        string subtitle,
        IEnumerable<NavNodeViewModel> children,
        bool isUnregisteredGroup)
    {
        Channel = channel;
        mTitle = title;
        mSubtitle = subtitle;
        IsUnregisteredGroup = isUnregisteredGroup;
        Children = new ObservableCollection<NavNodeViewModel>(children);
    }

    public ChannelProfile Channel { get; }
    public WorkCatalogItem? Work
    {
        get => mWork;
        private set => SetField(ref mWork, value);
    }

    public bool IsUnregisteredGroup { get; }

    /// <summary>
    /// 四位年份夹名。不是年份行时为空。
    /// </summary>
    public string? YearFolder { get; }

    public bool IsYearGroup => !string.IsNullOrWhiteSpace(YearFolder);

    /// <summary>
    /// 年份行只显示年份，不画副标与计数。
    /// </summary>
    public bool ShowsDetailLine => !IsYearGroup;

    public bool IsUnregistered => IsUnregisteredGroup || Work?.IsUnregistered == true;

    public string Title
    {
        get => mTitle;
        private set => SetField(ref mTitle, value);
    }

    public string Subtitle
    {
        get => mSubtitle;
        private set => SetField(ref mSubtitle, value);
    }

    public ObservableCollection<NavNodeViewModel> Children { get; }

    /// <summary>
    /// 副标前的上页 / 隐藏 / 撤下 / 回收计数，零项不出现。
    /// </summary>
    public ObservableCollection<NavMarkCountPart> MarkCountParts { get; } = new();

    /// <summary>
    /// 该节点是否有未执行标记。
    /// </summary>
    public bool HasMarkCounts => MarkCountParts.Count > 0;

    /// <summary>
    /// 有计数时副标左留一空，形成「5·3 xiaowayao」。
    /// </summary>
    public Thickness SubtitleIndent => HasMarkCounts ? new Thickness(4, 0, 0, 0) : new Thickness(0);

    /// <summary>
    /// 跨刷新复用的稳定键：栏目 / 作品 / 未登记分组。
    /// </summary>
    public string NavKey
    {
        get
        {
            if (IsYearGroup)
            {
                return "year:" + Channel.Key + ":" + YearFolder;
            }

            if (IsUnregisteredGroup)
            {
                return "unreg-group:" + Channel.Key;
            }

            if (Work != null)
            {
                return (Work.IsUnregistered ? "unreg:" : "work:") + Channel.Key + ":" + Work.Id;
            }

            return "channel:" + Channel.Key;
        }
    }

    public bool IsSelected
    {
        get => mIsSelected;
        set => SetField(ref mIsSelected, value);
    }

    public bool IsExpanded
    {
        get => mIsExpanded;
        set => SetField(ref mIsExpanded, value);
    }

    /// <summary>
    /// 就地写入题名与作品快照，不更换节点身份。
    /// </summary>
    public void ApplyCatalog(NavNodeViewModel incoming)
    {
        Title = incoming.Title;
        Subtitle = incoming.Subtitle;
        Work = incoming.Work;
    }

    /// <summary>
    /// 按四项计数刷新副标前的色块数字。
    /// </summary>
    public void ApplyMarkCounts(MarkCountTally tally)
    {
        MarkCountParts.Clear();
        var partList = MarkDraftStore.VisibleParts(tally);
        for (var i = 0; i < partList.Count; i++)
        {
            var (intent, count) = partList[i];
            MarkCountParts.Add(new NavMarkCountPart(
                count.ToString(),
                BrushOf(intent),
                i < partList.Count - 1));
        }

        Raise(nameof(HasMarkCounts));
        Raise(nameof(SubtitleIndent));
    }

    /// <summary>
    /// 上页绿、隐藏灰、撤下黄、回收红；比角标略亮，不透明。
    /// </summary>
    private static Brush BrushOf(MediaIntent intent)
    {
        return intent switch
        {
            MediaIntent.StageIngest or MediaIntent.SiteRestore => BrushCache.NavMarkIngest,
            MediaIntent.SiteHide => BrushCache.NavMarkHide,
            MediaIntent.SiteWithdraw => BrushCache.NavMarkWithdraw,
            MediaIntent.StageRecycle => BrushCache.NavMarkRecycle,
            _ => BrushCache.Muted
        };
    }
}

/// <summary>
/// 项目栏副标前的一节计数。
/// </summary>
public sealed class NavMarkCountPart
{
    public NavMarkCountPart(string text, Brush brush, bool showSeparator)
    {
        Text = text;
        Brush = brush;
        ShowSeparator = showSeparator;
    }

    public string Text { get; }
    public Brush Brush { get; }
    public bool ShowSeparator { get; }
}
