using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 网格中的一张图。
/// </summary>
public sealed class MediaCardViewModel : ViewModelBase
{
    private MediaIntent mMark = MediaIntent.None;
    private ImageSource? mThumbnail;
    private bool mIsSelected;
    private StageItem? mStage;
    private SiteItem? mSite;
    private string mCaption = "";
    private string mCaptionStem = "";
    private string mCaptionExtension = "";
    private string mSubtitle = "";
    private string mSubtitleStem = "";
    private string mSubtitleExtension = "";
    private string mBadge = "";
    private string mPublishLabel = "";
    private bool mIsUnsigned;
    private string? mPreviewPath;
    private bool mUsesWebPreview;
    private DateTime mPreviewWriteUtc;
    private string? mSortTimePath;
    private bool mSortTimeReady;
    private DateTime? mSortTimeUtc;
    private CardFileFacts mFacts;

    internal MediaCardViewModel(StageItem stage, CardFileFacts facts)
    {
        Kind = CardKind.Stage;
        ApplyStage(stage, facts);
    }

    internal MediaCardViewModel(SiteItem site, CardFileFacts facts)
    {
        Kind = CardKind.Site;
        ApplySite(site, facts);
    }

    public CardKind Kind { get; }

    public StageItem? Stage
    {
        get => mStage;
        private set => SetField(ref mStage, value);
    }

    public SiteItem? Site
    {
        get => mSite;
        private set => SetField(ref mSite, value);
    }

    public string Caption
    {
        get => mCaption;
        private set => SetField(ref mCaption, value);
    }

    public string CaptionStem
    {
        get => mCaptionStem;
        private set => SetField(ref mCaptionStem, value);
    }

    public string CaptionExtension
    {
        get => mCaptionExtension;
        private set => SetField(ref mCaptionExtension, value);
    }

    public string Subtitle
    {
        get => mSubtitle;
        private set => SetField(ref mSubtitle, value);
    }

    public string SubtitleStem
    {
        get => mSubtitleStem;
        private set => SetField(ref mSubtitleStem, value);
    }

    public string SubtitleExtension
    {
        get => mSubtitleExtension;
        private set => SetField(ref mSubtitleExtension, value);
    }

    public string Badge
    {
        get => mBadge;
        private set => SetField(ref mBadge, value);
    }

    public string PublishLabel
    {
        get => mPublishLabel;
        private set => SetField(ref mPublishLabel, value);
    }

    /// <summary>
    /// 须脱敏的栏目里，文件尚未写成片。
    /// </summary>
    public bool IsUnsigned
    {
        get => mIsUnsigned;
        private set => SetField(ref mIsUnsigned, value);
    }

    /// <summary>
    /// 投放箱台账为已发布。待发布撤下只改角标，仍锁定上页和回收。
    /// </summary>
    public bool IsPublished =>
        PublishLabel == PublishStatus.Published
        || (Stage?.IsPendingWithdraw == true
            && string.Equals(Stage.LedgerStatus, "published", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 站点条目已从页面去掉引用，台账尚未撤下。
    /// </summary>
    public bool IsHidden => PublishLabel == PublishStatus.Hidden;
    public Brush PublishBrush => PublishLabel switch
    {
        PublishStatus.Published => BrushCache.PublishPublished,
        PublishStatus.Hidden => BrushCache.PublishHidden,
        PublishStatus.Withdrawn => BrushCache.PublishWithdrawn,
        PublishStatus.Stock => BrushCache.PublishStock,
        PublishStatus.Paired => BrushCache.PublishPaired,
        PublishStatus.Unlisted => BrushCache.PublishUnlisted,
        _ => BrushCache.PublishUnpublished
    };

    /// <summary>
    /// 投放箱「已发布」用蓝，站点「已隐藏」用灰，其余保持金色。
    /// </summary>
    public Brush BadgeBrush =>
        Badge == PublishStatus.Published ? BrushCache.PublishPublished
        : Badge == PublishStatus.Hidden ? BrushCache.PublishHidden
        : Badge == PublishStatus.Withdrawn ? BrushCache.PublishWithdrawn
        : Badge == PublishStatus.Unsigned ? BrushCache.Gold
        : BrushCache.Gold;

    /// <summary>
    /// 投放箱与站点共用的置灰透明度。
    /// </summary>
    public const double DimmedChromeOpacity = 0.5;

    /// <summary>
    /// 投放箱置灰已发布，站点置灰已隐藏，须脱敏而未脱敏同样置灰；底、图、字同一透明度，标记角标不置灰，不关闭命中测试。
    /// </summary>
    public double ChromeOpacity => NeedsChromeDim ? DimmedChromeOpacity : 1d;

    /// <summary>
    /// 是否套用共用置灰。
    /// </summary>
    public bool NeedsChromeDim =>
        CatalogNotice.NeedsChromeDim(Kind == CardKind.Site, IsHidden, IsPublished, IsUnsigned);

    public string? PreviewPath
    {
        get => mPreviewPath;
        private set => SetField(ref mPreviewPath, value);
    }

    public bool UsesWebPreview
    {
        get => mUsesWebPreview;
        private set => SetField(ref mUsesWebPreview, value);
    }

    /// <summary>
    /// 视频卡片双击走外部播放器，不进图像灯箱。
    /// </summary>
    public bool IsVideo =>
        MediaPathRules.IsVideoFile(Stage?.FullPath)
        || MediaPathRules.IsVideoFile(Site?.ObjectKey)
        || MediaPathRules.IsVideoFile(PreviewPath);

    /// <summary>
    /// 跨刷新识别同一张卡：投放箱用相对路径，站点用对象键。
    /// </summary>
    public string? CardKey
    {
        get
        {
            if (Kind == CardKind.Stage && !string.IsNullOrWhiteSpace(Stage?.StageRel))
            {
                return "stage:" + JsonUtil.ToRel(Stage.StageRel);
            }

            if (Kind == CardKind.Site && !string.IsNullOrWhiteSpace(Site?.ObjectKey))
            {
                return "site:" + JsonUtil.ToRel(Site.ObjectKey);
            }

            return null;
        }
    }

    public bool IsSelected
    {
        get => mIsSelected;
        set => SetField(ref mIsSelected, value);
    }

    public MediaIntent Mark
    {
        get => mMark;
        set
        {
            if (SetField(ref mMark, value))
            {
                Raise(nameof(MarkLabel));
                Raise(nameof(MarkBrush));
                Raise(nameof(HasMark));
            }
        }
    }

    public string MarkLabel => mMark switch
    {
        MediaIntent.StageIngest => "上页",
        MediaIntent.StageRecycle => "回收",
        MediaIntent.SiteHide => "隐藏",
        MediaIntent.SiteRestore => "上页",
        MediaIntent.SiteWithdraw => "撤下",
        _ => ""
    };

    public bool HasMark => mMark != MediaIntent.None;

    public int StarCount { get; private set; }

    /// <summary>
    /// 查看排序用的名称。投放箱为文件名，站点为名称段。
    /// </summary>
    public string SortName { get; private set; } = "";

    /// <summary>
    /// 拍摄时间是否已在换表前读入。未读入时比较视为缺时间，不在取值时打开文件。
    /// </summary>
    public bool IsSortTimeReady => mSortTimeReady;

    /// <summary>
    /// 查看排序用的时间。只返回已填字段。
    /// </summary>
    public DateTime? SortTimeUtc => mSortTimeUtc;

    /// <summary>
    /// 取拍摄时间用的路径。只返回已记下的字段，不打开文件。
    /// </summary>
    internal string? SortTimePath => mSortTimePath;

    /// <summary>
    /// 本次组装所用的填充结果。就地同步时整份拷走，不再读盘。
    /// </summary>
    internal CardFileFacts Facts => mFacts;

    /// <summary>
    /// 写入已在填充步骤读完的拍摄时间。已读过则不再改。
    /// </summary>
    internal void ApplyFilledSortTime(DateTime? utc)
    {
        if (mSortTimeReady)
        {
            return;
        }

        mSortTimeUtc = utc;
        mSortTimeReady = true;
        mFacts = mFacts with { SortTimeReady = true, SortTimeUtc = utc };
    }

    /// <summary>
    /// 写入已解析的星数，并记进填充结果。
    /// </summary>
    internal void ApplyStars(int count)
    {
        SetStars(count);
        mFacts = mFacts with { Stars = StarCount };
    }

    /// <summary>
    /// 查看排序用的字节数。站点正式位缺失时为 0。
    /// </summary>
    public long SortSize { get; private set; }

    public IReadOnlyList<int> StarDots => Enumerable.Range(0, StarCount).ToArray();

    /// <summary>
    /// 灯箱五位。下标小于星数为已选，其余为未选。
    /// </summary>
    public IReadOnlyList<bool> StarSlots { get; private set; } = new bool[5];

    /// <summary>
    /// 写入当前卡片的星数。网格只画已选圆点；灯箱始终留五个星位。
    /// </summary>
    public void SetStars(int count)
    {
        var next = count < 0 ? 0 : count;
        if (StarCount == next)
        {
            return;
        }

        StarCount = next;
        StarSlots = BuildStarSlots(next);
        Raise(nameof(StarCount));
        Raise(nameof(StarDots));
        Raise(nameof(StarSlots));
    }

    /// <summary>
    /// 生成灯箱五位。超出五颗的星数只点亮前五位。
    /// </summary>
    private static bool[] BuildStarSlots(int count)
    {
        var slots = new bool[5];
        var filled = count > 5 ? 5 : count;
        for (var i = 0; i < filled; i++)
        {
            slots[i] = true;
        }

        return slots;
    }

    public Brush MarkBrush => mMark switch
    {
        MediaIntent.StageIngest => BrushCache.MarkIngest,
        MediaIntent.StageRecycle => BrushCache.MarkRecycle,
        MediaIntent.SiteHide => BrushCache.MarkHide,
        MediaIntent.SiteRestore => BrushCache.MarkIngest,
        MediaIntent.SiteWithdraw => BrushCache.MarkWithdraw,
        _ => BrushCache.MarkNone
    };

    public ImageSource? Thumbnail
    {
        get => mThumbnail;
        set => SetField(ref mThumbnail, value);
    }

    /// <summary>
    /// 就地写入编目快照；预览路径或文件时间变了才丢掉已解码缩略图。
    /// </summary>
    public void ApplyCatalog(MediaCardViewModel incoming)
    {
        if (incoming.Kind != Kind)
        {
            return;
        }

        if (incoming.Kind == CardKind.Stage && incoming.Stage != null)
        {
            ApplyStage(incoming.Stage, incoming.Facts);
        }
        else if (incoming.Kind == CardKind.Site && incoming.Site != null)
        {
            ApplySite(incoming.Site, incoming.Facts);
        }
    }

    /// <summary>
    /// 按投放箱条目与已填事实刷新题名、徽章与预览。
    /// </summary>
    private void ApplyStage(StageItem stage, CardFileFacts facts)
    {
        var path = stage.FullPath;
        var keptTime = mSortTimeReady
            && string.Equals(mSortTimePath, path, StringComparison.OrdinalIgnoreCase);
        Stage = stage;
        Site = null;
        Caption = Path.GetFileName(stage.StageRel.TrimEnd('/', '\\'));
        SortName = Caption;
        BindSortTime(path);
        ApplyFilledTime(keptTime, facts);
        SortSize = facts.SortSize;
        Subtitle = stage.StageRel;
        AssignStickyNames(stage.StageRel);
        PublishLabel = PublishStatus.ForStage(stage);
        IsUnsigned = CatalogNotice.IsUnsigned(stage);
        Badge = IsUnsigned ? PublishStatus.Unsigned : PublishLabel;
        AssignPreview(facts.PreviewPath, facts.UsesWebPreview, facts.PreviewWriteUtc);
        SetStars(facts.Stars);
        RememberFacts();
        RaiseCatalogChrome();
    }

    /// <summary>
    /// 按站点条目与已填事实刷新题名、徽章与预览。
    /// </summary>
    private void ApplySite(SiteItem site, CardFileFacts facts)
    {
        var path = site.FullPath;
        var keptTime = mSortTimeReady
            && string.Equals(mSortTimePath, path, StringComparison.OrdinalIgnoreCase);
        Site = site;
        Stage = null;
        Caption = SiteCardCaption.Format(site);
        SortName = SiteCardCaption.Name(site);
        BindSortTime(path);
        ApplyFilledTime(keptTime, facts);
        SortSize = facts.SortSize;
        Subtitle = site.ObjectKey;
        AssignStickyNames(null);
        PublishLabel = PublishStatus.ForSite(site);
        IsUnsigned = CatalogNotice.IsUnsigned(site);
        Badge = IsUnsigned
            ? PublishStatus.Unsigned
            : site.IsHidden ? PublishStatus.Hidden : site.WorkTitle;
        AssignPreview(facts.PreviewPath, facts.UsesWebPreview, facts.PreviewWriteUtc);
        SetStars(facts.Stars);
        RememberFacts();
        RaiseCatalogChrome();
    }

    /// <summary>
    /// 路径未变且已有时间则保留；否则在填充结果已带时间时写入。
    /// </summary>
    private void ApplyFilledTime(bool keptTime, CardFileFacts facts)
    {
        if (keptTime || !facts.SortTimeReady)
        {
            return;
        }

        mSortTimeUtc = facts.SortTimeUtc;
        mSortTimeReady = true;
    }

    /// <summary>
    /// 记下取时间的路径。路径未变且已读过则保留缓存。
    /// </summary>
    private void BindSortTime(string? path)
    {
        if (mSortTimeReady && string.Equals(mSortTimePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        mSortTimePath = path;
        mSortTimeReady = false;
        mSortTimeUtc = null;
    }

    /// <summary>
    /// 写入已填的预览路径与修改时间；二者有变才清掉内存缩略图。
    /// </summary>
    private void AssignPreview(string? previewPath, bool usesWebPreview, DateTime writeUtc)
    {
        var changed = !string.Equals(PreviewPath, previewPath, StringComparison.OrdinalIgnoreCase)
            || (mPreviewWriteUtc != default && writeUtc != mPreviewWriteUtc);
        PreviewPath = previewPath;
        UsesWebPreview = usesWebPreview;
        mPreviewWriteUtc = writeUtc;
        if (changed)
        {
            Thumbnail = null;
        }
    }

    /// <summary>
    /// 把当前字段收成一份填充结果，供下次就地同步拷走。
    /// </summary>
    private void RememberFacts()
    {
        mFacts = new CardFileFacts
        {
            SortSize = SortSize,
            PreviewPath = PreviewPath,
            UsesWebPreview = UsesWebPreview,
            PreviewWriteUtc = mPreviewWriteUtc,
            SortTimeReady = mSortTimeReady,
            SortTimeUtc = mSortTimeUtc,
            Stars = StarCount
        };
    }

    /// <summary>
    /// 卡片标题与路径拆出可省略主体；超宽时扩展名钉在右侧。
    /// </summary>
    private void AssignStickyNames(string? filePath)
    {
        MediaPathRules.SplitStickyExtension(Caption, filePath, out var captionStem, out var captionExt);
        CaptionStem = captionStem;
        CaptionExtension = captionExt;
        MediaPathRules.SplitStickyExtension(Subtitle, filePath, out var subtitleStem, out var subtitleExt);
        SubtitleStem = subtitleStem;
        SubtitleExtension = subtitleExt;
    }

    /// <summary>
    /// 徽章、置灰与视频判定随编目一起通知。
    /// </summary>
    private void RaiseCatalogChrome()
    {
        Raise(nameof(IsPublished));
        Raise(nameof(IsHidden));
        Raise(nameof(PublishBrush));
        Raise(nameof(BadgeBrush));
        Raise(nameof(NeedsChromeDim));
        Raise(nameof(ChromeOpacity));
        Raise(nameof(IsVideo));
        Raise(nameof(CardKey));
    }
}

/// <summary>
/// 卡片来自投放箱或站点。
/// </summary>
public enum CardKind
{
    Stage,
    Site
}

/// <summary>
/// 冻结的界面色刷。
/// </summary>
public static class BrushCache
{
    public static readonly Brush Pine = Freeze("#2F5D4A");
    public static readonly Brush Gold = Freeze("#C4A574");
    public static readonly Brush Oxide = Freeze("#C45C3E");
    public static readonly Brush Line = Freeze("#3A4148");
    public static readonly Brush Text = Freeze("#E8E6E1");
    public static readonly Brush Muted = Freeze("#8A8680");
    public static readonly Brush MarkIngest = Freeze("#2F6B45");
    public static readonly Brush MarkRecycle = Freeze("#C43C32");
    public static readonly Brush MarkHide = Freeze("#5C6168");
    public static readonly Brush MarkWithdraw = Freeze("#D4893A");
    public static readonly Brush MarkNone = Freeze("#3A4148");

    /// <summary>
    /// 项目栏计数：比角标略提明度与饱和，不透明。
    /// </summary>
    public static readonly Brush NavMarkIngest = Freeze("#3D9A58");
    public static readonly Brush NavMarkRecycle = Freeze("#E04A3E");
    public static readonly Brush NavMarkHide = Freeze("#8C929A");
    public static readonly Brush NavMarkWithdraw = Freeze("#E8A046");
    public static readonly Brush MarkBadgeBorder = Freeze("#E8E6E1");
    public static readonly Brush PublishPublished = Freeze("#4A9FE0");
    public static readonly Brush PublishHidden = Freeze("#A8A39C");
    public static readonly Brush PublishWithdrawn = Freeze("#D4893A");
    public static readonly Brush PublishStock = Freeze("#4A6A82");
    public static readonly Brush PublishPaired = Freeze("#C4A574");
    public static readonly Brush PublishUnlisted = Freeze("#8A8680");
    public static readonly Brush PublishUnpublished = Freeze("#3A4148");

    /// <summary>
    /// 弹窗按钮：沿用浅色字，铺深灰底，避免系统浅底把字吃掉。
    /// </summary>
    public static void ApplyGhostButton(Button button)
    {
        if (Application.Current?.TryFindResource("GhostButton") is Style style)
        {
            button.Style = style;
        }

        button.Foreground = Text;
        button.Background = Freeze("#252A31");
    }

    /// <summary>
    /// 由十六进制生成可跨线程使用的笔刷。
    /// </summary>
    public static Brush Freeze(string hex)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
