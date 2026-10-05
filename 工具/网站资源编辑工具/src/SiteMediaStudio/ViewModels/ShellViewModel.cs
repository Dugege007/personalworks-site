using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalWorks.SiteMediaStudio.Controls;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 主窗口状态与命令。
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly ThumbnailService mThumbnails = new();
    private bool mToolExeSearchBusy;
    private readonly SurfaceGridCache<MediaCardViewModel, CompareRowViewModel> mGridCache = new();
    private readonly CardSelectionHistory mSelectionHistory = new();
    private string? mSelectionHistoryScopeKey;
    private ObservableCollection<MediaCardViewModel> mCards = new();
    private ObservableCollection<CompareRowViewModel> mCompareRows = new();
    private readonly AppSettings mSettings;
    private WorkspaceSession? mSession;
    private NavNodeViewModel? mSelectedNav;
    private NavNodeViewModel? mNavSelectAnchor;
    private MediaCardViewModel? mFocusCard;
    private string mPane = "stage";
    private string mStatusText = "正在加载工作区…";
    private string mWorkspaceTitle = "网站资源编辑工具";
    private string mChannelCaption = "";
    private string mInspectorMeta = "";
    private string mPreviewHint = "选择一张图查看详情。";
    private string mNoticeText = "";
    private ImageSource? mInspectorImage;
    private string? mIngestTargetWorkId;
    private string mCopyName = "";
    private string mCopyDescription = "";
    private bool mShowCopyName;
    private bool mShowCopyDescription;
    private bool mShowProjectTags;
    private IReadOnlyList<TagEditorItem> mProjectTagList = Array.Empty<TagEditorItem>();
    private string? mProjectTagKey;
    private string? mProjectTagWorkId;
    private bool mApplyingProjectTag;
    private string? mCopySubjectKey;
    private string mCopyPublishedTitle = "";
    private string mCopyPublishedDescription = "";
    private bool mShowResourceTags;
    private bool mShowResourceTagBatch;
    private bool mShowResourceTagHint;
    private bool mResourceTagEditable;
    private string mResourceTagCaption = "";
    private string mResourceTagHint = "";
    private IReadOnlyList<TagEditorItem> mResourceTagSuggestionList = Array.Empty<TagEditorItem>();
    private IReadOnlyList<TagEditorItem> mResourceTagSelectedList = Array.Empty<TagEditorItem>();
    private ExecutionMode mMode = ExecutionMode.Direct;
    private CancellationTokenSource? mThumbCts;
    private CancellationTokenSource? mInspectorCts;
    private DispatcherTimer? mThumbDebounce;
    private readonly VideoFrameGrabber mVideoGrabber = new();
    private DispatcherTimer? mVideoScrubDebounce;
    private CancellationTokenSource? mVideoScrubCts;
    private bool mShowVideoScrub;
    private bool mCanScrubVideo;
    private bool mIgnoreVideoPosition;
    private double mVideoPositionSec;
    private double mVideoDurationSec;
    private string mVideoPositionCaption = "";
    private int mThumbnailPx = 240;
    private int mLoadStart;
    private int mLoadCount = 24;
    private int mRebuildToken;
    private int mReloadToken;
    private int mSortFillToken;
    private bool mDeferCardFill;
    private Task mPendingCardFill = Task.CompletedTask;
    private readonly object mFillLock = new();
    private int mOffUiFillCount;
    private TaskCompletionSource<bool>? mOffUiIdle;
    private bool mIsCatalogLoading;
    private string? mDisplayedGridKey;
    private bool mIsLightboxOpen;
    private bool mLightboxFit = true;
    private bool mLightboxZoomApplying;
    private double mLightboxScale = LightboxView.DefaultScale;
    private double mLightboxPanX;
    private double mLightboxPanY;
    private double mLightboxViewportWidth;
    private double mLightboxViewportHeight;
    private double mLightboxDisplayWidth;
    private double mLightboxDisplayHeight;
    private double mLightboxMiniWidth;
    private double mLightboxMiniHeight;
    private double mLightboxMiniViewLeft;
    private double mLightboxMiniViewTop;
    private double mLightboxMiniViewWidth;
    private double mLightboxMiniViewHeight;
    private string mLightboxZoomLabel = "适应窗口";
    private LightboxZoomChoice? mSelectedLightboxZoom;
    private SenseFilterChoice mSelectedSenseFilter;
    private readonly GridSortState mGridSort;
    private bool mIsGridSortOpen;
    private bool mIsPublishing;
    private bool mIsExecuting;
    private FileSystemWatcher? mStageWatcher;
    private DispatcherTimer? mWatchDebounce;
    private bool mWatchReloadPending;
    private string? mNavProfilePath;
    private bool mCardListSyncing;
    private bool mNavExpandApplying;
    private DispatcherTimer? mNavExpandPersistDebounce;

    public ShellViewModel()
    {
        mSettings = AppSettingsStore.Load();
        ToolExeStore.FillKnown(mSettings);
        mGridSort = GridSortState.FromPreferences(mSettings.GridSort);
        mThumbnailPx = mSettings.ThumbnailPx > 0 ? Math.Clamp(mSettings.ThumbnailPx, 120, 480) : 240;
        if (string.Equals(mSettings.ExecutionMode, "prompt", StringComparison.OrdinalIgnoreCase))
        {
            mMode = ExecutionMode.Prompt;
        }

        OpenFixtureCommand = new RelayCommand(OpenFixture);
        OpenPersonalWorksCommand = new RelayCommand(OpenPersonalWorks);
        RefreshCommand = new RelayCommand(
            () => Reload(mSession?.Profile.ProfilePath, coverGrid: false),
            () => mSession != null);
        MarkIngestCommand = new RelayCommand(() => MarkSelected(MediaIntent.StageIngest), HasIngestSelection);
        MarkRecycleCommand = new RelayCommand(() => MarkSelected(MediaIntent.StageRecycle), HasStageSelection);
        MarkHideCommand = new RelayCommand(() => MarkSelected(MediaIntent.SiteHide), HasSiteSelection);
        MarkWithdrawCommand = new RelayCommand(() => MarkSelected(MediaIntent.SiteWithdraw), HasSiteSelection);
        ClearMarkCommand = new RelayCommand(ClearSelectedMarks, HasAnySelection);
        PreviewCommand = new RelayCommand(ShowPreview, HasRunnableBatch);
        CopyPromptCommand = new RelayCommand(CopyPrompt, HasRunnableBatch);
        ExecuteCommand = new RelayCommand(ExecuteDirect, CanExecuteDirect);
        PublishCommand = new RelayCommand(PublishOnline, CanPublishOnline);
        PaneStageCommand = new RelayCommand(() => Pane = "stage");
        PaneSiteCommand = new RelayCommand(() => Pane = "site");
        PaneCompareCommand = new RelayCommand(() => Pane = "compare");
        ModeDirectCommand = new RelayCommand(() => IsPromptMode = false);
        ModePromptCommand = new RelayCommand(() => IsPromptMode = true);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        RegisterWorkCommand = new RelayCommand(OpenRegisterWork, CanRegisterWork);
        EditWorkCommand = new RelayCommand(OpenEditWork, CanEditWork);
        TagCommand = new RelayCommand(OpenTags, CanTag);
        WithdrawWorkCommand = new RelayCommand(WithdrawSelectedWork, CanWithdrawWork);
        CreateNoteCommand = new RelayCommand(CreateNote, CanCreateNote);
        PublishNoteCommand = new RelayCommand(PublishSelectedNote, CanPublishNote);
        HideNoteCommand = new RelayCommand(HideSelectedNote, CanHideNote);
        RestoreNoteCommand = new RelayCommand(RestoreSelectedNote, CanRestoreNote);
        RecycleNoteCommand = new RelayCommand(RecycleSelectedNote, CanRecycleNote);
        RelocateCommand = new RelayCommand(OpenRelocate, CanRelocate);
        SetCoverCommand = new RelayCommand(SetVideoCover, CanSetVideoCover);
        RestoreSelectionCommand = new RelayCommand(RestorePreviousSelection, () => mSelectionHistory.CanRestore);
        SelectAllCommand = new RelayCommand(SelectAllVisibleCards, CanSelectAllVisibleCards);
        LightboxZoomOptionList = CreateLightboxZoomOptions();
        mSelectedLightboxZoom = LightboxZoomOptionList[0];
        SenseFilterOptionList = CreateSenseFilterOptions();
        mSelectedSenseFilter = SenseFilterOptionList[0];
        GridSortOptionList = CreateGridSortOptions();
        SyncGridSortOptions();
        AttachCardFilters();
    }

    public ObservableCollection<NavNodeViewModel> NavNodes { get; } = new();

    /// <summary>
    /// 项目栏节点就地同步后，供窗口按当前选中补一次树高亮。
    /// </summary>
    public event Action? NavTreeRestored;

    /// <summary>
    /// 网格按键恢复选区后，供窗口写回 ListBox 高亮。
    /// </summary>
    public event Action? CardSelectionRestored;

    /// <summary>
    /// 网格内容或顺序已变，滚动偏移往往不变。窗口应按当前视口补缩略图。
    /// </summary>
    public event Action? VisibleSurfaceChanged;

    /// <summary>
    /// 整表重建后滚到焦点；就地同步不滚，以免打乱当前视口。
    /// </summary>
    public bool ScrollRestoredCardSelection { get; private set; } = true;

    public ObservableCollection<MediaCardViewModel> Cards
    {
        get => mCards;
        private set => SetField(ref mCards, value);
    }

    public ObservableCollection<CompareRowViewModel> CompareRows
    {
        get => mCompareRows;
        private set => SetField(ref mCompareRows, value);
    }
    public ObservableCollection<WorkCatalogItem> IngestWorkList { get; } = new();
    public List<MediaCardViewModel> SelectedCardList { get; } = new();

    public RelayCommand OpenFixtureCommand { get; }
    public RelayCommand OpenPersonalWorksCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand MarkIngestCommand { get; }
    public RelayCommand MarkRecycleCommand { get; }
    public RelayCommand MarkHideCommand { get; }
    public RelayCommand MarkWithdrawCommand { get; }
    public RelayCommand ClearMarkCommand { get; }
    public RelayCommand PreviewCommand { get; }
    public RelayCommand CopyPromptCommand { get; }
    public RelayCommand ExecuteCommand { get; }
    public RelayCommand PublishCommand { get; }
    public RelayCommand PaneStageCommand { get; }
    public RelayCommand PaneSiteCommand { get; }
    public RelayCommand PaneCompareCommand { get; }
    public RelayCommand ModeDirectCommand { get; }
    public RelayCommand ModePromptCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand RegisterWorkCommand { get; }
    public RelayCommand EditWorkCommand { get; }

    /// <summary>
    /// 给当前摄影作品或选中的多张写类型与标签。
    /// </summary>
    public RelayCommand TagCommand { get; }

    /// <summary>
    /// 整项撤下当前已入编作品。
    /// </summary>
    public RelayCommand WithdrawWorkCommand { get; }
    public RelayCommand CreateNoteCommand { get; }
    public RelayCommand PublishNoteCommand { get; }
    public RelayCommand HideNoteCommand { get; }
    public RelayCommand RestoreNoteCommand { get; }
    public RelayCommand RecycleNoteCommand { get; }
    public RelayCommand RelocateCommand { get; }
    public RelayCommand SetCoverCommand { get; }
    public RelayCommand RestoreSelectionCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public IReadOnlyList<LightboxZoomChoice> LightboxZoomOptionList { get; }
    public IReadOnlyList<SenseFilterChoice> SenseFilterOptionList { get; }

    public IReadOnlyList<GridSortOptionViewModel> GridSortOptionList { get; }

    public string WorkspaceTitle
    {
        get => mWorkspaceTitle;
        private set => SetField(ref mWorkspaceTitle, value);
    }

    public string ChannelCaption
    {
        get => mChannelCaption;
        private set => SetField(ref mChannelCaption, value);
    }

    public string StatusText
    {
        get => mStatusText;
        private set => SetField(ref mStatusText, value);
    }

    public string InspectorMeta
    {
        get => mInspectorMeta;
        private set => SetField(ref mInspectorMeta, value);
    }

    public string PreviewHint
    {
        get => mPreviewHint;
        private set => SetField(ref mPreviewHint, value);
    }

    public string NoticeText
    {
        get => mNoticeText;
        private set => SetField(ref mNoticeText, value);
    }

    public bool HasNotice => !string.IsNullOrWhiteSpace(mNoticeText);

    public string Pane
    {
        get => mPane;
        set
        {
            if (mPane == value)
            {
                return;
            }

            ParkDisplayedGrid();
            if (SetField(ref mPane, value))
            {
                Raise(nameof(IsStagePane));
                Raise(nameof(IsSitePane));
                Raise(nameof(IsComparePane));
                Raise(nameof(CanReorderSite));
                Raise(nameof(PaneHintText));
                Raise(nameof(IsGridSortEnabled));
                if (IsComparePane)
                {
                    IsGridSortOpen = false;
                }

                Raise(nameof(ShowRelocate));
                RelocateCommand.RaiseCanExecute();
                ResetSelectionHistoryForScope();
                ScheduleRebuildCards();
            }
        }
    }

    public bool IsCatalogLoading
    {
        get => mIsCatalogLoading;
        private set => SetField(ref mIsCatalogLoading, value);
    }

    public bool IsStagePane => mPane == "stage";
    public bool IsSitePane => mPane == "site";
    public bool IsComparePane => mPane == "compare";

    /// <summary>
    /// 站点页且树选已入编作品、查看序已清空时允许拖卡片改序。
    /// </summary>
    public bool CanReorderSite =>
        IsSitePane
        && mSelectedNav?.Work != null
        && !mSelectedNav.IsUnregistered
        && !mGridSort.HasActive;

    /// <summary>
    /// 主台快捷键提示；站点可拖时补一句。
    /// </summary>
    public string PaneHintText =>
        CanReorderSite
            ? "空格开灯箱 · ← → 翻页 · Ctrl 多选 · 拖卡片排序 · I 上页 / R 回收 / H 隐藏 / W 撤下"
            : "空格开灯箱 · ← → 翻页 · Ctrl 多选 · I 上页 / R 回收 / H 隐藏 / W 撤下";

    public bool IsPromptMode
    {
        get => mMode == ExecutionMode.Prompt;
        set
        {
            var mode = value ? ExecutionMode.Prompt : ExecutionMode.Direct;
            if (mMode == mode)
            {
                return;
            }

            mMode = mode;
            mSettings.ExecutionMode = mode == ExecutionMode.Direct ? "direct" : "prompt";
            AppSettingsStore.Save(mSettings);
            Raise(nameof(IsPromptMode));
            Raise(nameof(ModeCaption));
            ExecuteCommand.RaiseCanExecute();
        }
    }

    public string ModeCaption => IsPromptMode ? "提示词" : "机械";

    /// <summary>
    /// 须脱敏栏目可按未脱敏 / 已脱敏收窄网格；其它栏目禁用并视为全部。
    /// </summary>
    public SenseFilterChoice SelectedSenseFilter
    {
        get => mSelectedSenseFilter;
        set
        {
            if (value == null || !SetField(ref mSelectedSenseFilter, value))
            {
                return;
            }

            RefreshSenseFilter();
        }
    }

    public bool IsSenseFilterEnabled => CatalogNotice.RequiresDesense(mSelectedNav?.Channel.Key);

    /// <summary>
    /// 上页写入的目标作品；空则预演硬错误。
    /// </summary>
    public string? IngestTargetWorkId
    {
        get => mIngestTargetWorkId;
        set
        {
            if (SetField(ref mIngestTargetWorkId, value))
            {
                Raise(nameof(ShowIngestTarget));
            }
        }
    }

    /// <summary>
    /// 现实摄影、游戏摄影、AI摄影已选中作品时，上页目标就是该夹，不再出下拉。
    /// 一级栏目与年份行也不出下拉。其它栏目的项目仍可手选写入作品。
    /// </summary>
    public bool ShowIngestTarget =>
        IngestWorkList.Count > 0
        && !IsSelectedPhotoFolderWork()
        && !IsSelectedChannelRoot()
        && mSelectedNav is not { IsYearGroup: true }
        && mSelectedNav?.Work?.SourceKind != "note";

    /// <summary>
    /// 单条作品资源的可选显示名。
    /// </summary>
    public string CopyName
    {
        get => mCopyName;
        set => SetField(ref mCopyName, value ?? "");
    }

    /// <summary>
    /// 当前主体的描述。
    /// </summary>
    public string CopyDescription
    {
        get => mCopyDescription;
        set => SetField(ref mCopyDescription, value ?? "");
    }

    /// <summary>
    /// 是否显示资源名称栏。
    /// </summary>
    public bool ShowCopyName
    {
        get => mShowCopyName;
        private set => SetField(ref mShowCopyName, value);
    }

    /// <summary>
    /// 是否显示描述栏。
    /// </summary>
    public bool ShowCopyDescription
    {
        get => mShowCopyDescription;
        private set => SetField(ref mShowCopyDescription, value);
    }

    /// <summary>
    /// 左侧选中已入编摄影项目时，在描述上方列出该项目已用标签。
    /// </summary>
    public bool ShowProjectTags
    {
        get => mShowProjectTags;
        private set => SetField(ref mShowProjectTags, value);
    }

    /// <summary>
    /// 当前项目已用标签。次数是该项目里带着该词的资源数。
    /// </summary>
    public IReadOnlyList<TagEditorItem> ProjectTagList
    {
        get => mProjectTagList;
        private set => SetField(ref mProjectTagList, value);
    }

    /// <summary>
    /// 选中摄影作品里的资源后，在名称上方显示标签组件。
    /// </summary>
    public bool ShowResourceTags
    {
        get => mShowResourceTags;
        private set => SetField(ref mShowResourceTags, value);
    }

    /// <summary>
    /// 多张一起改时显示张数。
    /// </summary>
    public bool ShowResourceTagBatch
    {
        get => mShowResourceTagBatch;
        private set => SetField(ref mShowResourceTagBatch, value);
    }

    /// <summary>
    /// 已上页才允许改标签。
    /// </summary>
    public bool ResourceTagEditable
    {
        get => mResourceTagEditable;
        private set => SetField(ref mResourceTagEditable, value);
    }

    /// <summary>
    /// 当前栏目的类型与自由标签预设。
    /// </summary>
    public IReadOnlyList<TagEditorItem> ResourceTagSuggestions
    {
        get => mResourceTagSuggestionList;
        private set => SetField(ref mResourceTagSuggestionList, value);
    }

    /// <summary>
    /// 当前选中资源都带着的标签。
    /// </summary>
    public IReadOnlyList<TagEditorItem> ResourceTagSelected
    {
        get => mResourceTagSelectedList;
        private set => SetField(ref mResourceTagSelectedList, value);
    }

    /// <summary>
    /// 多选时的张数说明。
    /// </summary>
    public string ResourceTagCaption
    {
        get => mResourceTagCaption;
        private set => SetField(ref mResourceTagCaption, value);
    }

    /// <summary>
    /// 还没上页时说明写入时机。
    /// </summary>
    public bool ShowResourceTagHint
    {
        get => mShowResourceTagHint;
        private set => SetField(ref mShowResourceTagHint, value);
    }

    /// <summary>
    /// 还没上页时的说明。
    /// </summary>
    public string ResourceTagHint
    {
        get => mResourceTagHint;
        private set => SetField(ref mResourceTagHint, value);
    }

    public bool ShowRegisterWork => CanRegisterWork();

    public bool ShowEditWork => CanEditWork();

    public bool ShowWithdrawWork => CanWithdrawWork();

    public bool ShowNoteCreate => CanCreateNote();

    public bool ShowNoteActions => CanPublishNote();

    public bool ShowRelocate => CanRelocate();

    public NavNodeViewModel? SelectedNav
    {
        get => mSelectedNav;
        set
        {
            if (ReferenceEquals(mSelectedNav, value))
            {
                return;
            }

            ParkDisplayedGrid();
            if (SetField(ref mSelectedNav, value))
            {
                mIngestTargetWorkId = value?.Work is { IsUnregistered: false } work
                    ? work.Id
                    : null;
                RefreshIngestWorks();
                RefreshCopyEditor();
                ChannelCaption = FormatChannelCaption(value);
                Raise(nameof(IsSenseFilterEnabled));
                Raise(nameof(ShowRegisterWork));
                Raise(nameof(ShowEditWork));
                Raise(nameof(ShowWithdrawWork));
                Raise(nameof(ShowNoteCreate));
                Raise(nameof(ShowNoteActions));
                RegisterWorkCommand.RaiseCanExecute();
                EditWorkCommand.RaiseCanExecute();
                TagCommand.RaiseCanExecute();
                WithdrawWorkCommand.RaiseCanExecute();
                CreateNoteCommand.RaiseCanExecute();
                PublishNoteCommand.RaiseCanExecute();
                HideNoteCommand.RaiseCanExecute();
                RestoreNoteCommand.RaiseCanExecute();
                RecycleNoteCommand.RaiseCanExecute();
                Raise(nameof(ShowRelocate));
                Raise(nameof(CanReorderSite));
                Raise(nameof(PaneHintText));
                RelocateCommand.RaiseCanExecute();
                RememberLastNav();
                ResetSelectionHistoryForScope();
                ScheduleRebuildCards();
            }
        }
    }

    public MediaCardViewModel? FocusCard
    {
        get => mFocusCard;
        set
        {
            if (SetField(ref mFocusCard, value))
            {
                UpdateInspector();
                RefreshIngestWorks();
                RefreshCopyEditor();
                RaiseLightboxChrome();
            }
        }
    }

    /// <summary>
    /// 检视栏清晰预览；与网格缩略图分离。
    /// </summary>
    public ImageSource? InspectorImage
    {
        get => mInspectorImage;
        private set
        {
            if (SetField(ref mInspectorImage, value))
            {
                RefreshLightboxLayout();
                SetCoverCommand.RaiseCanExecute();
            }
        }
    }

    /// <summary>
    /// 视频焦点才显示进度条与设为封面。
    /// </summary>
    public bool ShowVideoScrub
    {
        get => mShowVideoScrub;
        private set => SetField(ref mShowVideoScrub, value);
    }

    /// <summary>
    /// 能解码逐帧时才允许拖进度条。
    /// </summary>
    public bool CanScrubVideo
    {
        get => mCanScrubVideo;
        private set
        {
            if (SetField(ref mCanScrubVideo, value))
            {
                SetCoverCommand.RaiseCanExecute();
            }
        }
    }

    public double VideoDurationSec
    {
        get => mVideoDurationSec;
        private set
        {
            if (SetField(ref mVideoDurationSec, value))
            {
                Raise(nameof(VideoSliderMaximum));
            }
        }
    }

    /// <summary>
    /// 进度条最大值；时长未知时避免 0。
    /// </summary>
    public double VideoSliderMaximum => Math.Max(mVideoDurationSec, 0.001);

    public double VideoPositionSec
    {
        get => mVideoPositionSec;
        set
        {
            if (SetField(ref mVideoPositionSec, value))
            {
                VideoPositionCaption = FormatVideoPosition(value, mVideoDurationSec);
                if (!mIgnoreVideoPosition)
                {
                    QueueVideoScrub();
                }
            }
        }
    }

    public string VideoPositionCaption
    {
        get => mVideoPositionCaption;
        private set => SetField(ref mVideoPositionCaption, value);
    }

    public string MarkSummary
    {
        get
        {
            var tally = ReadMarkTally();
            return $"已标 {tally.Total}  ·  上页 {tally.Ingest}  ·  回收 {tally.Recycle}  ·  隐藏 {tally.Hide}  ·  撤下 {tally.Withdraw}";
        }
    }

    /// <summary>
    /// 网格预览边长；同时作为卡片宽度。
    /// </summary>
    public int ThumbnailPx
    {
        get => mThumbnailPx;
        set
        {
            var clamped = (int)(Math.Round(Math.Clamp(value, 120, 480) / 20.0) * 20);
            if (!SetField(ref mThumbnailPx, clamped))
            {
                return;
            }

            mSettings.ThumbnailPx = clamped;
            AppSettingsStore.Save(mSettings);
            Raise(nameof(CardWidth));
            Raise(nameof(CardImageHeight));
            Raise(nameof(CardCellWidth));
            Raise(nameof(CardCellHeight));
            Raise(nameof(ThumbnailPxCaption));
            ScheduleThumbnailReload();
        }
    }

    public double CardWidth => mThumbnailPx;

    public double CardImageHeight => Math.Round(mThumbnailPx * 132.0 / 220.0);

    public double CardCellWidth => CardWidth + 12;

    public double CardCellHeight => CardImageHeight + 64 + 12;

    public string ThumbnailPxCaption => mThumbnailPx + " px";

    /// <summary>
    /// 窗口内灯箱是否打开。
    /// </summary>
    public bool IsLightboxOpen
    {
        get => mIsLightboxOpen;
        private set => SetField(ref mIsLightboxOpen, value);
    }

    /// <summary>
    /// 灯箱题名：当前张 / 本栏总数 · 卡片标题。
    /// </summary>
    public string LightboxCaption
    {
        get
        {
            var visibleList = VisibleCardList();
            if (mFocusCard == null || visibleList.Count == 0)
            {
                return "";
            }

            var index = visibleList.IndexOf(mFocusCard);
            var position = index < 0 ? 0 : index + 1;
            return $"{position} / {visibleList.Count}  ·  {mFocusCard.Caption}";
        }
    }

    public string LightboxMarkLabel => string.IsNullOrEmpty(mFocusCard?.MarkLabel)
        ? "未标记"
        : mFocusCard!.MarkLabel;

    public Brush LightboxMarkBrush => mFocusCard?.MarkBrush ?? BrushCache.MarkNone;

    public string LightboxPublishLabel =>
        mFocusCard?.IsUnsigned == true
            ? PublishStatus.Unsigned
            : mFocusCard?.PublishLabel ?? PublishStatus.Unpublished;

    public Brush LightboxPublishBrush =>
        mFocusCard?.IsUnsigned == true
            ? BrushCache.Gold
            : mFocusCard?.PublishBrush ?? BrushCache.PublishUnpublished;

    public double LightboxDisplayWidth => mLightboxDisplayWidth;

    public double LightboxDisplayHeight => mLightboxDisplayHeight;

    public double LightboxImageLeft => (mLightboxViewportWidth - mLightboxDisplayWidth) / 2 + mLightboxPanX;

    public double LightboxImageTop => (mLightboxViewportHeight - mLightboxDisplayHeight) / 2 + mLightboxPanY;

    public bool CanPanLightbox { get; private set; }

    public string LightboxZoomLabel
    {
        get => mLightboxZoomLabel;
        private set => SetField(ref mLightboxZoomLabel, value);
    }

    public LightboxZoomChoice? SelectedLightboxZoom
    {
        get => mSelectedLightboxZoom;
        set
        {
            if (!SetField(ref mSelectedLightboxZoom, value) || value == null || mLightboxZoomApplying)
            {
                return;
            }

            if (value.IsFit)
            {
                ApplyLightboxFit();
            }
            else
            {
                ApplyLightboxAbsolute(value.Scale);
            }
        }
    }

    public bool ShowLightboxMinimap =>
        LightboxView.ShouldShowMinimap(mIsLightboxOpen, mLightboxFit, mLightboxMiniWidth);

    public double LightboxMiniWidth => mLightboxMiniWidth;

    public double LightboxMiniHeight => mLightboxMiniHeight;

    public double LightboxMiniViewWidth => mLightboxMiniViewWidth;

    public double LightboxMiniViewHeight => mLightboxMiniViewHeight;

    public Thickness LightboxMiniViewMargin => new(mLightboxMiniViewLeft, mLightboxMiniViewTop, 0, 0);

    /// <summary>
    /// 启动时加载上次工作区，否则加载模拟站。
    /// </summary>
    public void Start(string? profileFromArgs)
    {
        var path = profileFromArgs;
        if (string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(mSettings.LastProfilePath)
            && File.Exists(mSettings.LastProfilePath))
        {
            path = mSettings.LastProfilePath;
        }

        path ??= ToolPaths.FindFixtureProfile();
        if (path == null)
        {
            StatusText = "找不到模拟站配置。";
            return;
        }

        Reload(path);
    }

    /// <summary>
    /// 打开指定 profile.json。
    /// </summary>
    public void OpenProfile(string profilePath)
    {
        Reload(profilePath);
    }

    /// <summary>
    /// 对照行点选一侧卡片。Ctrl 可叠加选区；一侧投放箱、一侧站点时即使不按 Ctrl 也保留两端，便于重挂。
    /// </summary>
    public void FocusCompareCard(MediaCardViewModel? card, bool addToSelection = false)
    {
        if (card == null)
        {
            return;
        }

        var previous = CaptureSelectionSnapshot();
        if (!addToSelection)
        {
            var keepOpposite = SelectedCardList.Count == 1
                && SelectedCardList[0].Kind != card.Kind;
            if (!keepOpposite)
            {
                SelectedCardList.Clear();
            }
        }

        if (SelectedCardList.Contains(card))
        {
            if (addToSelection)
            {
                SelectedCardList.Remove(card);
            }
        }
        else
        {
            SelectedCardList.Add(card);
        }

        foreach (var item in Cards)
        {
            item.IsSelected = SelectedCardList.Contains(item);
        }

        FocusCard = card;
        RememberUserSelection(previous);
        RaiseMarkCommands();
    }

    /// <summary>
    /// 有焦点图且盘上文件存在时打开灯箱；缩放回到适应窗口。
    /// </summary>
    public void OpenLightbox()
    {
        if (mFocusCard?.IsVideo == true)
        {
            OpenExternalPreview();
            return;
        }

        var path = mFocusCard?.PreviewPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        IsLightboxOpen = true;
        ResetLightboxView();
        RaiseLightboxChrome();
    }

    /// <summary>
    /// 用 PotPlayer 或系统默认播放器打开视频源文件。
    /// </summary>
    public void OpenExternalPreview()
    {
        var source = MediaPreviewRules.SourcePath(mFocusCard?.Stage?.FullPath, mFocusCard?.PreviewPath);
        if (string.IsNullOrWhiteSpace(source))
        {
            StatusText = "找不到可播放的视频文件。";
            return;
        }

        var player = ToolExeStore.ResolvePotPlayer(mSettings, searchDisk: false);
        if (string.IsNullOrWhiteSpace(player))
        {
            BeginToolExeSearch(
                "PotPlayer",
                () => ToolExeStore.ResolvePotPlayer(mSettings, searchDisk: true),
                found =>
                {
                    if (ExternalPlayer.TryOpen(source, found))
                    {
                        StatusText = "已找到 PotPlayer 并写入设置，正在打开视频。";
                    }
                    else
                    {
                        StatusText = "已写入 PotPlayer 路径，但无法打开视频。";
                    }
                });
            return;
        }

        if (ExternalPlayer.TryOpen(source, player))
        {
            StatusText = "已用外部播放器打开视频。";
            return;
        }

        StatusText = "无法打开播放器。";
    }

    /// <summary>
    /// 关闭灯箱并复位缩放与平移。
    /// </summary>
    public void CloseLightbox()
    {
        if (!mIsLightboxOpen)
        {
            return;
        }

        IsLightboxOpen = false;
        ResetLightboxView();
        Raise(nameof(ShowLightboxMinimap));
    }

    /// <summary>
    /// 空格与双击：图像开关灯箱；视频走外部播放器。
    /// </summary>
    public void ToggleLightbox()
    {
        if (mFocusCard?.IsVideo == true)
        {
            if (mIsLightboxOpen)
            {
                CloseLightbox();
            }

            OpenExternalPreview();
            return;
        }

        if (mIsLightboxOpen)
        {
            CloseLightbox();
            return;
        }

        OpenLightbox();
    }

    /// <summary>
    /// 按线性下标移动焦点；灯箱打开时翻页并复位缩放。首尾不绕圈。
    /// </summary>
    public bool TryStepFocus(int delta)
    {
        var visibleList = VisibleCardList();
        var current = mFocusCard == null ? -1 : visibleList.IndexOf(mFocusCard);
        var next = VirtualGridLayout.StepIndex(current, delta, visibleList.Count);
        if (next < 0 || next == current)
        {
            return false;
        }

        var card = visibleList[next];
        var previous = CaptureSelectionSnapshot();
        SelectedCardList.Clear();
        SelectedCardList.Add(card);
        foreach (var item in Cards)
        {
            item.IsSelected = item == card;
        }

        FocusCard = card;
        RememberUserSelection(previous);
        if (mIsLightboxOpen)
        {
            ResetLightboxView();
        }

        RaiseMarkCommands();
        return true;
    }

    /// <summary>
    /// 写入灯箱视口尺寸后重算显示与小地图。
    /// </summary>
    public void ReportLightboxViewport(double width, double height)
    {
        mLightboxViewportWidth = width;
        mLightboxViewportHeight = height;
        RefreshLightboxLayout();
    }

    /// <summary>
    /// 灯箱滚轮改为绝对缩放，并以指针位置为锚点平移。
    /// </summary>
    public void ZoomLightbox(int wheelDelta, double cursorX, double cursorY)
    {
        if (!mIsLightboxOpen)
        {
            return;
        }

        var oldScale = mLightboxFit ? CurrentContainRatio() : mLightboxScale;
        var newScale = LightboxView.ApplyWheel(oldScale, wheelDelta);
        var bitmap = mInspectorImage as BitmapSource;
        var display = LightboxView.DisplaySize(bitmap?.PixelWidth ?? 0, bitmap?.PixelHeight ?? 0, newScale);
        var pan = LightboxView.ZoomAtCursor(
            mLightboxPanX,
            mLightboxPanY,
            oldScale,
            newScale,
            cursorX,
            cursorY,
            mLightboxViewportWidth,
            mLightboxViewportHeight,
            display.Width,
            display.Height);
        mLightboxFit = false;
        mLightboxScale = newScale;
        mLightboxPanX = pan.X;
        mLightboxPanY = pan.Y;
        SyncSelectedZoomToScale();
        RefreshLightboxLayout();
    }

    /// <summary>
    /// 按指针位移拖移；图小于视口时夹回居中。
    /// </summary>
    public void PanLightbox(double deltaX, double deltaY)
    {
        if (!mIsLightboxOpen || !CanPanLightbox)
        {
            return;
        }

        mLightboxPanX += deltaX;
        mLightboxPanY += deltaY;
        RefreshLightboxLayout();
    }

    /// <summary>
    /// 小地图点击：把对应位置移到视口中心。
    /// </summary>
    public void PanLightboxToMinimap(double miniX, double miniY)
    {
        if (!mIsLightboxOpen || mLightboxMiniWidth <= 0)
        {
            return;
        }

        var pan = LightboxView.PanToMinimap(
            miniX,
            miniY,
            mLightboxMiniWidth,
            mLightboxMiniHeight,
            mLightboxDisplayWidth,
            mLightboxDisplayHeight,
            mLightboxViewportWidth,
            mLightboxViewportHeight);
        mLightboxPanX = pan.X;
        mLightboxPanY = pan.Y;
        RefreshLightboxLayout();
    }

    /// <summary>
    /// 同步 ListBox 多选到命令可用状态。
    /// </summary>
    public void SyncSelection(IEnumerable<MediaCardViewModel> selectedItems, bool recordHistory = true)
    {
        if (mCardListSyncing)
        {
            return;
        }

        if (!mApplyingProjectTag)
        {
            mProjectTagKey = null;
        }

        var previous = CaptureSelectionSnapshot();
        SelectedCardList.Clear();
        SelectedCardList.AddRange(selectedItems);
        foreach (var card in Cards)
        {
            card.IsSelected = SelectedCardList.Contains(card);
        }

        if (SelectedCardList.Count == 1)
        {
            FocusCard = SelectedCardList[0];
        }
        else if (SelectedCardList.Count == 0)
        {
            FocusCard = null;
        }

        if (recordHistory)
        {
            RememberUserSelection(previous);
        }

        RaiseMarkCommands();
        RefreshIngestWorks();
        RefreshCopyEditor();
    }

    /// <summary>
    /// 记下当前选区，供框选结束时一次写入历史。
    /// </summary>
    public CardSelectionSnapshot SnapshotSelection()
    {
        return CaptureSelectionSnapshot();
    }

    /// <summary>
    /// 把框选开始时的选区压入历史。
    /// </summary>
    public void RememberSelection(CardSelectionSnapshot previous)
    {
        RememberUserSelection(previous);
    }

    /// <summary>
    /// 当前脱敏筛下的可见卡片全部选中。
    /// </summary>
    public void SelectAllVisibleCards()
    {
        var cardList = CollectSelectableCards();
        if (cardList.Count == 0)
        {
            return;
        }

        SyncSelection(cardList);
        ScrollRestoredCardSelection = false;
        CardSelectionRestored?.Invoke();
    }

    /// <summary>
    /// 有可见卡片时才允许全选。
    /// </summary>
    private bool CanSelectAllVisibleCards()
    {
        return CollectSelectableCards().Count > 0;
    }

    /// <summary>
    /// 投放箱 / 站点取可见格；对照取可见行两侧非空卡片。
    /// </summary>
    private List<MediaCardViewModel> CollectSelectableCards()
    {
        if (IsComparePane)
        {
            var cardList = new List<MediaCardViewModel>();
            foreach (var row in VisibleCompareRowList())
            {
                if (row.Left != null && !cardList.Contains(row.Left))
                {
                    cardList.Add(row.Left);
                }

                if (row.Right != null && !cardList.Contains(row.Right))
                {
                    cardList.Add(row.Right);
                }
            }

            return cardList;
        }

        return VisibleCardList();
    }

    /// <summary>
    /// 取消当前选区，并把非空选区记入历史。
    /// </summary>
    public void ClearCardSelection()
    {
        if (SelectedCardList.Count == 0 && FocusCard == null)
        {
            return;
        }

        var previous = CaptureSelectionSnapshot();
        SelectedCardList.Clear();
        foreach (var card in Cards)
        {
            card.IsSelected = false;
        }

        FocusCard = null;
        RememberUserSelection(previous);
        RaiseMarkCommands();
        RefreshIngestWorks();
        RefreshCopyEditor();
    }

    /// <summary>
    /// 回退到最近一条已记录选区，含多选。
    /// </summary>
    public void RestorePreviousSelection()
    {
        var snapshot = mSelectionHistory.Restore();
        RestoreSelectionCommand.RaiseCanExecute();
        if (snapshot == null)
        {
            return;
        }

        mCardListSyncing = true;
        try
        {
            RestoreCardSelection(snapshot.KeyList, snapshot.FocusKey);
        }
        finally
        {
            mCardListSyncing = false;
        }

        ScrollRestoredCardSelection = true;
        CardSelectionRestored?.Invoke();
        RaiseMarkCommands();
        RefreshIngestWorks();
        RefreshCopyEditor();
    }

    /// <summary>
    /// 对当前选区写入意图；再按一次同键则清除。
    /// </summary>
    public void MarkSelected(MediaIntent intent)
    {
        var targetList = SelectedCardList.Count > 0
            ? SelectedCardList.ToList()
            : FocusCard == null ? new List<MediaCardViewModel>() : new List<MediaCardViewModel> { FocusCard };
        var skipped = 0;
        var alreadyHidden = 0;
        var alreadyOnPage = 0;
        var publishedLocked = 0;
        foreach (var card in targetList)
        {
            var resolved = ResolveMarkIntent(intent, card);
            if (resolved == MediaIntent.None)
            {
                skipped++;
                continue;
            }

            if (IsPublishedStageLocked(card, resolved))
            {
                publishedLocked++;
                continue;
            }

            var settingHide = resolved == MediaIntent.SiteHide && card.Mark != MediaIntent.SiteHide;
            if (settingHide && card.IsHidden)
            {
                alreadyHidden++;
            }

            var settingOnPage = resolved == MediaIntent.SiteRestore && !card.IsHidden;
            if (settingOnPage && card.Mark != resolved)
            {
                alreadyOnPage++;
            }

            card.Mark = card.Mark == resolved ? MediaIntent.None : resolved;
        }

        PersistCardMarks(targetList);
        StatusText = BuildMarkStatus(skipped, alreadyHidden, alreadyOnPage, publishedLocked, intent);
        Raise(nameof(MarkSummary));
        RaiseMarkCommands();
        RaiseLightboxChrome();
    }

    /// <summary>
    /// 投放箱上页、站点恢复显示都走上页键。投放箱已上页不可再标上页 / 回收。
    /// </summary>
    private static MediaIntent ResolveMarkIntent(MediaIntent intent, MediaCardViewModel card)
    {
        if (card.Kind == CardKind.Stage)
        {
            return intent is MediaIntent.StageIngest or MediaIntent.StageRecycle ? intent : MediaIntent.None;
        }

        if (intent == MediaIntent.StageIngest)
        {
            return MediaIntent.SiteRestore;
        }

        return intent is MediaIntent.SiteHide or MediaIntent.SiteWithdraw ? intent : MediaIntent.None;
    }

    /// <summary>
    /// 投放箱媒体台账已发布时不再接受上页或回收。心得索引只改角标，不锁。
    /// </summary>
    private static bool IsPublishedStageLocked(MediaCardViewModel card, MediaIntent resolved)
    {
        return card.Kind == CardKind.Stage
            && card.IsPublished
            && resolved is MediaIntent.StageIngest or MediaIntent.StageRecycle;
    }

    /// <summary>
    /// 标记后的底栏：视图不符、已上页锁定、已隐藏再隐藏、站点已在页上分开说。
    /// </summary>
    private static string BuildMarkStatus(
        int skipped,
        int alreadyHidden,
        int alreadyOnPage,
        int publishedLocked,
        MediaIntent requested)
    {
        var partList = new List<string>();
        if (skipped > 0)
        {
            partList.Add($"已跳过 {skipped} 张（视图与意图不符）");
        }

        if (publishedLocked > 0)
        {
            var action = requested == MediaIntent.StageRecycle ? "回收" : "上页";
            partList.Add($"{publishedLocked} 张已上页，不能再标{action}");
        }

        if (alreadyHidden > 0)
        {
            partList.Add($"{alreadyHidden} 张已隐藏，无需再隐藏，执行时将跳过");
        }

        if (alreadyOnPage > 0)
        {
            partList.Add($"{alreadyOnPage} 张已在页上，无需再上页，执行时将跳过");
        }

        return partList.Count == 0 ? "已更新标记。" : string.Join("；", partList) + "。";
    }

    /// <summary>
    /// 加载模拟站。
    /// </summary>
    private void OpenFixture()
    {
        var path = ToolPaths.FindFixtureProfile();
        if (path == null)
        {
            StatusText = "找不到模拟站。";
            return;
        }

        Reload(path);
    }

    /// <summary>
    /// 加载本仓库预置配置。
    /// </summary>
    private void OpenPersonalWorks()
    {
        var path = ToolPaths.FindPersonalWorksProfile();
        if (path == null)
        {
            StatusText = "找不到 PersonalWorks 预置配置。";
            return;
        }

        Reload(path);
    }

    /// <summary>
    /// 重新编目并刷新树与网格。扫描在界面线程之外。
    /// </summary>
    private void Reload(string? profilePath, bool coverGrid = true)
    {
        _ = ReloadAsync(profilePath, coverGrid);
    }

    /// <summary>
    /// 重新编目。会话返回前主窗保持可点；coverGrid 为真时网格区盖与切栏目相同的罩。
    /// 刷新编目与投放箱监视不盖罩。返回假表示本次已被更新的加载取代或失败。
    /// </summary>
    private async Task<bool> ReloadAsync(string? profilePath, bool coverGrid = true)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return false;
        }

        var token = ++mReloadToken;
        mDeferCardFill = true;
        mRebuildToken++;
        try
        {
            PersistNavExpand();
            CommitCopyDraft();
            ClearCopySubject();
            // 编目重读后，网格里的发布状态必须跟新会话走。留下旧卡片会把已上页的原片继续画成未上页。
            mGridCache.Clear();
            mDisplayedGridKey = null;
            if (mSession != null
                && !string.Equals(mSession.Profile.ProfilePath, profilePath, StringComparison.OrdinalIgnoreCase))
            {
                mSelectionHistory.Clear();
                mSelectionHistoryScopeKey = null;
                RestoreSelectionCommand.RaiseCanExecute();
            }

            if (coverGrid)
            {
                ShowDestinationLoadingSurface();
            }
            else
            {
                IsCatalogLoading = false;
            }

            WorkspaceSession session;
            try
            {
                session = await Task.Run(() => WorkspaceSession.Load(profilePath)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                if (token != mReloadToken)
                {
                    return false;
                }

                IsCatalogLoading = false;
                StatusText = "加载失败：" + ex.Message;
                return false;
            }

            if (token != mReloadToken)
            {
                return false;
            }

            await WhenOffUiFillIdleAsync().ConfigureAwait(true);
            if (token != mReloadToken)
            {
                return false;
            }

            string? lastChannel;
            string? lastWorkId;
            string? lastYearFolder;
            bool lastUnregGroup;
            if (mSelectedNav != null)
            {
                lastChannel = mSelectedNav.Channel.Key;
                lastWorkId = mSelectedNav.Work?.Id;
                lastYearFolder = mSelectedNav.YearFolder;
                lastUnregGroup = mSelectedNav.IsUnregisteredGroup;
            }
            else
            {
                lastChannel = mSettings.LastChannel;
                lastWorkId = mSettings.LastWorkId;
                lastYearFolder = null;
                lastUnregGroup = mSettings.LastUnregisteredGroup;
            }

            mSession = session;
            mSettings.LastProfilePath = mSession.Profile.ProfilePath;
            AppSettingsStore.Save(mSettings);
            MarkDraftStore.PruneMissing(mSession.Profile, mSession);
            WorkspaceTitle = $"网站资源编辑工具  ·  {mSession.Profile.Name}";
            mDeferCardFill = false;
            await RebuildNav(lastChannel, lastWorkId, lastUnregGroup, lastYearFolder).ConfigureAwait(true);
            if (token != mReloadToken)
            {
                return false;
            }

            AttachStageWatcher();
            RefreshCommand.RaiseCanExecute();
            PublishCommand.RaiseCanExecute();
            return true;
        }
        catch (Exception ex)
        {
            if (token != mReloadToken)
            {
                return false;
            }

            IsCatalogLoading = false;
            StatusText = "加载失败：" + ex.Message;
            return false;
        }
        finally
        {
            if (token == mReloadToken)
            {
                mDeferCardFill = false;
            }
        }
    }

    /// <summary>
    /// 编目返回后按键选回节点，再写底栏。
    /// </summary>
    private async Task SelectAfterReloadAsync(
        string profilePath,
        string? channelKey,
        string? workId,
        bool unregGroup,
        string? yearFolder,
        string status)
    {
        if (!await ReloadAsync(profilePath, coverGrid: true).ConfigureAwait(true))
        {
            return;
        }

        RestoreNav(FindNav(channelKey, workId, unregGroup, yearFolder));
        await mPendingCardFill.ConfigureAwait(true);
        StatusText = status;
    }

    /// <summary>
    /// 按栏目同步左侧树；未登记作品单独成组。同工作区就地增删，换配置才整树重建。
    /// 网格填充结束后才返回。
    /// </summary>
    private async Task RebuildNav(
        string? lastChannel = null,
        string? lastWorkId = null,
        bool lastUnregGroup = false,
        string? lastYearFolder = null)
    {
        if (mSession == null)
        {
            NavNodes.Clear();
            mNavProfilePath = null;
            mNavSelectAnchor = null;
            SelectedNav = null;
            return;
        }

        var profilePath = mSession.Profile.ProfilePath;
        var sameWorkspace = string.Equals(profilePath, mNavProfilePath, StringComparison.OrdinalIgnoreCase);
        Dictionary<string, bool> expandDict;
        if (sameWorkspace)
        {
            expandDict = NavTreeSync.CaptureExpand(NavNodes);
        }
        else
        {
            NavNodes.Clear();
            expandDict = NavExpandStore.ToExpandDict(NavExpandStore.ReadCollapsed(mSettings, profilePath));
        }

        mNavExpandApplying = true;
        try
        {
            var desiredList = BuildNavTree(mSession);
            NavTreeSync.Sync(NavNodes, desiredList);
            NavTreeSync.ApplyExpand(NavNodes, expandDict);
            mNavProfilePath = profilePath;
            RestoreNav(
                FindNav(lastChannel ?? mSettings.LastChannel, lastWorkId, lastUnregGroup, lastYearFolder)
                    ?? NavNodes.FirstOrDefault(),
                refreshCardsIfSame: true);
            RefreshNavMarkCounts();
        }
        finally
        {
            mNavExpandApplying = false;
        }

        await mPendingCardFill.ConfigureAwait(true);
    }

    /// <summary>
    /// 折叠或展开后排队写入本机设置。
    /// </summary>
    public void SchedulePersistNavExpand()
    {
        if (mNavExpandApplying || mSession == null)
        {
            return;
        }

        mNavExpandPersistDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        mNavExpandPersistDebounce.Tick -= OnNavExpandPersistTick;
        mNavExpandPersistDebounce.Tick += OnNavExpandPersistTick;
        mNavExpandPersistDebounce.Stop();
        mNavExpandPersistDebounce.Start();
    }

    /// <summary>
    /// 把当前收起的节点键写入该工作区。
    /// </summary>
    public void PersistNavExpand()
    {
        mNavExpandPersistDebounce?.Stop();
        if (mSession == null)
        {
            return;
        }

        NavExpandStore.WriteCollapsed(mSettings, mSession.Profile.ProfilePath, NavTreeSync.CaptureCollapsed(NavNodes));
        AppSettingsStore.Save(mSettings);
    }

    /// <summary>
    /// 防抖到期后写折叠。
    /// </summary>
    private void OnNavExpandPersistTick(object? sender, EventArgs e)
    {
        mNavExpandPersistDebounce?.Stop();
        PersistNavExpand();
    }

    /// <summary>
    /// 写入选中并补树高亮；同一节点时仍可强制刷新网格。
    /// </summary>
    private void RestoreNav(NavNodeViewModel? node, bool refreshCardsIfSame = false)
    {
        NavTreeSync.ApplySelection(NavNodes, node);
        mNavSelectAnchor = node;
        var sameNav = ReferenceEquals(mSelectedNav, node);
        SelectedNav = node;
        if (sameNav)
        {
            ChannelCaption = FormatChannelCaption(node);
            RefreshIngestWorks();
            RefreshCopyEditor();
            Raise(nameof(IsSenseFilterEnabled));
            Raise(nameof(ShowRegisterWork));
            RegisterWorkCommand.RaiseCanExecute();
            EditWorkCommand.RaiseCanExecute();
            TagCommand.RaiseCanExecute();
            WithdrawWorkCommand.RaiseCanExecute();
            Raise(nameof(ShowRelocate));
            RelocateCommand.RaiseCanExecute();
            if (refreshCardsIfSame)
            {
                ReconcileCards();
            }
        }

        NavTreeRestored?.Invoke();
    }

    /// <summary>
    /// Ctrl 切换该节点；Shift 按当前可见顺序连选；否则只留该节点。
    /// 焦点没变但摄影项目多选变了时，仍重铺网格。
    /// </summary>
    public void SelectNavByPointer(NavNodeViewModel node, bool toggle, bool extend)
    {
        var beforeStamp = PhotoMultiCacheStamp();
        var visible = NavTreeSync.FlattenVisible(NavNodes);
        var current = new List<NavNodeViewModel>();
        NavTreeSync.CollectSelected(NavNodes, current);
        var result = NavPointerSelection.Apply(visible, current, mNavSelectAnchor, node, toggle, extend);
        NavTreeSync.ApplySelectionSet(NavNodes, result.Selected);
        mNavSelectAnchor = result.Anchor;
        var focusStayed = ReferenceEquals(mSelectedNav, result.Focus);
        SelectedNav = result.Focus;
        if (focusStayed && !string.Equals(beforeStamp, PhotoMultiCacheStamp(), StringComparison.Ordinal))
        {
            ScheduleRebuildCards();
        }
    }

    /// <summary>
    /// 只保留一个节点，并把它当作之后连选的锚点。
    /// </summary>
    public void SelectNavExclusive(NavNodeViewModel node)
    {
        NavTreeSync.ApplySelection(NavNodes, node);
        mNavSelectAnchor = node;
        SelectedNav = node;
    }

    /// <summary>
    /// 右键已在选区内的节点时，只把网格切过去，不清其它选中。
    /// </summary>
    public void FocusNavKeepingSelection(NavNodeViewModel node)
    {
        SelectedNav = node;
    }

    /// <summary>
    /// 按当前会话排出完整项目树，供就地同步对照。
    /// </summary>
    private static List<NavNodeViewModel> BuildNavTree(WorkspaceSession session)
    {
        var rootList = new List<NavNodeViewModel>();
        foreach (var channel in session.Profile.Channels)
        {
            var registeredList = UnregisteredWorkDiscovery.RegisteredInChannel(session.Works, channel.Key);
            var unregisteredList = UnregisteredWorkDiscovery.UnregisteredInChannel(session.Works, channel.Key);
            var layout = NavYearLayout.Arrange(channel, registeredList, unregisteredList, session.StageItems);
            var childList = new List<NavNodeViewModel>();
            foreach (var row in layout.YearRows)
            {
                var yearNodes = row.Works.Select(work => new NavNodeViewModel(channel, work));
                childList.Add(new NavNodeViewModel(channel, row.Year, yearNodes));
            }

            childList.AddRange(layout.LooseRegistered.Select(work => new NavNodeViewModel(channel, work)));
            if (layout.LooseUnregistered.Count > 0)
            {
                var unregNodes = layout.LooseUnregistered.Select(work => new NavNodeViewModel(channel, work));
                childList.Add(new NavNodeViewModel(
                    channel,
                    "未登记",
                    layout.LooseUnregistered.Count + " 个文件夹",
                    unregNodes,
                    isUnregisteredGroup: true));
            }

            rootList.Add(new NavNodeViewModel(channel, childList));
        }

        return rootList;
    }

    /// <summary>
    /// 按栏目、作品或未登记分组找回导航节点。
    /// </summary>
    private NavNodeViewModel? FindNav(
        string? channelKey,
        string? workId,
        bool unregisteredGroup,
        string? yearFolder = null)
    {
        var channelNode = NavNodes.FirstOrDefault(node =>
            !string.IsNullOrWhiteSpace(channelKey) && node.Channel.Key == channelKey)
            ?? NavNodes.FirstOrDefault();
        if (channelNode == null)
        {
            return null;
        }

        if (unregisteredGroup)
        {
            return channelNode.Children.FirstOrDefault(node => node.IsUnregisteredGroup) ?? channelNode;
        }

        if (string.IsNullOrWhiteSpace(workId) && !string.IsNullOrWhiteSpace(yearFolder))
        {
            return channelNode.Children.FirstOrDefault(node => node.YearFolder == yearFolder) ?? channelNode;
        }

        if (string.IsNullOrWhiteSpace(workId))
        {
            return channelNode;
        }

        foreach (var child in channelNode.Children)
        {
            if (child.Work?.Id == workId)
            {
                return child;
            }

            var nested = child.Children.FirstOrDefault(node => node.Work?.Id == workId);
            if (nested != null)
            {
                return nested;
            }
        }

        return channelNode;
    }

    /// <summary>
    /// 栏目或未登记节点的顶栏标题。
    /// </summary>
    private static string FormatChannelCaption(NavNodeViewModel? node)
    {
        if (node == null)
        {
            return "";
        }

        if (node.IsYearGroup)
        {
            return $"{node.Channel.Zh} · {node.YearFolder}";
        }

        if (node.IsUnregisteredGroup)
        {
            return $"{node.Channel.Zh} · 未登记";
        }

        if (node.Work == null)
        {
            return $"{node.Channel.Zh} / {node.Channel.Deco}";
        }

        return node.Work.IsUnregistered
            ? $"{node.Channel.Zh} · 未登记 · {node.Work.Title}"
            : $"{node.Channel.Zh} · {node.Work.Title}";
    }

    /// <summary>
    /// 命中缓存则直接换到目标页。未命中先进入目标空页并盖加载字，格子一次放好，再按视口分批补缩略图。
    /// 编目扫描期间先记下选中，等会话换上后再铺。
    /// </summary>
    private void ScheduleRebuildCards()
    {
        if (mDeferCardFill)
        {
            return;
        }

        var token = ++mRebuildToken;
        if (TryRestoreCachedGrid())
        {
            return;
        }

        ShowDestinationLoadingSurface();
        mPendingCardFill = ContinueRebuildCardsAsync(token);
    }

    /// <summary>
    /// 先换成目标空页再亮加载字。中途再切时，半成品不写回上一页的缓存。
    /// </summary>
    private void ShowDestinationLoadingSurface()
    {
        CloseLightbox();
        mDisplayedGridKey = null;
        mCardListSyncing = true;
        try
        {
            Cards = new ObservableCollection<MediaCardViewModel>();
            CompareRows = new ObservableCollection<CompareRowViewModel>();
            AttachCardFilters();
            SelectedCardList.Clear();
            FocusCard = null;
        }
        finally
        {
            mCardListSyncing = false;
        }

        UpdateInspector();
        IsCatalogLoading = true;
        StatusText = "正在加载栏目…";
    }

    /// <summary>
    /// 目标空页罩画出来之后再后台建卡；中途再点树则丢弃本次。
    /// </summary>
    private async Task ContinueRebuildCardsAsync(int token)
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            }

            if (token != mRebuildToken)
            {
                return;
            }

            if (mSession == null || mSelectedNav == null)
            {
                RebuildCards();
                return;
            }

            RememberLastNav();
            var sessionStamp = mSession;
            var shell = await FillShellGridAsync().ConfigureAwait(true);
            if (token != mRebuildToken || !ReferenceEquals(sessionStamp, mSession))
            {
                return;
            }

            mThumbCts?.Cancel();
            mThumbCts = new CancellationTokenSource();
            // 格子只换一次。后续文件事实与缩略图就地补入，不能再插卡或换集合。
            ReplaceGrid(shell.CardList, shell.RowList, Array.Empty<string>(), null, applySort: false);
            await YieldRenderAsync().ConfigureAwait(true);
            if (token != mRebuildToken)
            {
                return;
            }

            var initialThumbnailTask = LoadInitialViewportThumbnailsAsync(mThumbCts.Token);
            var fillTime = mGridSort.Contains(GridSortKey.Time);
            var desired = await FillGridAsync(fillTime).ConfigureAwait(true);
            if (token != mRebuildToken || !ReferenceEquals(sessionStamp, mSession))
            {
                return;
            }

            if (!await ApplyFilledGridAsync(desired, token).ConfigureAwait(true))
            {
                return;
            }

            CloseLightbox();
            await FinishLoadingSurfaceAsync(scrollToFocus: true, token).ConfigureAwait(true);
            if (token != mRebuildToken)
            {
                return;
            }

            mDisplayedGridKey = CurrentGridCacheKey();
            ParkDisplayedGrid();
            _ = RequestRemainingThumbnailsAsync(initialThumbnailTask, token);
        }
        catch (Exception ex)
        {
            if (token != mRebuildToken)
            {
                return;
            }

            IsCatalogLoading = false;
            StatusText = "加载失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 把当前仍显示的网格记到离开前的键下，避免切走后把新节点的空表写进去。
    /// </summary>
    private void ParkDisplayedGrid()
    {
        if (string.IsNullOrWhiteSpace(mDisplayedGridKey))
        {
            return;
        }

        mGridCache.Park(
            mDisplayedGridKey,
            Cards.ToList(),
            CompareRows.ToList(),
            CaptureSelectedCardKeys(),
            mFocusCard?.CardKey);
    }

    /// <summary>
    /// 当前栏目节点与投放箱 / 站点 / 对照合成缓存键。
    /// </summary>
    private string? CurrentGridCacheKey()
    {
        if (mSelectedNav == null || string.IsNullOrWhiteSpace(mPane))
        {
            return null;
        }

        var navKey = PhotoMultiCacheStamp() ?? mSelectedNav.NavKey;
        return SurfaceGridCache<MediaCardViewModel, CompareRowViewModel>.BuildKey(mPane, navKey);
    }

    /// <summary>
    /// 记下当前选区键，供缓存与重建后写回。
    /// </summary>
    private List<string> CaptureSelectedCardKeys()
    {
        return SelectedCardList
            .Select(card => card.CardKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .ToList();
    }

    /// <summary>
    /// 记下当前选区键与焦点，供选区历史对照。
    /// </summary>
    private CardSelectionSnapshot CaptureSelectionSnapshot()
    {
        return new CardSelectionSnapshot(CaptureSelectedCardKeys(), mFocusCard?.CardKey);
    }

    /// <summary>
    /// 维护者改选后把上一份非空选区压入历史。
    /// </summary>
    private void RememberUserSelection(CardSelectionSnapshot previous)
    {
        mSelectionHistory.Remember(previous, CaptureSelectionSnapshot());
        RestoreSelectionCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 切栏目或切视图后丢掉上一表面的选区历史。
    /// </summary>
    private void ResetSelectionHistoryForScope()
    {
        var key = CurrentGridCacheKey();
        if (string.Equals(key, mSelectionHistoryScopeKey, StringComparison.Ordinal))
        {
            return;
        }

        mSelectionHistory.Clear();
        mSelectionHistoryScopeKey = key;
        RestoreSelectionCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 命中已看过的网格则整表替换，不再当场重扫编目。
    /// </summary>
    private bool TryRestoreCachedGrid()
    {
        var key = CurrentGridCacheKey();
        if (key == null || !mGridCache.TryGet(key, out var snapshot) || snapshot == null)
        {
            return false;
        }

        CloseLightbox();
        mThumbCts?.Cancel();
        mThumbCts = new CancellationTokenSource();
        ReplaceGrid(snapshot.CardList, snapshot.RowList, snapshot.SelectedKeyList, snapshot.FocusKey);
        mDisplayedGridKey = key;
        // 缓存只留缩略图和选区。发布状态按当前会话重绑，避免上页后回到旧快照。
        ReconcileCards();
        return true;
    }

    /// <summary>
    /// 整表换成新集合，只通知一次。未访过的切页、缓存换回与同页刷新都走这里。
    /// </summary>
    private void ReplaceGrid(
        IReadOnlyList<MediaCardViewModel> cardList,
        IReadOnlyList<CompareRowViewModel> rowList,
        IReadOnlyList<string> selectedKeyList,
        string? focusKey,
        bool applySort = true)
    {
        mCardListSyncing = true;
        try
        {
            Cards = new ObservableCollection<MediaCardViewModel>(cardList);
            CompareRows = new ObservableCollection<CompareRowViewModel>(rowList);
            AttachCardFilters(applySort);
            SelectedCardList.Clear();
            RestoreCardSelection(selectedKeyList, focusKey);
        }
        finally
        {
            mCardListSyncing = false;
        }
    }

    /// <summary>
    /// 新集合重新挂上脱敏筛与查看序。对照行不排序。
    /// </summary>
    private void AttachCardFilters(bool applySort = true)
    {
        CollectionViewSource.GetDefaultView(Cards).Filter = MatchesVisibleCard;
        CollectionViewSource.GetDefaultView(CompareRows).Filter = MatchesVisibleCompareRow;
        if (applySort)
        {
            ApplyGridSort();
        }
    }

    /// <summary>
    /// 收起态文案。
    /// </summary>
    public string GridSortCaption => mGridSort.Caption;

    /// <summary>
    /// 排序弹出层是否展开。对照页不允许展开。
    /// </summary>
    public bool IsGridSortOpen
    {
        get => mIsGridSortOpen;
        set
        {
            if (value && !IsGridSortEnabled)
            {
                value = false;
            }

            SetField(ref mIsGridSortOpen, value);
        }
    }

    /// <summary>
    /// 投放箱与站点可改查看序。对照行保持配对原序，下拉禁用。
    /// </summary>
    public bool IsGridSortEnabled => !IsComparePane;

    /// <summary>
    /// 点名称或左端数字，改入列后重排视图。
    /// </summary>
    private void ToggleGridSort(GridSortKey key)
    {
        mGridSort.Toggle(key);
        CommitGridSort();
    }

    /// <summary>
    /// 点箭头，只反向后重排视图。
    /// </summary>
    private void FlipGridSort(GridSortKey key)
    {
        mGridSort.Flip(key);
        CommitGridSort();
    }

    /// <summary>
    /// 把查看状态写入设置并刷新四行，再让网格按同一规则重排。不写排序草稿。
    /// </summary>
    private void CommitGridSort()
    {
        mSettings.GridSort = mGridSort.ToPreferences();
        AppSettingsStore.Save(mSettings);
        SyncGridSortOptions();
        ApplyGridSort();
        RequestViewportThumbnails();
        Raise(nameof(GridSortCaption));
        Raise(nameof(CanReorderSite));
        Raise(nameof(PaneHintText));
    }

    /// <summary>
    /// 四行固定，只刷新数字与箭头。
    /// </summary>
    private void SyncGridSortOptions()
    {
        foreach (var option in GridSortOptionList)
        {
            option.Apply(mGridSort.Priority(option.Key), mGridSort.Dir(option.Key));
        }
    }

    /// <summary>
    /// 有入列键时用自定义比较；清空后视图回到源集合顺序。
    /// </summary>
    private void ApplyGridSort()
    {
        if (CollectionViewSource.GetDefaultView(Cards) is not ListCollectionView view)
        {
            return;
        }

        if (mGridSort.Contains(GridSortKey.Time) && Cards.Any(card => !card.IsSortTimeReady))
        {
            var token = ++mSortFillToken;
            _ = FillSortTimesThenApplyAsync(token);
            return;
        }

        mSortFillToken++;
        var rules = mGridSort.Rules;
        view.CustomSort = rules.Count == 0 ? null : new CardGridSortComparer(Cards, rules);
    }

    /// <summary>
    /// 在界面线程之外组装不探测文件的卡片壳。
    /// </summary>
    private async Task<DesiredGrid> FillShellGridAsync()
    {
        if (Application.Current?.Dispatcher == null)
        {
            return BuildShellGrid();
        }

        EnterOffUiFill();
        try
        {
            return await Task.Run(BuildShellGrid).ConfigureAwait(true);
        }
        finally
        {
            ExitOffUiFill();
        }
    }

    /// <summary>
    /// 在界面线程之外组卡。没有调度器时就地组卡。
    /// </summary>
    private async Task<DesiredGrid> FillGridAsync(bool fillTime)
    {
        if (Application.Current?.Dispatcher == null)
        {
            return BuildGrid(fillTime);
        }

        EnterOffUiFill();
        try
        {
            return await Task.Run(() => BuildGrid(fillTime)).ConfigureAwait(true);
        }
        finally
        {
            ExitOffUiFill();
        }
    }

    /// <summary>
    /// 记下一次离开界面线程的组卡，供换会话前等待。
    /// </summary>
    private void EnterOffUiFill()
    {
        lock (mFillLock)
        {
            mOffUiFillCount++;
            mOffUiIdle ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// 一次组卡已回到界面线程。全部结束后放行正在等待的换会话。
    /// </summary>
    private void ExitOffUiFill()
    {
        TaskCompletionSource<bool>? idle = null;
        lock (mFillLock)
        {
            mOffUiFillCount--;
            if (mOffUiFillCount == 0 && mOffUiIdle != null)
            {
                idle = mOffUiIdle;
                mOffUiIdle = null;
            }
        }

        idle?.TrySetResult(true);
    }

    /// <summary>
    /// 等到没有仍在界面线程之外的组卡。
    /// </summary>
    private Task WhenOffUiFillIdleAsync()
    {
        lock (mFillLock)
        {
            if (mOffUiFillCount == 0)
            {
                return Task.CompletedTask;
            }

            mOffUiIdle ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return mOffUiIdle.Task;
        }
    }

    /// <summary>
    /// 只按会话结构组卡。预览路径可先用于补图，大小与修改时间稍后由完整填充写回。
    /// </summary>
    private DesiredGrid BuildShellGrid()
    {
        if (mSession == null)
        {
            return new DesiredGrid();
        }

        return CardGridFill.UseShells(mSession, _ => BuildDesiredGridCore());
    }

    /// <summary>
    /// 组卡。文件事实、星级与按需拍摄时间都由同一次填充读完。
    /// </summary>
    private DesiredGrid BuildGrid(bool fillTime)
    {
        if (mSession == null)
        {
            return new DesiredGrid();
        }

        return CardGridFill.Use(mSession, _ =>
        {
            var desired = BuildDesiredGridCore();
            if (fillTime)
            {
                CardGridFill.ReadSortTimes(desired.CardList);
            }

            return desired;
        });
    }

    /// <summary>
    /// 点「按时间」或换表时发现时间未填，则在界面线程之外读完再重排。
    /// </summary>
    private async Task FillSortTimesThenApplyAsync(int token)
    {
        var cardList = Cards.ToList();
        await Task.Run(() => CardGridFill.ReadSortTimes(cardList)).ConfigureAwait(true);
        if (token != mSortFillToken || !mGridSort.Contains(GridSortKey.Time))
        {
            return;
        }

        ApplyGridSort();
        RequestViewportThumbnails();
    }

    /// <summary>
    /// 星级变化且「按星级」入列时，只刷新视图顺序。
    /// </summary>
    private void RefreshGridSortForStars()
    {
        if (!mGridSort.Contains(GridSortKey.Stars))
        {
            return;
        }

        if (CollectionViewSource.GetDefaultView(Cards) is ListCollectionView view)
        {
            view.Refresh();
            RequestViewportThumbnails();
        }
    }

    /// <summary>
    /// 内容或顺序变了，但滚动偏移往往不变。交给窗口按当前视口补缩略图。
    /// </summary>
    private void RequestViewportThumbnails()
    {
        VisibleSurfaceChanged?.Invoke();
    }

    /// <summary>
    /// 排序下拉的四行，顺序固定。
    /// </summary>
    private List<GridSortOptionViewModel> CreateGridSortOptions()
    {
        return new List<GridSortOptionViewModel>
        {
            new(GridSortKey.Name, () => ToggleGridSort(GridSortKey.Name), () => FlipGridSort(GridSortKey.Name)),
            new(GridSortKey.Time, () => ToggleGridSort(GridSortKey.Time), () => FlipGridSort(GridSortKey.Time)),
            new(GridSortKey.Stars, () => ToggleGridSort(GridSortKey.Stars), () => FlipGridSort(GridSortKey.Stars)),
            new(GridSortKey.Size, () => ToggleGridSort(GridSortKey.Size), () => FlipGridSort(GridSortKey.Size))
        };
    }

    /// <summary>
    /// 打开设置窗。
    /// </summary>
    private void OpenSettings()
    {
        SettingsWindow.Show(ClearThumbCache, mSettings, AttachStageWatcher);
    }

    /// <summary>
    /// 当前节点是否为未登记作品夹。
    /// </summary>
    private bool CanRegisterWork()
    {
        return mSession != null && mSelectedNav?.Work?.IsUnregistered == true;
    }

    /// <summary>
    /// 打开登记窗；写入成功后重载并选中新作品。
    /// </summary>
    private void OpenRegisterWork()
    {
        if (mSession == null || mSelectedNav?.Work?.IsUnregistered != true)
        {
            return;
        }

        var channelKey = mSelectedNav.Channel.Key;
        var profilePath = mSession.Profile.ProfilePath;
        RegisterWorkWindow.Show(mSession, mSelectedNav.Work, workId =>
        {
            _ = SelectAfterReloadAsync(
                profilePath,
                channelKey,
                workId,
                false,
                null,
                "已登记空壳作品，投放箱文件仍未上页。");
        });
    }

    /// <summary>
    /// 心得栏目根节点可以新建。
    /// </summary>
    private bool CanCreateNote()
    {
        return mSession != null
            && NoteRules.IsNotesChannel(mSelectedNav?.Channel.Key)
            && mSelectedNav?.Work == null
            && mSelectedNav?.IsYearGroup != true
            && mSelectedNav?.IsUnregisteredGroup != true;
    }

    /// <summary>
    /// 已有心得夹可以上页、隐藏或删除。
    /// </summary>
    private bool CanPublishNote()
    {
        return mSession != null
            && !mIsExecuting
            && mSelectedNav?.Work is { SourceKind: "note" };
    }

    private bool CanHideNote() => CanPublishNote();

    private bool CanRestoreNote() => CanPublishNote();

    private bool CanRecycleNote() => CanPublishNote();

    /// <summary>
    /// 按本机时间建夹，并选中新夹。
    /// </summary>
    private void CreateNote()
    {
        if (mSession == null || !CanCreateNote())
        {
            return;
        }

        var channel = mSelectedNav!.Channel;
        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(mSession.Profile, mSession.Profile.StageRoot);
        var channelDir = Path.Combine(
            stageRoot,
            WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel).Replace('/', Path.DirectorySeparatorChar));
        string folder;
        try
        {
            folder = NoteRules.CreateFolder(channelDir, DateTime.Now);
        }
        catch (Exception ex)
        {
            TextDialog.Show("无法新建心得", ex.Message);
            return;
        }

        var profilePath = mSession.Profile.ProfilePath;
        _ = SelectAfterReloadAsync(
            profilePath,
            NoteRules.ChannelKey,
            Path.GetFileName(folder),
            false,
            null,
            "已新建心得夹。双击正文用 Typora 编辑。");
    }

    /// <summary>
    /// 标记了正文才上页整篇。只标记配图则失败。成功后这些标记不再走作品上页。
    /// </summary>
    private bool PublishMarkedNotes(IReadOnlyList<IntentItem> noteIngestList, out string error)
    {
        error = "";
        if (noteIngestList.Count == 0 || mSession == null)
        {
            return true;
        }

        var channel = mSession.Profile.Channels.First(item => NoteRules.IsNotesChannel(item.Key));
        var channelFolder = WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel);
        var groups = noteIngestList.GroupBy(item => NoteRules.NoteFolderFromStageRel(item.StageRel, channelFolder) ?? "");
        foreach (var group in groups)
        {
            if (!group.Any(item => NoteRules.IsBodyStageRel(item.StageRel)))
            {
                error = "须标记正文.md 才能上页这篇心得，不能只标记配图。";
                return false;
            }
        }

        foreach (var group in groups)
        {
            var failure = PublishNoteFolder(group.Key);
            if (failure != null)
            {
                error = failure;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 上页当前心得。未逐张标记配图时，夹内允许的图片全部入库。
    /// </summary>
    private void PublishSelectedNote()
    {
        if (mSession == null || mSelectedNav?.Work is not { SourceKind: "note" } work)
        {
            return;
        }

        if (!ConfirmIntentDialog.Show(
                "将上页「" + work.Title + "」。未逐张标记配图时，夹内允许的图片全部压图入库。中转站正文仍用相对路径。",
                requireAck: false,
                "上页心得",
                "上页"))
        {
            return;
        }

        var failure = PublishNoteFolder(work.Id);
        if (failure != null)
        {
            TextDialog.Show("无法上页", failure);
            return;
        }

        _ = SelectAfterReloadAsync(
            mSession.Profile.ProfilePath,
            NoteRules.ChannelKey,
            work.Id,
            false,
            null,
            "心得已写入发布稿。公网要等发布上线。");
    }

    /// <summary>
    /// 上页指定夹。不弹确认，不刷新界面。
    /// </summary>
    private string? PublishNoteFolder(string folderId)
    {
        if (mSession == null)
        {
            return "工作区未加载。";
        }

        var profile = mSession.Profile;
        var folder = NoteFolderPath(folderId);
        var marked = MarkedNoteIngest(folderId);
        var stars = StarDraftsForFolder(folderId);
        mIsExecuting = true;
        ExecuteCommand.RaiseCanExecute();
        NotePublishOutcome outcome = NotePublishOutcome.Fail("未执行");
        BatchRunResult batch = new() { Ok = false, FailureText = "未执行" };
        try
        {
            batch = ProgressDialog.Run("正在上页心得", _ =>
            {
                outcome = NotePublisher.Publish(
                    profile,
                    folder,
                    marked,
                    stars,
                    PrepareNoteImage,
                    (ready, objectKey, source) =>
                    {
                        var run = SitemediaClient.Ingest(profile, ready, objectKey, bump: false, source);
                        return run.Ok;
                    });
                return new BatchRunResult
                {
                    Ok = string.IsNullOrWhiteSpace(outcome.Error),
                    FailureText = outcome.Error ?? ""
                };
            });
        }
        finally
        {
            mIsExecuting = false;
            ExecuteCommand.RaiseCanExecute();
        }

        if (!batch.Ok || !string.IsNullOrWhiteSpace(outcome.Error))
        {
            return string.IsNullOrWhiteSpace(outcome.Error) ? batch.FailureText : outcome.Error;
        }

        QueueNotePending(outcome.Ingested, outcome.Retired, full: outcome.Ingested.Count > 0);
        return null;
    }

    /// <summary>
    /// 隐藏整篇。对象存储和夹都留着。
    /// </summary>
    private void HideSelectedNote()
    {
        if (mSession == null || mSelectedNav?.Work is not { SourceKind: "note" } work)
        {
            return;
        }

        var error = NoteCommands.Hide(mSession.Profile, work.Id);
        if (error != null)
        {
            TextDialog.Show("无法隐藏", error);
            return;
        }

        QueueNotePending(Array.Empty<string>(), Array.Empty<string>(), full: false);
        _ = SelectAfterReloadAsync(
            mSession.Profile.ProfilePath,
            NoteRules.ChannelKey,
            work.Id,
            false,
            null,
            "已从访客索引隐藏。公网要等发布上线。");
    }

    /// <summary>
    /// 恢复隐藏的心得。
    /// </summary>
    private void RestoreSelectedNote()
    {
        if (mSession == null || mSelectedNav?.Work is not { SourceKind: "note" } work)
        {
            return;
        }

        var error = NoteCommands.Restore(mSession.Profile, work.Id);
        if (error != null)
        {
            TextDialog.Show("无法恢复", error);
            return;
        }

        QueueNotePending(Array.Empty<string>(), Array.Empty<string>(), full: false);
        _ = SelectAfterReloadAsync(
            mSession.Profile.ProfilePath,
            NoteRules.ChannelKey,
            work.Id,
            false,
            null,
            "已恢复显示。公网要等发布上线。");
    }

    /// <summary>
    /// 未上页的夹进回收站。已上页的拒绝。
    /// </summary>
    private void RecycleSelectedNote()
    {
        if (mSession == null || mSelectedNav?.Work is not { SourceKind: "note" } work)
        {
            return;
        }

        var block = NoteRules.RecycleBlockReason(NoteIndexStore.Load(mSession.Profile), work.Id);
        if (block != null)
        {
            TextDialog.Show("无法删除", block);
            return;
        }

        if (!ConfirmIntentDialog.Show(
                "将把心得夹「" + work.Id + "」移入回收站。",
                requireAck: true,
                "删除心得",
                "删除"))
        {
            return;
        }

        var error = NoteCommands.RecycleFolder(mSession.Profile, NoteFolderPath(work.Id));
        if (error != null)
        {
            TextDialog.Show("无法删除", error);
            return;
        }

        _ = SelectAfterReloadAsync(
            mSession.Profile.ProfilePath,
            NoteRules.ChannelKey,
            null,
            false,
            null,
            "心得夹已进入回收站。");
    }

    /// <summary>
    /// 双击正文卡时启动 Typora。不是正文则返回 false，交给灯箱。
    /// </summary>
    public bool TryOpenNoteBody()
    {
        var path = FocusCard?.Stage?.FullPath;
        if (mSession == null || string.IsNullOrWhiteSpace(path) || !NoteRules.IsBodyFile(FocusCard?.Stage?.ChannelKey, path))
        {
            return false;
        }

        var exe = ToolExeStore.ResolveTypora(mSettings, searchDisk: false);
        if (exe == null)
        {
            var markdownPath = path;
            BeginToolExeSearch(
                "Typora",
                () => ToolExeStore.ResolveTypora(mSettings, searchDisk: true),
                found => StartTypora(found, markdownPath));
            return true;
        }

        StartTypora(exe, path);
        return true;
    }

    /// <summary>
    /// 已知位置没有该程序时，在后台扫固定磁盘，找到后写回设置再打开。
    /// </summary>
    private void BeginToolExeSearch(string displayName, Func<string?> search, Action<string> onFound)
    {
        if (mToolExeSearchBusy)
        {
            StatusText = "正在全盘搜索 " + displayName + "…";
            return;
        }

        mToolExeSearchBusy = true;
        StatusText = "正在全盘搜索 " + displayName + "…";
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        System.Threading.Tasks.Task.Run(search).ContinueWith(task =>
        {
            void Finish()
            {
                mToolExeSearchBusy = false;
                if (task.IsFaulted)
                {
                    StatusText = "全盘搜索 " + displayName + " 失败。";
                    return;
                }

                var found = task.Result;
                if (string.IsNullOrWhiteSpace(found))
                {
                    StatusText = "全盘未找到 " + displayName + "。";
                    return;
                }

                onFound(found);
            }

            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Finish();
            }
            else
            {
                dispatcher.Invoke(Finish);
            }
        });
    }

    /// <summary>
    /// 用 Typora 打开正文。失败写在状态行。
    /// </summary>
    private void StartTypora(string exe, string markdownPath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                ArgumentList = { markdownPath },
                UseShellExecute = false
            });
            StatusText = "已用 Typora 打开正文。";
        }
        catch (Exception ex)
        {
            StatusText = "无法启动 Typora：" + ex.Message;
        }
    }

    private string NoteFolderPath(string folderName)
    {
        var channel = mSession!.Profile.Channels.First(item => NoteRules.IsNotesChannel(item.Key));
        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(mSession.Profile, mSession.Profile.StageRoot);
        return Path.Combine(
            stageRoot,
            WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel).Replace('/', Path.DirectorySeparatorChar),
            folderName);
    }

    private HashSet<string>? MarkedNoteIngest(string folderName)
    {
        var file = MarkDraftStore.Load();
        if (mSession == null || !MarkDraftStore.MatchesWorkspace(file, mSession.Profile))
        {
            return null;
        }

        var channel = mSession.Profile.Channels.First(item => NoteRules.IsNotesChannel(item.Key));
        var prefix = WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel) + "/" + folderName + "/";
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in file.Entries)
        {
            if (!string.Equals(entry.Intent, MediaIntentCodes.StageIngest, StringComparison.Ordinal)
                || !entry.Key.StartsWith("stage:", StringComparison.Ordinal))
            {
                continue;
            }

            var rel = entry.Key["stage:".Length..];
            if (rel.StartsWith(prefix, StringComparison.Ordinal) && !NoteRules.IsBodyStageRel(rel))
            {
                set.Add(rel);
            }
        }

        return set.Count == 0 ? null : set;
    }

    private Dictionary<string, int> StarDraftsForFolder(string folderName)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        var file = StarDraftStore.Load();
        if (mSession == null || !StarDraftStore.MatchesWorkspace(file, mSession.Profile))
        {
            return dict;
        }

        var channel = mSession.Profile.Channels.First(item => NoteRules.IsNotesChannel(item.Key));
        var prefix = "stage:" + WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel) + "/" + folderName + "/";
        foreach (var entry in file.Entries)
        {
            if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            dict[entry.Key["stage:".Length..]] = entry.Stars;
        }

        return dict;
    }

    private static bool PrepareNoteImage(string sourceFullPath, string readyFullPath, bool alreadyWebp)
    {
        if (alreadyWebp)
        {
            File.Copy(sourceFullPath, readyFullPath, overwrite: true);
            return true;
        }

        var result = WebpPrepareClient.Prepare(sourceFullPath, readyFullPath, NoteRules.ChannelKey);
        return result.Ok;
    }

    private void QueueNotePending(IReadOnlyList<string> ingested, IReadOnlyList<string> retired, bool full)
    {
        if (mSession == null)
        {
            return;
        }

        PendingPublishStore.MergeSave(new PendingPublish
        {
            Version = 1,
            ProfilePath = mSession.Profile.ProfilePath,
            WorkspaceRoot = mSession.Profile.ResolvedRoot,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Deploy = full ? "full" : "spa",
            IngestObjectList = ingested.ToList(),
            WithdrawObjectList = retired.ToList(),
            CopyOnly = !full && retired.Count == 0
        });
        PublishCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 当前节点是否为已入编项目。
    /// </summary>
    private bool CanEditWork()
    {
        return mSession != null
            && mSelectedNav?.Work is { IsUnregistered: false, SourceKind: not "note" };
    }

    /// <summary>
    /// 当前节点是否为内容池里已入编的作品。
    /// </summary>
    private bool CanWithdrawWork()
    {
        return mSession != null
            && !mIsExecuting
            && !mIsPublishing
            && mSelectedNav?.Work is { IsUnregistered: false, SourceKind: "works" };
    }

    /// <summary>
    /// 删除当前作品的内容记录，对象键留到发布上线再撤。
    /// </summary>
    private async void WithdrawSelectedWork()
    {
        if (mSession == null || mSelectedNav?.Work is not { IsUnregistered: false, SourceKind: "works" } work)
        {
            return;
        }

        var document = IntentDocumentBuilder.BuildWorkWithdraw(
            mSession,
            ExecutionMode.Direct,
            work.Channel,
            work.Id);
        var report = PreviewReporter.BuildFromDocument(mSession, document);
        if (report.HasHardError)
        {
            TextDialog.Show("无法整项撤下", PromptRenderer.RenderPreview(report));
            return;
        }

        var objectCount = report.Document.Items
            .Where(item => item.Intent == MediaIntentCodes.WorkWithdraw)
            .SelectMany(item => item.ObjectList ?? new List<string>())
            .Distinct(StringComparer.Ordinal)
            .Count();
        var objectLine = objectCount == 0
            ? "这条作品没有待撤对象。"
            : $"发布上线时删除正式位与对象存储，共 {objectCount} 个对象（视频含伴生封面）。";
        if (!ConfirmIntentDialog.Show(
                $"整项撤下「{work.Title}」。\n将删除内容记录。{objectLine}\n中转站文件保留。正式位目录清空后删除空目录。",
                requireAck: true,
                "确认整项撤下",
                "撤下"))
        {
            return;
        }

        var outDir = Path.Combine(ToolPaths.FindToolRoot() ?? Path.GetTempPath(), "out");
        Directory.CreateDirectory(outDir);
        var intentPath = Path.Combine(outDir, "intent.json");
        File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(document), JsonUtil.Utf8NoBom);

        mIsExecuting = true;
        ExecuteCommand.RaiseCanExecute();
        PublishCommand.RaiseCanExecute();
        WithdrawWorkCommand.RaiseCanExecute();
        StatusText = "正在整项撤下…";
        BatchRunResult result;
        try
        {
            result = ProgressDialog.Run(
                "正在整项撤下",
                progress => BatchExecutor.Run(mSession, document, intentPath, progress));
        }
        finally
        {
            mIsExecuting = false;
            ExecuteCommand.RaiseCanExecute();
            PublishCommand.RaiseCanExecute();
            WithdrawWorkCommand.RaiseCanExecute();
        }

        var profile = mSession.Profile;
        await ReloadAsync(profile.ProfilePath, coverGrid: true).ConfigureAwait(true);
        if (!result.Ok)
        {
            TextDialog.Show(
                "整项撤下失败，意图文件已保留",
                ExecuteResultComposer.BuildFailure(result, intentPath));
            ApplyIdleStatus();
            return;
        }

        if (result.Pending != null)
        {
            PendingPublishStore.MergeSave(result.Pending);
        }

        TextDialog.Show(
            "整项撤下已写入",
            ExecuteResultComposer.BuildSuccess(
                result,
                new ExecuteResultCounts(0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0)));
        StatusText = objectCount == 0
            ? "已删除内容记录。请发布上线。"
            : $"已删除内容记录。发布上线时撤下 {objectCount} 个对象。";
        PublishCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 打开编辑窗；写入成功后重载并留在该项目。
    /// </summary>
    private void OpenEditWork()
    {
        if (mSession == null || mSelectedNav?.Work is not { IsUnregistered: false } work)
        {
            return;
        }

        var channelKey = mSelectedNav.Channel.Key;
        var profilePath = mSession.Profile.ProfilePath;
        RegisterWorkWindow.ShowEdit(mSession, work, workId =>
        {
            _ = SelectAfterReloadAsync(profilePath, channelKey, workId, false, null, "已更新项目题名、开始日与地点。");
        });
    }

    /// <summary>
    /// 已入编的摄影作品可以打类型与标签。
    /// </summary>
    private bool CanTag()
    {
        return mSession != null
            && mSelectedNav?.Work is { IsUnregistered: false, SourceKind: "works" } work
            && PhotoFactRules.RequiresFacts(work.Channel);
    }

    /// <summary>
    /// 打开标签窗。站点页里 Ctrl 多选的卡作为批量目标。
    /// </summary>
    private void OpenTags()
    {
        if (mSession == null || mSelectedNav?.Work is not { IsUnregistered: false, SourceKind: "works" } work)
        {
            return;
        }

        var objectKeyList = SelectedCardList
            .Where(card => card.Kind == CardKind.Site && card.Site != null)
            .Select(card => card.Site!)
            .Where(site =>
                site.WorkId == work.Id
                && site.ChannelKey == work.Channel
                && !string.IsNullOrWhiteSpace(site.ObjectKey))
            .Select(site => site.ObjectKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var channelKey = work.Channel;
        var workId = work.Id;
        var profilePath = mSession.Profile.ProfilePath;
        TagWindow.Show(mSession, work, objectKeyList, async () =>
        {
            var reloaded = await ReloadAsync(profilePath, coverGrid: true).ConfigureAwait(true);
            if (mSession == null)
            {
                return null;
            }

            if (reloaded)
            {
                RestoreNav(FindNav(channelKey, workId, false));
                await mPendingCardFill.ConfigureAwait(true);
            }

            return mSession.Works.FirstOrDefault(item =>
                !item.IsUnregistered
                && item.Channel == channelKey
                && item.Id == workId);
        });
        RefreshResourceTags();
    }

    /// <summary>
    /// 检视栏回车或空格提交一个标签。
    /// </summary>
    public void CommitResourceTag(TagEditorCommitEventArgs args)
    {
        if (!TryResourceTagTarget(out var session, out var work, out var frames))
        {
            return;
        }

        var outcome = TagEditorHost.CommitFrames(
            session, work.Channel, frames, ResourceTagSuggestions, args.Text, out var inputNotice,
            text => OnResourceTagPersistFailed(text, work.Channel, work.Id));
        args.Notice = inputNotice;
        args.Accepted = outcome is TagWriteOutcome.Written or TagWriteOutcome.Unchanged;
        FinishResourceTagWrite(outcome, frames.Count);
    }

    /// <summary>
    /// 检视栏芯片叉，从当前资源去掉。
    /// </summary>
    public void RemoveResourceTag(TagEditorItemEventArgs args)
    {
        if (!TryResourceTagTarget(out var session, out var work, out var frames))
        {
            return;
        }

        var outcome = TagEditorHost.RemoveFromFrames(
            session, work.Channel, frames, args.Item,
            text => OnResourceTagPersistFailed(text, work.Channel, work.Id));
        args.Accepted = outcome is TagWriteOutcome.Written or TagWriteOutcome.Unchanged;
        FinishResourceTagWrite(outcome, frames.Count);
    }

    /// <summary>
    /// 检视栏点下拉项。
    /// </summary>
    public void PickResourceTag(TagEditorItemEventArgs args)
    {
        if (!TryResourceTagTarget(out var session, out var work, out var frames))
        {
            return;
        }

        var outcome = TagEditorHost.ToggleFrames(
            session, work.Channel, frames, args.Item,
            text => OnResourceTagPersistFailed(text, work.Channel, work.Id));
        args.Accepted = outcome is TagWriteOutcome.Written or TagWriteOutcome.Unchanged;
        FinishResourceTagWrite(outcome, frames.Count);
    }

    /// <summary>
    /// 检视栏删除一条自由预设。
    /// </summary>
    public void DeleteResourceTagPreset(TagEditorItem item)
    {
        if (mSession == null
            || mSelectedNav == null
            || !PhotoFactRules.RequiresFacts(mSelectedNav.Channel.Key)
            || item.Kind != TagEditorKind.Free)
        {
            return;
        }

        var channel = mSelectedNav.Channel.Key;
        var workId = mSelectedNav.Work?.Id ?? "";
        var outcome = TagEditorHost.DeletePreset(
            mSession, channel, item.Label,
            text => OnResourceTagPersistFailed(text, channel, workId));
        if (outcome == TagWriteOutcome.Written)
        {
            RefreshResourceTags();
            StatusText = "已删除预设「" + item.Label + "」。";
        }
    }

    /// <summary>
    /// 后台写盘失败后重载，界面回到磁盘上的标签。
    /// </summary>
    private async void OnResourceTagPersistFailed(string failureText, string channelKey, string workId)
    {
        TextDialog.Show("标签失败，意图文件已保留", failureText);
        if (mSession == null)
        {
            return;
        }

        string? restoreWorkId;
        string? yearFolder;
        if (mSelectedNav != null
            && string.Equals(mSelectedNav.Channel.Key, channelKey, StringComparison.Ordinal))
        {
            restoreWorkId = mSelectedNav.Work?.Id;
            yearFolder = string.IsNullOrWhiteSpace(restoreWorkId) ? mSelectedNav.YearFolder : null;
        }
        else
        {
            restoreWorkId = workId;
            yearFolder = null;
        }

        var profilePath = mSession.Profile.ProfilePath;
        if (!await ReloadAsync(profilePath, coverGrid: true).ConfigureAwait(true))
        {
            return;
        }

        RestoreNav(FindNav(channelKey, restoreWorkId, false, yearFolder));
        await mPendingCardFill.ConfigureAwait(true);
    }

    /// <summary>
    /// 当前已上页照片及其现有标签。栏目或年份下按每张卡片认领作品。
    /// </summary>
    private bool TryResourceTagTarget(
        out WorkspaceSession session,
        out WorkCatalogItem work,
        out List<(string WorkId, string ObjectKey, IReadOnlyList<string> Themes, IReadOnlyList<string> Tags)> frames)
    {
        session = mSession!;
        work = null!;
        frames = new List<(string, string, IReadOnlyList<string>, IReadOnlyList<string>)>();
        var published = SelectedPublishedFrameList();
        if (!ResourceTagEditable || mSession == null || published.Count == 0)
        {
            return false;
        }

        session = mSession;
        work = published[0].Work;
        foreach (var (item, key) in published)
        {
            var frame = TagEditorCatalog.ReadFrame(item, key);
            frames.Add((item.Id, key, frame.Themes, frame.Tags));
        }

        return frames.Count > 0;
    }

    /// <summary>
    /// 内存里的标签已经改完，马上重画芯片。写盘在后台。
    /// </summary>
    private void FinishResourceTagWrite(TagWriteOutcome outcome, int frameCount)
    {
        if (outcome != TagWriteOutcome.Written || mSession == null)
        {
            return;
        }

        RefreshResourceTags();
        StatusText = frameCount <= 1 ? "已写入这张的标签。" : $"已写入 {frameCount} 张的标签。";
    }

    /// <summary>
    /// 摄影栏目、年份或项目下选中的投放箱或站点资源。
    /// </summary>
    private List<MediaCardViewModel> SelectedPhotoCardList()
    {
        if (mSelectedNav == null || !PhotoFactRules.RequiresFacts(mSelectedNav.Channel.Key))
        {
            return new List<MediaCardViewModel>();
        }

        return SelectedCardList
            .Where(card => card.Kind is CardKind.Stage or CardKind.Site)
            .ToList();
    }

    /// <summary>
    /// 已选中且已经上页的照片。项目节点用当前作品；栏目或年份按卡片认领。
    /// </summary>
    private List<(WorkCatalogItem Work, string ObjectKey)> SelectedPublishedFrameList()
    {
        var frameList = new List<(WorkCatalogItem Work, string ObjectKey)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ownedByWork = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var card in SelectedPhotoCardList())
        {
            var work = ResolveResourceWork(card);
            if (work == null)
            {
                continue;
            }

            if (!ownedByWork.TryGetValue(work.Id, out var owned))
            {
                owned = new HashSet<string>(
                    work.Media
                        .Where(item => !string.IsNullOrWhiteSpace(item.Src))
                        .Select(item => JsonUtil.ToRel(item.Src!)),
                    StringComparer.Ordinal);
                ownedByWork[work.Id] = owned;
            }

            var key = card.Kind == CardKind.Site
                ? card.Site?.ObjectKey
                : card.Stage?.MatchedObject;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var rel = JsonUtil.ToRel(key);
            if (owned.Contains(rel) && seen.Add(work.Id + "\u001f" + rel))
            {
                frameList.Add((work, rel));
            }
        }

        return frameList;
    }

    /// <summary>
    /// 选中资源所属的已入编摄影作品。
    /// </summary>
    private WorkCatalogItem? ResolveResourceWork(MediaCardViewModel card)
    {
        if (mSession == null || mSelectedNav == null)
        {
            return null;
        }

        if (mSelectedNav.Work is { IsUnregistered: false, SourceKind: "works" } navWork
            && PhotoFactRules.RequiresFacts(navWork.Channel))
        {
            return navWork;
        }

        var channel = mSelectedNav.Channel.Key;
        var workId = CopyOwner.ResolveWorkId(mSession, card.Stage, card.Site);
        var matched = FindPhotoWork(channel, workId);
        if (matched != null)
        {
            return matched;
        }

        var key = card.Kind == CardKind.Site
            ? card.Site?.ObjectKey
            : card.Stage?.MatchedObject;
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var rel = JsonUtil.ToRel(key);
        return mSession.Works.FirstOrDefault(item =>
            IsEditablePhotoWork(item, channel)
            && item.Media.Any(media =>
                !string.IsNullOrWhiteSpace(media.Src)
                && string.Equals(JsonUtil.ToRel(media.Src), rel, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 当前栏目里可写标签的已入编作品。
    /// </summary>
    private WorkCatalogItem? FindPhotoWork(string channel, string? workId)
    {
        if (mSession == null || string.IsNullOrWhiteSpace(workId))
        {
            return null;
        }

        return mSession.Works.FirstOrDefault(item =>
            IsEditablePhotoWork(item, channel)
            && string.Equals(item.Id, workId, StringComparison.Ordinal));
    }

    /// <summary>
    /// 该作品是否接受单张照片标签。
    /// </summary>
    private static bool IsEditablePhotoWork(WorkCatalogItem item, string channel)
    {
        return !item.IsUnregistered
            && item.SourceKind == "works"
            && PhotoFactRules.RequiresFacts(item.Channel)
            && string.Equals(item.Channel, channel, StringComparison.Ordinal);
    }

    /// <summary>
    /// 收起标签组件。
    /// </summary>
    private void ClearResourceTags()
    {
        ShowResourceTags = false;
        ShowResourceTagBatch = false;
        ShowResourceTagHint = false;
        ResourceTagEditable = false;
        ResourceTagCaption = "";
        ResourceTagHint = "";
        ResourceTagSuggestions = Array.Empty<TagEditorItem>();
        ResourceTagSelected = Array.Empty<TagEditorItem>();
    }

    /// <summary>
    /// 选区变化时把标签装到名称上方。未选资源则收起。
    /// </summary>
    private void RefreshResourceTags()
    {
        var cardList = SelectedPhotoCardList();
        if (cardList.Count == 0 || mSession == null || mSelectedNav == null)
        {
            ClearResourceTags();
            return;
        }

        var channel = mSelectedNav.Channel.Key;
        var published = SelectedPublishedFrameList();
        var channelWorks = mSession.Works.Where(item =>
            !item.IsUnregistered && string.Equals(item.Channel, channel, StringComparison.Ordinal));
        var suggestions = TagEditorCatalog.BuildSuggestions(
            channelWorks,
            TagVocabStore.Read(mSession.Profile.ResolvedRoot, channel),
            includeTypes: true);
        var frames = published
            .Select(item => TagEditorCatalog.ReadFrame(item.Work, item.ObjectKey))
            .ToList();
        ResourceTagSuggestions = suggestions;
        ResourceTagSelected = TagEditorCatalog.ChipsForFrames(suggestions, frames);
        ResourceTagCaption = "已选 " + cardList.Count + " 张";
        ShowResourceTagBatch = cardList.Count > 1;
        ShowResourceTagHint = published.Count == 0;
        ResourceTagEditable = published.Count > 0;
        ResourceTagHint = cardList.Count > 1 ? "上页后写入这些照片。" : "上页后写入这一张。";
        ShowResourceTags = true;
    }

    /// <summary>
    /// 点项目汇总标签后，选中当前网格里带着该词的资源。
    /// </summary>
    public void ChooseProjectTag(TagEditorItem item)
    {
        var works = TagScopeWorks();
        var scopeId = TagScopeId();
        if (works.Count == 0 || string.IsNullOrWhiteSpace(scopeId))
        {
            return;
        }

        mProjectTagKey = item.Id;
        mProjectTagWorkId = scopeId;
        var keySet = TagEditorCatalog.ObjectKeysWithTag(works, item);
        var matches = Cards.Where(card => CardHasObject(card, keySet)).ToList();
        mApplyingProjectTag = true;
        try
        {
            SyncSelection(matches);
            if (matches.Count > 0 && (mFocusCard == null || !matches.Contains(mFocusCard)))
            {
                FocusCard = matches[0];
            }
        }
        finally
        {
            mApplyingProjectTag = false;
        }

        ScrollRestoredCardSelection = false;
        CardSelectionRestored?.Invoke();
    }

    /// <summary>
    /// 左侧是摄影项目、年份或一级栏目，且中间没有选中资源时，把范围内已用标签装到描述上方。
    /// </summary>
    private void RefreshProjectTags()
    {
        if (SelectedCardList.Count > 0)
        {
            mProjectTagKey = null;
            mProjectTagWorkId = null;
            ShowProjectTags = false;
            ProjectTagList = Array.Empty<TagEditorItem>();
            return;
        }

        var works = TagScopeWorks();
        var scopeId = TagScopeId();
        if (works.Count == 0 || string.IsNullOrWhiteSpace(scopeId))
        {
            ClearProjectTags();
            return;
        }

        if (!string.Equals(mProjectTagWorkId, scopeId, StringComparison.Ordinal))
        {
            if (!mApplyingProjectTag)
            {
                mProjectTagKey = null;
            }

            mProjectTagWorkId = scopeId;
        }

        var chips = TagEditorCatalog.SummarizeWorks(works);
        if (!string.IsNullOrEmpty(mProjectTagKey))
        {
            foreach (var chip in chips)
            {
                chip.IsActive = string.Equals(chip.Id, mProjectTagKey, StringComparison.Ordinal);
            }
        }

        ProjectTagList = chips;
        ShowProjectTags = chips.Count > 0;
    }

    /// <summary>
    /// 收起项目标签汇总。
    /// </summary>
    private void ClearProjectTags()
    {
        ShowProjectTags = false;
        ProjectTagList = Array.Empty<TagEditorItem>();
        if (!mApplyingProjectTag)
        {
            mProjectTagKey = null;
            mProjectTagWorkId = null;
        }
    }

    /// <summary>
    /// 卡片是否对应这些已上页对象键。投放箱只认已配对的对象。
    /// </summary>
    private static bool CardHasObject(MediaCardViewModel card, HashSet<string> keySet)
    {
        var key = card.Kind == CardKind.Site ? card.Site?.ObjectKey : card.Stage?.MatchedObject;
        return !string.IsNullOrWhiteSpace(key) && keySet.Contains(JsonUtil.ToRel(key));
    }

    /// <summary>
    /// 对照选区是否可重挂：一侧站点对象、一侧投放箱新路径。
    /// </summary>
    private bool CanRelocate()
    {
        if (mSession == null || !IsComparePane)
        {
            return false;
        }

        return TryReadRelocatePair(out _, out _, out _);
    }

    /// <summary>
    /// 打开重挂窗；写入成功后重载编目。
    /// </summary>
    private void OpenRelocate()
    {
        if (mSession == null || !TryReadRelocatePair(out var objectKey, out var before, out var after))
        {
            return;
        }

        var profilePath = mSession.Profile.ProfilePath;
        var channelKey = mSelectedNav?.Channel.Key;
        var workId = mSelectedNav?.Work?.Id;
        RelocateWindow.Show(mSession, objectKey, before, after, () =>
        {
            _ = SelectAfterReloadAsync(profilePath, channelKey, workId, false, null, "已重挂台账路径，对象键与内容层未改。");
        });
    }

    /// <summary>
    /// 从选区取出站点对象与投放箱新路径。
    /// </summary>
    private bool TryReadRelocatePair(out string objectKey, out string stageRelBefore, out string stageRelAfter)
    {
        objectKey = "";
        stageRelBefore = "";
        stageRelAfter = "";
        var stage = SelectedCardList.Select(card => card.Stage).FirstOrDefault(item => item != null)
            ?? FocusCard?.Stage;
        var site = SelectedCardList.Select(card => card.Site).FirstOrDefault(item => item != null)
            ?? FocusCard?.Site;
        if (stage == null || site == null || string.IsNullOrWhiteSpace(site.ObjectKey))
        {
            return false;
        }

        if (!mSession!.LedgerDict.TryGetValue(site.ObjectKey, out var record)
            || string.IsNullOrWhiteSpace(record.StageRel))
        {
            return false;
        }

        if (string.Equals(JsonUtil.ToRel(record.StageRel), JsonUtil.ToRel(stage.StageRel), StringComparison.Ordinal))
        {
            return false;
        }

        objectKey = site.ObjectKey;
        stageRelBefore = record.StageRel;
        stageRelAfter = stage.StageRel;
        return true;
    }

    /// <summary>
    /// 清理磁盘与内存缩略图，并重载当前可见格。
    /// </summary>
    private void ClearThumbCache()
    {
        ThumbCacheStore.Clear();
        mThumbnails.ClearMemory();
        ForgetCachedThumbnails();

        InspectorImage = null;
        mThumbCts?.Cancel();
        mThumbCts = new CancellationTokenSource();
        _ = LoadVisibleThumbnailsAsync(mThumbCts.Token);
        UpdateInspector();
        StatusText = "已清理缩略图缓存。";
    }

    /// <summary>
    /// 按当前栏目 / 作品与视图重建网格。有栏目时走后台填充，界面线程只换表。
    /// </summary>
    private void RebuildCards()
    {
        mThumbCts?.Cancel();
        mThumbCts = new CancellationTokenSource();
        if (mSession == null || mSelectedNav == null)
        {
            var selectedKeyList = CaptureSelectedCardKeys();
            ReplaceGrid(Array.Empty<MediaCardViewModel>(), Array.Empty<CompareRowViewModel>(), selectedKeyList, null);
            FocusCard = null;
            CloseLightbox();
            IsCatalogLoading = false;
            mDisplayedGridKey = null;
            RefreshNotice();
            Raise(nameof(MarkSummary));
            return;
        }

        if (mDeferCardFill)
        {
            return;
        }

        var token = ++mRebuildToken;
        mPendingCardFill = ContinueRebuildCardsAsync(token);
    }

    /// <summary>
    /// 同栏目同视图按键就地增删，不盖加载罩、不拆已有卡片。组卡在界面线程之外。
    /// </summary>
    private void ReconcileCards()
    {
        if (mDeferCardFill)
        {
            return;
        }

        if (mSession == null || mSelectedNav == null)
        {
            RebuildCards();
            return;
        }

        var token = ++mRebuildToken;
        mPendingCardFill = ContinueReconcileCardsAsync(token);
    }

    /// <summary>
    /// 等后台组卡完成后再就地同步。不盖罩，不改滚动。
    /// </summary>
    private async Task ContinueReconcileCardsAsync(int token)
    {
        try
        {
            RememberLastNav();
            var fillTime = mGridSort.Contains(GridSortKey.Time);
            var sessionStamp = mSession;
            var desired = await FillGridAsync(fillTime).ConfigureAwait(true);
            if (token != mRebuildToken || !ReferenceEquals(sessionStamp, mSession))
            {
                return;
            }

            mCardListSyncing = true;
            try
            {
                CardGridSync.SyncCards(Cards, desired.CardList);
                CardGridSync.SyncRows(CompareRows, CardGridSync.RemapRows(desired.RowList, Cards));
                PruneCardSelection();
            }
            finally
            {
                mCardListSyncing = false;
            }

            if (mIsLightboxOpen && (FocusCard == null || FocusCard.IsVideo))
            {
                CloseLightbox();
            }

            RefreshGridSortForStars();
            FinishCardSurface(scrollToFocus: false);
            if (token != mRebuildToken)
            {
                return;
            }

            if (mGridSort.Contains(GridSortKey.Time) && Cards.Any(card => !card.IsSortTimeReady))
            {
                ApplyGridSort();
            }

            mDisplayedGridKey = CurrentGridCacheKey();
            ParkDisplayedGrid();
        }
        catch (Exception ex)
        {
            if (token != mRebuildToken)
            {
                return;
            }

            IsCatalogLoading = false;
            StatusText = "加载失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 组卡本体。调用方已放好填充。
    /// </summary>
    private DesiredGrid BuildDesiredGridCore()
    {
        var desired = new DesiredGrid();
        if (mSession == null || mSelectedNav == null)
        {
            return desired;
        }

        var yearNodes = SelectedPhotoYearNodes();
        if (yearNodes.Count >= 2)
        {
            AppendPhotoYearGrid(desired, yearNodes);
            return desired;
        }

        var photoWorks = PhotoProjectsForGrid();
        if (photoWorks.Count >= 2)
        {
            AppendPhotoWorkGrid(desired, photoWorks);
            return desired;
        }

        var channelNodes = SelectedChannelRoots();
        if (channelNodes.Count >= 2)
        {
            AppendChannelGrid(desired, channelNodes);
            return desired;
        }

        var channelKey = mSelectedNav.Channel.Key;
        var workId = mSelectedNav.Work?.Id;
        var yearFolder = mSelectedNav.YearFolder;
        var unregGroup = mSelectedNav.IsUnregisteredGroup;
        if (IsComparePane)
        {
            var pinList = PinStore.Load(mSession.Profile.ProfilePath);
            var workIdList = yearFolder == null
                ? EnumerateCompareWorkIds(channelKey, workId, unregGroup)
                : ChildWorkIds(mSelectedNav).Select(id => (string?)id);
            foreach (var compareWorkId in workIdList)
            {
                foreach (var row in ComparePairing.Build(mSession, channelKey, compareWorkId, pinList))
                {
                    var left = row.Stage == null ? null : CreateCard(row.Stage, CardGridFill.Current.ReadStage(row.Stage));
                    var right = row.Site == null ? null : CreateCard(row.Site, CardGridFill.Current.ReadSite(row.Site));
                    if (left != null)
                    {
                        desired.CardList.Add(left);
                    }

                    if (right != null)
                    {
                        desired.CardList.Add(right);
                    }

                    desired.RowList.Add(new CompareRowViewModel(row, left, right));
                }
            }
        }
        else if (IsStagePane)
        {
            var stageItems = yearFolder != null
                ? mSession.StageItems.Where(item =>
                    item.ChannelKey == channelKey
                    && StageWorkFolder.TryReadYearFolder(mSelectedNav.Channel, item.StageFolderGuess, out var itemYear)
                    && itemYear == yearFolder)
                : unregGroup
                ? mSession.StageItems.Where(item => item.ChannelKey == channelKey)
                : StageWorkScope.CandidatesForWork(mSession, channelKey, workId);
            foreach (var item in stageItems)
            {
                if (yearFolder == null && !StageMatchesNav(item, workId, unregGroup))
                {
                    continue;
                }

                desired.CardList.Add(CreateCard(item, CardGridFill.Current.ReadStage(item)));
            }
        }
        else if (yearFolder != null)
        {
            var workIdSet = ChildWorkIds(mSelectedNav);
            AppendOrderedSiteCards(desired, mSession.SiteItems.Where(item =>
                item.ChannelKey == channelKey
                && !string.IsNullOrWhiteSpace(item.WorkId)
                && workIdSet.Contains(item.WorkId)));
        }
        else if (!unregGroup)
        {
            var siteList = mSession.SiteItems
                .Where(item => item.ChannelKey == channelKey && (workId == null || item.WorkId == workId))
                .ToList();
            if (workId != null && mSession != null)
            {
                var draft = ReorderDraftStore.Find(
                    ReorderDraftStore.Load(),
                    mSession.Profile,
                    ReorderDraftStore.WorkKey(channelKey, workId));
                if (draft != null && draft.ObjectList.Count > 0)
                {
                    siteList = MediaOrder.ApplySiteItems(siteList, draft.ObjectList);
                    foreach (var item in siteList)
                    {
                        desired.CardList.Add(CreateCard(item, CardGridFill.Current.ReadSite(item)));
                    }
                }
                else
                {
                    AppendOrderedSiteCards(desired, siteList);
                }
            }
            else
            {
                AppendOrderedSiteCards(desired, siteList);
            }
        }

        return desired;
    }

    /// <summary>
    /// 记住当前栏目与项目，供下次启动选树。
    /// </summary>
    private void RememberLastNav()
    {
        if (mSelectedNav == null)
        {
            return;
        }

        mSettings.LastChannel = mSelectedNav.Channel.Key;
        mSettings.LastWorkId = mSelectedNav.Work?.Id;
        mSettings.LastUnregisteredGroup = mSelectedNav.IsUnregisteredGroup;
        AppSettingsStore.Save(mSettings);
    }

    /// <summary>
    /// 去掉已不在网格中的选区；焦点被撤下则退到仍选中的第一张。
    /// </summary>
    private void PruneCardSelection()
    {
        var remainSet = new HashSet<MediaCardViewModel>(Cards);
        SelectedCardList.RemoveAll(card => !remainSet.Contains(card));
        foreach (var card in Cards)
        {
            card.IsSelected = SelectedCardList.Contains(card);
        }

        if (mFocusCard != null && !remainSet.Contains(mFocusCard))
        {
            FocusCard = SelectedCardList.FirstOrDefault();
        }
    }

    /// <summary>
    /// 刷新检视、警告条与缩略图请求。缓存命中或就地同步时立刻撤罩。
    /// </summary>
    private void FinishCardSurface(bool scrollToFocus)
    {
        IsCatalogLoading = false;
        ApplyCardSurfaceChrome(scrollToFocus);
        RequestViewportThumbnails();
        ApplyIdleStatus();
    }

    /// <summary>
    /// 完整事实已就地写回后撤加载字。缩略图装载独立运行，不反过来卡住页面。
    /// </summary>
    private async Task FinishLoadingSurfaceAsync(bool scrollToFocus, int token)
    {
        ApplyCardSurfaceChrome(scrollToFocus);
        await YieldRenderAsync().ConfigureAwait(true);
        if (token != mRebuildToken)
        {
            return;
        }

        IsCatalogLoading = false;
        ApplyIdleStatus();
    }

    /// <summary>
    /// 把完整填充结果写回已经显示的卡片。集合与对照行不换，避免前面的卡片随批次闪动。
    /// </summary>
    private async Task<bool> ApplyFilledGridAsync(DesiredGrid desired, int token)
    {
        const int batchCount = 48;
        var incomingDict = new Dictionary<string, MediaCardViewModel>(StringComparer.Ordinal);
        foreach (var incoming in desired.CardList)
        {
            if (!string.IsNullOrWhiteSpace(incoming.CardKey))
            {
                incomingDict[incoming.CardKey] = incoming;
            }
        }

        var pending = 0;
        foreach (var card in Cards)
        {
            if (token != mRebuildToken)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(card.CardKey)
                && incomingDict.TryGetValue(card.CardKey, out var incoming))
            {
                card.ApplyCatalog(incoming);
            }

            pending++;
            if (pending < batchCount)
            {
                continue;
            }

            pending = 0;
            await YieldRenderAsync().ConfigureAwait(true);
        }

        if (token != mRebuildToken)
        {
            return false;
        }

        ApplyGridSort();
        return true;
    }

    /// <summary>
    /// 卡片壳落下后立即按当前视口补图。每四张让出一次绘制，集合本身不再变化。
    /// </summary>
    private async Task LoadInitialViewportThumbnailsAsync(CancellationToken token)
    {
        if (IsComparePane)
        {
            var rowList = VisibleCompareRowList();
            var cardList = new List<MediaCardViewModel>();
            var limit = Math.Max(EstimateTwoPageCount(), 1) * 2;
            for (var i = 0; i < rowList.Count && cardList.Count < limit; i++)
            {
                if (rowList[i].Left != null)
                {
                    cardList.Add(rowList[i].Left!);
                }

                if (rowList[i].Right != null)
                {
                    cardList.Add(rowList[i].Right!);
                }
            }

            await LoadVisibleThumbnailsAsync(token, cardList).ConfigureAwait(true);
            return;
        }

        mLoadStart = 0;
        mLoadCount = Math.Max(mLoadCount, EstimateTwoPageCount());
        await LoadVisibleThumbnailsAsync(token).ConfigureAwait(true);
    }

    /// <summary>
    /// 首轮视口补图结束后再按最终预览路径补漏，避免壳层猜测路径失效。
    /// </summary>
    private async Task RequestRemainingThumbnailsAsync(Task initialTask, int token)
    {
        try
        {
            await initialTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token == mRebuildToken)
        {
            RequestViewportThumbnails();
        }
    }

    /// <summary>
    /// 写回标记、警告条与检视，不碰加载罩。
    /// </summary>
    private void ApplyCardSurfaceChrome(bool scrollToFocus)
    {
        ApplyPersistedMarks();
        RefreshIngestWorks();
        RefreshNotice();
        Raise(nameof(MarkSummary));
        UpdateInspector();
        RaiseMarkCommands();
        RaiseLightboxChrome();
        ScrollRestoredCardSelection = scrollToFocus;
        CardSelectionRestored?.Invoke();
    }

    /// <summary>
    /// 当前栏目 / 视图下待铺到网格的卡片与对照行。
    /// </summary>
    private sealed class DesiredGrid
    {
        public List<MediaCardViewModel> CardList { get; } = new();
        public List<CompareRowViewModel> RowList { get; } = new();
    }

    /// <summary>
    /// 按投放箱路径 / 对象键恢复选区与焦点；已撤下的条目跳过。
    /// </summary>
    private void RestoreCardSelection(IReadOnlyList<string> selectedKeyList, string? focusKey)
    {
        var cardDict = new Dictionary<string, MediaCardViewModel>(StringComparer.Ordinal);
        foreach (var card in Cards)
        {
            var key = card.CardKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                cardDict[key] = card;
            }
        }

        SelectedCardList.Clear();
        foreach (var key in selectedKeyList)
        {
            if (cardDict.TryGetValue(key, out var card) && !SelectedCardList.Contains(card))
            {
                SelectedCardList.Add(card);
            }
        }

        MediaCardViewModel? focus = null;
        if (!string.IsNullOrWhiteSpace(focusKey) && cardDict.TryGetValue(focusKey, out var focusCard))
        {
            focus = focusCard;
        }
        else
        {
            focus = SelectedCardList.FirstOrDefault();
        }

        foreach (var card in Cards)
        {
            card.IsSelected = SelectedCardList.Contains(card);
        }

        FocusCard = focus;
    }

    /// <summary>
    /// 按可视区与前瞻页请求缩略图；滚动时继续补后面的。
    /// </summary>
    public void RequestVisibleThumbnails(int startIndex, int count)
    {
        mLoadStart = Math.Max(0, startIndex);
        mLoadCount = Math.Max(count, EstimateTwoPageCount());
        if (mThumbCts == null)
        {
            mThumbCts = new CancellationTokenSource();
        }

        _ = LoadVisibleThumbnailsAsync(mThumbCts.Token);
    }

    /// <summary>
    /// 按当前栏目 / 项目刷新未脱敏与无 src 警告条。
    /// </summary>
    private void RefreshNotice()
    {
        if (mSession == null || mSelectedNav == null)
        {
            NoticeText = "";
            Raise(nameof(HasNotice));
            return;
        }

        var unsignedCount = Cards.Count(card => card.Stage != null && card.IsUnsigned);
        NoticeText = CatalogNotice.ComposeBar(
            mSession,
            mSelectedNav.Channel.Key,
            mSelectedNav.Work?.Id,
            unsignedCount);
        Raise(nameof(HasNotice));
    }

    /// <summary>
    /// 当前脱敏筛下可见的网格卡片，顺序与虚拟化下标一致。
    /// </summary>
    public List<MediaCardViewModel> VisibleCardList()
    {
        return EnumerateView<MediaCardViewModel>(Cards);
    }

    /// <summary>
    /// 当前脱敏筛下可见的对照行。
    /// </summary>
    public List<CompareRowViewModel> VisibleCompareRowList()
    {
        return EnumerateView<CompareRowViewModel>(CompareRows);
    }

    /// <summary>
    /// 按可见对照行走解码，避免筛掉行后仍按下标去解隐藏行。
    /// </summary>
    public void RequestVisibleCompareThumbnails(int firstRow, int rowCount)
    {
        var rowList = VisibleCompareRowList();
        var start = Math.Max(0, firstRow);
        var cardList = new List<MediaCardViewModel>();
        for (var i = start; i < rowList.Count && cardList.Count < Math.Max(rowCount, 1) * 2; i++)
        {
            if (rowList[i].Left != null)
            {
                cardList.Add(rowList[i].Left!);
            }

            if (rowList[i].Right != null)
            {
                cardList.Add(rowList[i].Right!);
            }
        }

        if (cardList.Count == 0)
        {
            return;
        }

        var index = Cards.IndexOf(cardList[0]);
        mLoadStart = Math.Max(0, index);
        mLoadCount = Math.Max(cardList.Count, 1);
        if (mThumbCts == null)
        {
            mThumbCts = new CancellationTokenSource();
        }

        _ = LoadVisibleThumbnailsAsync(mThumbCts.Token, cardList);
    }

    /// <summary>
    /// 切换脱敏筛后只刷新视图，不重建卡片，以免丢掉标记与已解码缩略图。
    /// </summary>
    private void RefreshSenseFilter()
    {
        CollectionViewSource.GetDefaultView(Cards)?.Refresh();
        CollectionViewSource.GetDefaultView(CompareRows)?.Refresh();
        var visibleList = VisibleCardList();
        if (mFocusCard != null && !visibleList.Contains(mFocusCard))
        {
            SelectedCardList.Clear();
            FocusCard = visibleList.FirstOrDefault();
            if (FocusCard != null)
            {
                SelectedCardList.Add(FocusCard);
            }

            foreach (var card in Cards)
            {
                card.IsSelected = FocusCard != null && card == FocusCard;
            }

            if (mIsLightboxOpen && FocusCard == null)
            {
                CloseLightbox();
            }
        }

        Raise(nameof(LightboxCaption));
        mThumbCts?.Cancel();
        mThumbCts = new CancellationTokenSource();
        RequestViewportThumbnails();

        ApplyIdleStatus();
        RaiseMarkCommands();
    }

    /// <summary>
    /// 施工图栏目按脱敏筛收窄；对照行本身不进网格，始终交给对照筛。
    /// </summary>
    private bool MatchesVisibleCard(object obj)
    {
        if (obj is not MediaCardViewModel card)
        {
            return false;
        }

        if (IsComparePane || !IsSenseFilterEnabled)
        {
            return true;
        }

        if (card.Stage != null)
        {
            return CatalogNotice.MatchesSense(card.Stage, mSelectedSenseFilter.Kind);
        }

        return card.Site == null || CatalogNotice.MatchesSense(card.Site, mSelectedSenseFilter.Kind);
    }

    /// <summary>
    /// 对照行任一侧命中即保留。
    /// </summary>
    private bool MatchesVisibleCompareRow(object obj)
    {
        if (obj is not CompareRowViewModel row)
        {
            return false;
        }

        return !IsSenseFilterEnabled || CatalogNotice.MatchesSense(row.Row, mSelectedSenseFilter.Kind);
    }

    /// <summary>
    /// 读取集合的默认视图当前项。
    /// </summary>
    private static List<T> EnumerateView<T>(ObservableCollection<T> source)
    {
        if (CollectionViewSource.GetDefaultView(source) is ICollectionView view)
        {
            return view.Cast<T>().ToList();
        }

        return source.ToList();
    }

    /// <summary>
    /// 作品范围内按对象键、文件夹猜测或已认领夹收入投放箱。
    /// </summary>
    private bool StageMatchesWork(StageItem item, string workId)
    {
        return StageWorkScope.BelongsToWork(mSession, item, workId);
    }

    /// <summary>
    /// 当前导航节点是否应收下该投放箱条目。
    /// </summary>
    private bool StageMatchesNav(StageItem item, string? workId, bool unregisteredGroup)
    {
        if (unregisteredGroup)
        {
            return UnregisteredWorkDiscovery.IsUnregisteredGuess(item, mSession?.Works);
        }

        return workId == null || StageMatchesWork(item, workId);
    }

    /// <summary>
    /// 已选中的已入编摄影项目。不足两个时为空，网格仍跟焦点。
    /// </summary>
    private IReadOnlyList<NavGridScope.PhotoWorkRef> PhotoProjectsForGrid()
    {
        var selected = new List<NavNodeViewModel>();
        NavTreeSync.CollectSelected(NavNodes, selected);
        var picks = new List<NavGridScope.NavWorkPick>(selected.Count);
        foreach (var node in selected)
        {
            if (node.Work == null)
            {
                continue;
            }

            picks.Add(new NavGridScope.NavWorkPick(node.Work.Channel, node.Work.Id, !node.Work.IsUnregistered));
        }

        return NavGridScope.PhotoProjectsForGrid(picks);
    }

    /// <summary>
    /// 多选摄影项目时的网格缓存键。不是这种选区时为空。
    /// </summary>
    private string? PhotoMultiCacheStamp()
    {
        var years = SelectedPhotoYearNodes();
        if (years.Count >= 2)
        {
            return "years:" + string.Join("|", years.Select(node => node.Channel.Key + ":" + node.YearFolder));
        }

        var works = PhotoProjectsForGrid();
        if (works.Count >= 2)
        {
            return "multi:" + string.Join("|", works.Select(item => item.Channel + ":" + item.WorkId));
        }

        var channels = SelectedChannelRoots();
        if (channels.Count >= 2)
        {
            return "channels:" + string.Join("|", channels.Select(node => node.Channel.Key));
        }

        return null;
    }

    /// <summary>
    /// 已选中的摄影年份层。不足两个时为空。
    /// </summary>
    private IReadOnlyList<NavNodeViewModel> SelectedPhotoYearNodes()
    {
        var selected = new List<NavNodeViewModel>();
        NavTreeSync.CollectSelected(NavNodes, selected);
        var picks = new List<NavGridScope.YearRef>();
        foreach (var node in selected)
        {
            if (!node.IsYearGroup || string.IsNullOrWhiteSpace(node.YearFolder))
            {
                continue;
            }

            picks.Add(new NavGridScope.YearRef(node.Channel.Key, node.YearFolder));
        }

        var kept = NavGridScope.YearsForGrid(picks);
        if (kept.Count < 2)
        {
            return Array.Empty<NavNodeViewModel>();
        }

        var keptSet = new HashSet<string>(
            kept.Select(item => item.Channel + "\n" + item.Year),
            StringComparer.Ordinal);
        return selected
            .Where(node => node.IsYearGroup
                && keptSet.Contains(node.Channel.Key + "\n" + node.YearFolder))
            .ToList();
    }

    /// <summary>
    /// 按选中的摄影年份铺这些年下全部项目的资源。
    /// </summary>
    private void AppendPhotoYearGrid(DesiredGrid desired, IReadOnlyList<NavNodeViewModel> years)
    {
        var session = mSession;
        if (session == null)
        {
            return;
        }

        if (IsComparePane)
        {
            var pinList = PinStore.Load(session.Profile.ProfilePath);
            foreach (var year in years)
            {
                foreach (var workId in ChildWorkIds(year))
                {
                    foreach (var row in ComparePairing.Build(session, year.Channel.Key, workId, pinList))
                    {
                        var left = row.Stage == null ? null : CreateCard(row.Stage, CardGridFill.Current.ReadStage(row.Stage));
                        var right = row.Site == null ? null : CreateCard(row.Site, CardGridFill.Current.ReadSite(row.Site));
                        if (left != null)
                        {
                            desired.CardList.Add(left);
                        }

                        if (right != null)
                        {
                            desired.CardList.Add(right);
                        }

                        desired.RowList.Add(new CompareRowViewModel(row, left, right));
                    }
                }
            }

            return;
        }

        if (IsStagePane)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in session.StageItems)
            {
                var hit = false;
                foreach (var year in years)
                {
                    if (item.ChannelKey == year.Channel.Key
                        && StageWorkFolder.TryReadYearFolder(year.Channel, item.StageFolderGuess, out var itemYear)
                        && itemYear == year.YearFolder)
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    continue;
                }

                var rel = item.StageRel;
                if (!string.IsNullOrWhiteSpace(rel) && !seen.Add(item.ChannelKey + "\n" + rel))
                {
                    continue;
                }

                desired.CardList.Add(CreateCard(item, CardGridFill.Current.ReadStage(item)));
            }

            return;
        }

        var workKeySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var year in years)
        {
            foreach (var workId in ChildWorkIds(year))
            {
                workKeySet.Add(year.Channel.Key + "\n" + workId);
            }
        }

        AppendOrderedSiteCards(desired, session.SiteItems.Where(item =>
            !string.IsNullOrWhiteSpace(item.WorkId)
            && workKeySet.Contains(item.ChannelKey + "\n" + item.WorkId)));
    }

    /// <summary>
    /// 已选中的栏目根。不足两个时为空，网格仍跟焦点。
    /// </summary>
    private IReadOnlyList<NavNodeViewModel> SelectedChannelRoots()
    {
        var selected = new List<NavNodeViewModel>();
        NavTreeSync.CollectSelected(NavNodes, selected);
        var list = new List<NavNodeViewModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in selected)
        {
            if (node.Work != null || node.IsYearGroup || node.IsUnregisteredGroup)
            {
                continue;
            }

            if (!seen.Add(node.Channel.Key))
            {
                continue;
            }

            list.Add(node);
        }

        return list.Count >= 2 ? list : Array.Empty<NavNodeViewModel>();
    }

    /// <summary>
    /// 按选中的栏目铺投放箱、站点或对照。站点与投放箱同走路径序。
    /// </summary>
    private void AppendChannelGrid(DesiredGrid desired, IReadOnlyList<NavNodeViewModel> channels)
    {
        var session = mSession;
        if (session == null)
        {
            return;
        }

        var keySet = new HashSet<string>(
            channels.Select(node => node.Channel.Key),
            StringComparer.Ordinal);
        if (IsComparePane)
        {
            var pinList = PinStore.Load(session.Profile.ProfilePath);
            var orderedKeyList = new List<string>();
            var seenChannel = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in session.StageItems)
            {
                if (keySet.Contains(item.ChannelKey) && seenChannel.Add(item.ChannelKey))
                {
                    orderedKeyList.Add(item.ChannelKey);
                }
            }

            foreach (var node in channels)
            {
                if (seenChannel.Add(node.Channel.Key))
                {
                    orderedKeyList.Add(node.Channel.Key);
                }
            }

            foreach (var channelKey in orderedKeyList)
            {
                foreach (var row in ComparePairing.Build(session, channelKey, null, pinList))
                {
                    var left = row.Stage == null ? null : CreateCard(row.Stage, CardGridFill.Current.ReadStage(row.Stage));
                    var right = row.Site == null ? null : CreateCard(row.Site, CardGridFill.Current.ReadSite(row.Site));
                    if (left != null)
                    {
                        desired.CardList.Add(left);
                    }

                    if (right != null)
                    {
                        desired.CardList.Add(right);
                    }

                    desired.RowList.Add(new CompareRowViewModel(row, left, right));
                }
            }

            return;
        }

        if (IsStagePane)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in session.StageItems)
            {
                if (!keySet.Contains(item.ChannelKey))
                {
                    continue;
                }

                var rel = item.StageRel;
                if (!string.IsNullOrWhiteSpace(rel) && !seen.Add(item.ChannelKey + "\n" + rel))
                {
                    continue;
                }

                desired.CardList.Add(CreateCard(item, CardGridFill.Current.ReadStage(item)));
            }

            return;
        }

        AppendOrderedSiteCards(desired, session.SiteItems.Where(item => keySet.Contains(item.ChannelKey)));
    }

    /// <summary>
    /// 站点卡片按投放箱路径序铺进网格。
    /// </summary>
    private void AppendOrderedSiteCards(DesiredGrid desired, IEnumerable<SiteItem> siteItems)
    {
        foreach (var item in SiteGridOrder.SortLikeStage(siteItems.ToList()))
        {
            desired.CardList.Add(CreateCard(item, CardGridFill.Current.ReadSite(item)));
        }
    }

    /// <summary>
    /// 当前左侧范围内要汇总标签的已入编摄影作品。
    /// </summary>
    private List<WorkCatalogItem> TagScopeWorks()
    {
        var list = new List<WorkCatalogItem>();
        if (mSession == null || mSelectedNav == null || !IsPhotoTagNav(mSelectedNav))
        {
            return list;
        }

        if (mSelectedNav.Work is { IsUnregistered: false, SourceKind: "works" } work)
        {
            list.Add(work);
            return list;
        }

        IEnumerable<string> idList = mSelectedNav.IsYearGroup
            ? ChildWorkIds(mSelectedNav)
            : mSession.Works
                .Where(item =>
                    !item.IsUnregistered
                    && item.SourceKind == "works"
                    && string.Equals(item.Channel, mSelectedNav.Channel.Key, StringComparison.Ordinal))
                .Select(item => item.Id);
        var idSet = new HashSet<string>(idList, StringComparer.Ordinal);
        foreach (var item in mSession.Works)
        {
            if (!item.IsUnregistered
                && item.SourceKind == "works"
                && string.Equals(item.Channel, mSelectedNav.Channel.Key, StringComparison.Ordinal)
                && idSet.Contains(item.Id))
            {
                list.Add(item);
            }
        }

        return list;
    }

    /// <summary>
    /// 标签汇总的范围键。换栏目、年份或项目时清掉点中的芯片。
    /// </summary>
    private string? TagScopeId()
    {
        if (mSelectedNav == null || !IsPhotoTagNav(mSelectedNav))
        {
            return null;
        }

        if (mSelectedNav.Work is { IsUnregistered: false, SourceKind: "works" } work)
        {
            return "work:" + work.Channel + ":" + work.Id;
        }

        if (mSelectedNav.IsYearGroup || IsSelectedChannelRoot())
        {
            return mSelectedNav.NavKey;
        }

        return null;
    }

    /// <summary>
    /// 摄影栏目、其年份层或已入编项目才出项目标签汇总。
    /// </summary>
    private static bool IsPhotoTagNav(NavNodeViewModel node)
    {
        return PhotoFactRules.RequiresFacts(node.Channel.Key)
            || WorkRegisterRules.IsPhotoFolderChannel(node.Channel.Key);
    }

    /// <summary>
    /// 按选中的摄影项目依次铺投放箱、站点或对照。
    /// </summary>
    private void AppendPhotoWorkGrid(DesiredGrid desired, IReadOnlyList<NavGridScope.PhotoWorkRef> works)
    {
        var session = mSession;
        if (session == null)
        {
            return;
        }

        if (IsComparePane)
        {
            var pinList = PinStore.Load(session.Profile.ProfilePath);
            foreach (var work in works)
            {
                foreach (var row in ComparePairing.Build(session, work.Channel, work.WorkId, pinList))
                {
                    var left = row.Stage == null ? null : CreateCard(row.Stage, CardGridFill.Current.ReadStage(row.Stage));
                    var right = row.Site == null ? null : CreateCard(row.Site, CardGridFill.Current.ReadSite(row.Site));
                    if (left != null)
                    {
                        desired.CardList.Add(left);
                    }

                    if (right != null)
                    {
                        desired.CardList.Add(right);
                    }

                    desired.RowList.Add(new CompareRowViewModel(row, left, right));
                }
            }

            return;
        }

        if (IsStagePane)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var work in works)
            {
                foreach (var item in StageWorkScope.CandidatesForWork(session, work.Channel, work.WorkId))
                {
                    if (!StageMatchesNav(item, work.WorkId, false))
                    {
                        continue;
                    }

                    var rel = item.StageRel;
                    if (!string.IsNullOrWhiteSpace(rel) && !seen.Add(work.Channel + "\n" + rel))
                    {
                        continue;
                    }

                    desired.CardList.Add(CreateCard(item, CardGridFill.Current.ReadStage(item)));
                }
            }

            return;
        }

        foreach (var work in works)
        {
            AppendOrderedSiteCards(desired, session.SiteItems.Where(item =>
                item.ChannelKey == work.Channel && item.WorkId == work.WorkId));
        }
    }

    /// <summary>
    /// 年份行直接挂着的作品 id。
    /// </summary>
    private static HashSet<string> ChildWorkIds(NavNodeViewModel node)
    {
        return node.Children
            .Select(child => child.Work?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// 对照视图按作品 id 展开；未登记分组只列各未登记夹，栏目总览保持 null。
    /// </summary>
    private IEnumerable<string?> EnumerateCompareWorkIds(string channelKey, string? workId, bool unregisteredGroup)
    {
        if (unregisteredGroup)
        {
            return UnregisteredWorkDiscovery.UnregisteredInChannel(mSession!.Works, channelKey)
                .Select(work => (string?)work.Id);
        }

        return new string?[] { workId };
    }

    /// <summary>
    /// 缩放拖动结束后再重解码，避免每一档都打满解码队列。
    /// </summary>
    private void ScheduleThumbnailReload()
    {
        mThumbDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        mThumbDebounce.Tick -= OnThumbDebounceTick;
        mThumbDebounce.Tick += OnThumbDebounceTick;
        mThumbDebounce.Stop();
        mThumbDebounce.Start();
    }

    /// <summary>
    /// 按当前边长重新填充网格缩略图。
    /// </summary>
    private void OnThumbDebounceTick(object? sender, EventArgs e)
    {
        mThumbDebounce?.Stop();
        ForgetCachedThumbnails();
        mThumbCts?.Cancel();
        mThumbCts = new CancellationTokenSource();
        if (IsComparePane)
        {
            RequestVisibleCompareThumbnails(0, EstimateTwoPageCount());
        }
        else
        {
            _ = LoadVisibleThumbnailsAsync(mThumbCts.Token);
        }
    }

    /// <summary>
    /// 只解码当前窗口附近的缩略图。
    /// </summary>
    private async Task LoadVisibleThumbnailsAsync(
        CancellationToken token,
        IReadOnlyList<MediaCardViewModel>? explicitList = null)
    {
        var snapshotList = explicitList ?? VisibleCardList();
        var from = explicitList == null
            ? Math.Clamp(mLoadStart, 0, snapshotList.Count)
            : 0;
        var to = explicitList == null
            ? Math.Min(snapshotList.Count, from + Math.Max(mLoadCount, 1))
            : snapshotList.Count;
        var painted = 0;
        for (var i = from; i < to; i++)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var card = snapshotList[i];
            if (card.Thumbnail != null || string.IsNullOrWhiteSpace(card.PreviewPath))
            {
                continue;
            }

            var image = await mThumbnails.LoadAsync(
                card.PreviewPath,
                mThumbnailPx,
                token,
                CoverIdentity(card)).ConfigureAwait(true);
            if (image != null && Cards.Contains(card))
            {
                card.Thumbnail = image;
                painted++;
                if (mIsCatalogLoading && painted % 4 == 0)
                {
                    await YieldRenderAsync().ConfigureAwait(true);
                }
            }
        }
    }

    /// <summary>
    /// 让布局与绘制先于后续状态变更执行。
    /// </summary>
    private static async Task YieldRenderAsync()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 清掉当前网格与已记住表面的缩略图绑定，磁盘与内存缓存另清。
    /// </summary>
    private void ForgetCachedThumbnails()
    {
        foreach (var card in Cards)
        {
            card.Thumbnail = null;
        }

        mGridCache.ForEachCard(card => card.Thumbnail = null);
    }

    /// <summary>
    /// 按当前缩略图边长估算两屏数量。
    /// </summary>
    private int EstimateTwoPageCount()
    {
        var cardWidth = Math.Max(120, mThumbnailPx + 20);
        var cardHeight = Math.Max(80, CardImageHeight + 56);
        var columns = Math.Max(1, 800 / cardWidth);
        var rows = Math.Max(1, (int)Math.Ceiling(560 / cardHeight));
        return Math.Clamp(columns * rows * 2, 12, 80);
    }

    /// <summary>
    /// 刷新右侧检视，并异步载入清晰预览。
    /// </summary>
    private void UpdateInspector()
    {
        var card = mFocusCard;
        mInspectorCts?.Cancel();
        if (card == null)
        {
            InspectorMeta = mSelectedNav?.IsUnregistered == true ? "未入编，不可上页。" : "";
            PreviewHint = mSelectedNav?.IsUnregistered == true
                ? "未入编，不可上页。先登记或改选已有作品。"
                : "选择一张图查看详情。";
            InspectorImage = null;
            ResetVideoInspector();
            return;
        }

        if (card.Stage != null)
        {
            var stage = card.Stage;
            var unregistered = UnregisteredWorkDiscovery.IsUnregisteredGuess(stage, mSession?.Works);
            InspectorMeta = FormatMeta(
                $"投放路径 {stage.StageRel}",
                $"栏目 {stage.ChannelKey}",
                string.IsNullOrWhiteSpace(stage.MatchedObject) ? "未配对对象键" : "对象 " + stage.MatchedObject,
                "台账 " + (stage.IsPendingWithdraw
                    ? PublishStatus.Withdrawn
                    : PublishStatus.LedgerLine(stage.LedgerStatus)),
                stage.IsStock ? PublishStatus.Stock : (stage.WorkIdGuess ?? ""),
                unregistered ? "未入编，不可上页。" : "",
                card.UsesWebPreview ? "预览 网页图" : "预览 原片",
                card.IsVideo ? "视频 双击外部播放" : "",
                FileSizeFormat.Format(stage.Length));
            PreviewHint = unregistered
                ? "未入编，不可上页。先登记或改选已有作品。"
                : card.IsVideo
                    ? "拖进度条看帧，设为封面后清 thumbs 仍保留。双击仍走 PotPlayer。"
                    : (card.PreviewPath ?? stage.FullPath);
        }
        else if (card.Site != null)
        {
            var site = card.Site;
            InspectorMeta = FormatMeta(
                SiteCardCaption.Format(site),
                $"作品 {site.WorkTitle} ({site.WorkId})",
                "对象 " + site.ObjectKey,
                "引用 " + site.ReferenceCount,
                "台账 " + PublishStatus.LedgerLine(site.LedgerStatus),
                card.UsesWebPreview ? "预览 网页图" : "预览 原片",
                card.IsVideo ? "视频 双击外部播放" : "",
                FormatExistingFileSize(site.FullPath),
                site.FullPath ?? "正式位文件缺失");
            PreviewHint = card.IsVideo
                ? "拖进度条看帧，设为封面后清 thumbs 仍保留。双击仍走 PotPlayer。"
                : (card.PreviewPath ?? site.ObjectKey);
        }

        mInspectorCts = new CancellationTokenSource();
        if (card.IsVideo)
        {
            ShowVideoScrub = true;
            _ = LoadVideoInspectorAsync(card, mInspectorCts.Token);
            return;
        }

        ResetVideoInspector();
        _ = LoadInspectorImageAsync(card, mInspectorCts.Token);
    }

    /// <summary>
    /// 检视栏按网页图优先解码清晰图，原片则限制最大边以免卡顿。
    /// </summary>
    private async Task LoadInspectorImageAsync(MediaCardViewModel card, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(card.PreviewPath))
        {
            InspectorImage = null;
            return;
        }

        var decodePx = card.UsesWebPreview ? 0 : 1600;
        var image = await mThumbnails.LoadAsync(
            card.PreviewPath,
            decodePx,
            token,
            CoverIdentity(card)).ConfigureAwait(true);
        if (!token.IsCancellationRequested)
        {
            InspectorImage = image;
        }
    }

    /// <summary>
    /// 视频检视：先出手设封面或默认帧，再打开静音取帧。
    /// </summary>
    private async Task LoadVideoInspectorAsync(MediaCardViewModel card, CancellationToken token)
    {
        var path = card.PreviewPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            InspectorImage = null;
            CanScrubVideo = false;
            VideoDurationSec = 0;
            return;
        }

        await LoadInspectorImageAsync(card, token).ConfigureAwait(true);
        if (token.IsCancellationRequested)
        {
            return;
        }

        var opened = await mVideoGrabber.OpenAsync(path, token).ConfigureAwait(true);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (!opened)
        {
            CanScrubVideo = false;
            VideoDurationSec = 0;
            return;
        }

        CanScrubVideo = true;
        VideoDurationSec = mVideoGrabber.DurationSec;
        var identity = CoverIdentity(card);
        var info = new FileInfo(path);
        var saved = identity == null
            ? null
            : VideoCoverStore.TryRead(identity, info.Length, info.LastWriteTimeUtc);
        var position = saved?.Record.PositionSec
            ?? VideoCoverRules.DefaultPositionSec(mVideoGrabber.DurationSec);
        mIgnoreVideoPosition = true;
        VideoPositionSec = position;
        mIgnoreVideoPosition = false;
        var frame = await mVideoGrabber.GrabAsync(position, token).ConfigureAwait(true);
        if (!token.IsCancellationRequested && frame != null)
        {
            InspectorImage = frame;
        }
    }

    /// <summary>
    /// 拖进度条后延迟取帧，避免连续拖动卡死。
    /// </summary>
    private void QueueVideoScrub()
    {
        mVideoScrubCts?.Cancel();
        mVideoScrubDebounce?.Stop();
        mVideoScrubDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        mVideoScrubDebounce.Tick -= OnVideoScrubTick;
        mVideoScrubDebounce.Tick += OnVideoScrubTick;
        mVideoScrubDebounce.Start();
    }

    /// <summary>
    /// 按当前进度抓一帧到检视。
    /// </summary>
    private async void OnVideoScrubTick(object? sender, EventArgs e)
    {
        mVideoScrubDebounce?.Stop();
        if (!mCanScrubVideo || mFocusCard == null)
        {
            return;
        }

        mVideoScrubCts?.Cancel();
        mVideoScrubCts?.Dispose();
        mVideoScrubCts = CancellationTokenSource.CreateLinkedTokenSource(
            mInspectorCts?.Token ?? CancellationToken.None);
        var token = mVideoScrubCts.Token;
        try
        {
            var frame = await mVideoGrabber.GrabAsync(mVideoPositionSec, token).ConfigureAwait(true);
            if (frame != null && !token.IsCancellationRequested)
            {
                InspectorImage = frame;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// 把当前检视帧写入封面缓存并刷新卡片。
    /// </summary>
    private void SetVideoCover()
    {
        var card = mFocusCard;
        var path = card?.PreviewPath;
        var identity = card == null ? null : CoverIdentity(card);
        if (card == null
            || !card.IsVideo
            || string.IsNullOrWhiteSpace(path)
            || !File.Exists(path)
            || string.IsNullOrWhiteSpace(identity)
            || mInspectorImage is not BitmapSource bitmap)
        {
            StatusText = "当前帧无法写成封面。";
            return;
        }

        var info = new FileInfo(path);
        var bytes = VideoCoverEncoder.ToJpeg(bitmap);
        VideoCoverStore.Write(
            identity,
            info.Length,
            info.LastWriteTimeUtc,
            mVideoPositionSec,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            bytes,
            webp: false);
        mThumbnails.Forget(path);
        card.Thumbnail = null;
        _ = ReloadCardThumbnailAsync(card);
        StatusText = "已将当前帧设为封面。";
    }

    /// <summary>
    /// 有视频帧且能停在指针位置时才允许设封面。
    /// </summary>
    private bool CanSetVideoCover()
    {
        return mShowVideoScrub && mCanScrubVideo && mInspectorImage != null;
    }

    /// <summary>
    /// 改封面后只重解这一张卡片。
    /// </summary>
    private async Task ReloadCardThumbnailAsync(MediaCardViewModel card)
    {
        if (string.IsNullOrWhiteSpace(card.PreviewPath))
        {
            return;
        }

        var image = await mThumbnails.LoadAsync(
            card.PreviewPath,
            mThumbnailPx,
            CancellationToken.None,
            CoverIdentity(card)).ConfigureAwait(true);
        if (Cards.Contains(card) || CompareRows.Any(row => row.Left == card || row.Right == card))
        {
            card.Thumbnail = image;
        }
    }

    /// <summary>
    /// 切到图像或空焦点时收起视频检视。
    /// </summary>
    private void ResetVideoInspector()
    {
        mVideoScrubDebounce?.Stop();
        mVideoScrubCts?.Cancel();
        mVideoGrabber.Close();
        ShowVideoScrub = false;
        CanScrubVideo = false;
        mIgnoreVideoPosition = true;
        VideoDurationSec = 0;
        VideoPositionSec = 0;
        VideoPositionCaption = "";
        mIgnoreVideoPosition = false;
        SetCoverCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 封面缓存键：投放路径，站点条目用对象键。
    /// </summary>
    private static string? CoverIdentity(MediaCardViewModel card)
    {
        if (!string.IsNullOrWhiteSpace(card.Stage?.StageRel))
        {
            return card.Stage.StageRel;
        }

        return string.IsNullOrWhiteSpace(card.Site?.ObjectKey) ? null : card.Site.ObjectKey;
    }

    /// <summary>
    /// 进度条文案，与批次进度条区分。
    /// </summary>
    private static string FormatVideoPosition(double positionSec, double durationSec)
    {
        if (durationSec <= 0)
        {
            return "";
        }

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{positionSec:0.0}s / {durationSec:0.0}s");
    }

    /// <summary>
    /// 多行元数据。
    /// </summary>
    private static string FormatMeta(params string[] lines)
    {
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// 已存在文件的大小；缺失则写明。
    /// </summary>
    private static string FormatExistingFileSize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "文件缺失";
        }

        return FileSizeFormat.Format(new FileInfo(path).Length);
    }

    /// <summary>
    /// 弹出预演文本。
    /// </summary>
    private void ShowPreview()
    {
        var report = BuildPreview();
        if (report == null)
        {
            return;
        }

        TextDialog.Show("预演", PromptRenderer.RenderPreview(report));
    }

    /// <summary>
    /// 复制提示词到剪贴板。
    /// </summary>
    private void CopyPrompt()
    {
        var report = BuildPreview();
        if (report == null)
        {
            return;
        }

        var text = PromptRenderer.RenderPrompt(report);
        Clipboard.SetText(text);
        var outDir = Path.Combine(ToolPaths.FindToolRoot() ?? Path.GetTempPath(), "out");
        Directory.CreateDirectory(outDir);
        var file = Path.Combine(outDir, "intent-prompt.md");
        File.WriteAllText(file, text, JsonUtil.Utf8NoBom);
        StatusText = report.HasHardError
            ? "已复制提示词（含拒绝项）。"
            : "已复制提示词，并写入工具目录 out/intent-prompt.md。";
    }

    /// <summary>
    /// 机械模式执行投放箱回收与站点隐藏。
    /// </summary>
    private async void ExecuteDirect()
    {
        if (mSession == null)
        {
            return;
        }

        var report = BuildPreview();
        if (report == null)
        {
            return;
        }

        if (report.HasHardError)
        {
            TextDialog.Show("无法执行", PromptRenderer.RenderPreview(report));
            return;
        }

        var recycleCount = report.Document.Items.Count(item => item.Intent == MediaIntentCodes.StageRecycle);
        var ingestSkipCount = CountRedundantIngest(report);
        var ingestCount = report.Document.Items.Count(item => item.Intent == MediaIntentCodes.StageIngest)
            - ingestSkipCount;
        var hideSkipCount = CountRedundantHide(report);
        var hideCount = report.Document.Items.Count(item => item.Intent == MediaIntentCodes.SiteHide)
            - hideSkipCount;
        var restoreSkipCount = CountRedundantRestore(report);
        var restoreCount = report.Document.Items.Count(item => item.Intent == MediaIntentCodes.SiteRestore)
            - restoreSkipCount;
        var withdrawCount = report.Document.Items.Count(item => item.Intent == MediaIntentCodes.SiteWithdraw);
        var copyCount = report.Document.Items.Count(item => item.Intent == MediaIntentCodes.CopyUpdate);
        var reorderCount = report.Document.Items.Count(item => item.Intent == MediaIntentCodes.MediaReorder);
        var starCount = report.Document.Items.Count(StarDraftCompiler.IsContentWrite);
        if (recycleCount + ingestCount + hideCount + restoreCount + withdrawCount + copyCount + reorderCount + starCount == 0)
        {
            if (ingestSkipCount + hideSkipCount + restoreSkipCount > 0)
            {
                ClearExecutedMarks(MarkDraftStore.KeysOf(report.Document));
            }

            var skipText = hideSkipCount > 0
                ? $"{hideSkipCount} 张已隐藏，无需再隐藏，已跳过。"
                : restoreSkipCount > 0
                    ? $"{restoreSkipCount} 张已在页上，无需再上页，已跳过。"
                    : ingestSkipCount > 0
                        ? $"{ingestSkipCount} 张已上页，无需再上页，已跳过。"
                        : "没有可执行的标记或文案草稿。";
            TextDialog.Show("没有可执行的步骤", skipText);
            ApplyIdleStatus();
            return;
        }

        var noteIngestList = report.Document.Items
            .Where(item => item.Intent == MediaIntentCodes.StageIngest && NoteRules.IsNotesChannel(item.Channel))
            .ToList();
        var requireAck = recycleCount > 0 || withdrawCount > 0;
        if (!ConfirmIntentDialog.Show(
                BuildExecuteConfirm(
                    recycleCount,
                    ingestCount,
                    hideCount,
                    withdrawCount,
                    copyCount,
                    hideSkipCount,
                    restoreCount,
                    ingestSkipCount,
                    reorderCount,
                    starCount),
                requireAck))
        {
            return;
        }

        if (!PublishMarkedNotes(noteIngestList, out var noteError))
        {
            TextDialog.Show("无法执行", noteError);
            return;
        }

        report.Document.Items.RemoveAll(item => noteIngestList.Contains(item));
        var outDir = Path.Combine(ToolPaths.FindToolRoot() ?? Path.GetTempPath(), "out");
        Directory.CreateDirectory(outDir);
        var intentPath = Path.Combine(outDir, "intent.json");
        report.Document.Mode = "direct";
        File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(report.Document), JsonUtil.Utf8NoBom);

        // 压图 / 压码会改写 StageRel，必须先记下本批原键。上页成功条已在执行器内清标。
        var executedKeyList = MarkDraftStore.KeysOf(report.Document).ToList();
        executedKeyList.AddRange(noteIngestList
            .Select(item => item.StageRel)
            .Where(rel => !string.IsNullOrWhiteSpace(rel))
            .Select(rel => MarkDraftStore.StageKey(rel!)));
        var copyLineList = ExecuteResultComposer.BuildCopyLines(mSession, report.Document);
        var resultCounts = new ExecuteResultCounts(
            ingestCount,
            0,
            restoreCount,
            0,
            hideCount,
            0,
            withdrawCount,
            0,
            recycleCount,
            0,
            copyCount,
            0,
            starCount,
            0);

        mIsExecuting = true;
        ExecuteCommand.RaiseCanExecute();
        PublishCommand.RaiseCanExecute();
        StatusText = "正在执行…";
        BatchRunResult result;
        try
        {
            result = ProgressDialog.Run(
                "正在执行",
                progress => BatchExecutor.Run(mSession, report.Document, intentPath, progress));
        }
        finally
        {
            mIsExecuting = false;
            ExecuteCommand.RaiseCanExecute();
            PublishCommand.RaiseCanExecute();
        }

        resultCounts = resultCounts with
        {
            IngestOk = result.IngestOk + noteIngestList.Count,
            RestoreOk = result.RestoreOk,
            HideOk = result.HideOk,
            WithdrawOk = result.WithdrawOk,
            RecycleOk = result.RecycleOk,
            CopyOk = result.Ok ? copyCount : 0,
            StarOk = result.Ok ? starCount : 0
        };

        var profile = mSession.Profile;
        if (result.Ok)
        {
            AlignExecutedCopyDrafts(report.Document);
            AlignExecutedStarDrafts(report.Document);
            AlignExecutedReorderDrafts(report.Document);
            ClearExecutedMarks(executedKeyList);
        }

        await ReloadAsync(profile.ProfilePath, coverGrid: true).ConfigureAwait(true);
        if (!result.Ok)
        {
            TextDialog.Show(
                "执行失败，意图文件已保留",
                ExecuteResultComposer.BuildFailure(result, intentPath, copyLineList));
            ApplyIdleStatus();
            return;
        }

        if (result.Pending != null)
        {
            PendingPublishStore.MergeSave(result.Pending);
        }

        TextDialog.Show(
            "执行已完成",
            ExecuteResultComposer.BuildSuccess(result, resultCounts, copyLineList));
        ApplyIdleStatus();
        PublishCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 预演中已隐藏再隐藏、将跳过写盘的条数。
    /// </summary>
    private static int CountRedundantHide(PreviewReport report)
    {
        return report.Lines.Count(line =>
            MediaIntentCodes.FromCode(line.Item.Intent) == MediaIntent.SiteHide
            && IntentGate.IsRedundantHideDecision(line.Decision));
    }

    /// <summary>
    /// 预演中已在页上再恢复、将跳过写盘的条数。
    /// </summary>
    private static int CountRedundantRestore(PreviewReport report)
    {
        return report.Lines.Count(line =>
            MediaIntentCodes.FromCode(line.Item.Intent) == MediaIntent.SiteRestore
            && IntentGate.IsRedundantRestoreDecision(line.Decision));
    }

    /// <summary>
    /// 预演中已上页再上页、将跳过写盘的条数。
    /// </summary>
    private static int CountRedundantIngest(PreviewReport report)
    {
        return report.Lines.Count(line =>
            MediaIntentCodes.FromCode(line.Item.Intent) == MediaIntent.StageIngest
            && IntentGate.IsRedundantIngestDecision(line.Decision));
    }

    /// <summary>
    /// 机械执行前复述条数与破坏性后果。
    /// </summary>
    private static string BuildExecuteConfirm(
        int recycleCount,
        int ingestCount,
        int hideCount,
        int withdrawCount,
        int copyCount,
        int hideSkipCount = 0,
        int restoreCount = 0,
        int ingestSkipCount = 0,
        int reorderCount = 0,
        int starCount = 0)
    {
        var parts = new List<string>();
        if (recycleCount > 0)
        {
            parts.Add($"将把 {recycleCount} 个未上页文件移入本机回收站");
        }

        if (ingestCount > 0)
        {
            parts.Add($"将入库 {ingestCount} 条，中转站文件保留");
        }

        if (restoreCount > 0)
        {
            parts.Add($"将把 {restoreCount} 条已隐藏写回内容层，不新建键、不重新入库");
        }

        if (hideCount > 0)
        {
            parts.Add($"将从内容层去掉 {hideCount} 条引用并重排标签");
        }

        if (hideSkipCount > 0)
        {
            parts.Add($"另有 {hideSkipCount} 张已隐藏，将跳过");
        }

        if (ingestSkipCount > 0)
        {
            parts.Add($"另有 {ingestSkipCount} 张已上页，将跳过");
        }

        if (withdrawCount > 0)
        {
            parts.Add($"将去掉 {withdrawCount} 条引用，并记入待发布队列；中转站文件保留");
        }

        if (copyCount > 0)
        {
            parts.Add($"将把 {copyCount} 条文案写入内容层");
        }

        if (reorderCount > 0)
        {
            parts.Add($"将按站点页顺序重排 {reorderCount} 个作品的资源");
        }

        if (starCount > 0)
        {
            parts.Add($"将把 {starCount} 条星级写入内容层");
        }

        var text = string.Join("；", parts) + "。";
        if (ingestCount + hideCount + restoreCount + withdrawCount + copyCount + reorderCount + starCount > 0)
        {
            text += "本步只改本机，正式网页要再点「发布上线」才会更新。";
        }

        return text + "是否继续？";
    }

    /// <summary>
    /// 栏目加载完成后的底栏：张数或对照组数，加上队列门禁。
    /// </summary>
    private void ApplyIdleStatus()
    {
        var paneLine = IsComparePane
            ? $"对照 {VisibleCompareRowList().Count} 组。"
            : $"本栏 {VisibleCardList().Count} 张。";
        if (IsSenseFilterEnabled && mSelectedSenseFilter.Kind != SenseFilterKind.All)
        {
            paneLine = paneLine.TrimEnd('。') + $" · {mSelectedSenseFilter.Label}。";
        }

        StatusText = paneLine + " " + QueueStatusLine();
        PublishCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 当前工作区待发布队列的底栏句。
    /// </summary>
    private string QueueStatusLine()
    {
        if (mSession == null)
        {
            return "无待发布队列。";
        }

        return PendingPublishStore.StatusLine(mSession.Profile, PendingPublishStore.Load());
    }

    /// <summary>
    /// 工作区已加载且空闲即可点。无队列发静态包；缺凭证在点击后提示。
    /// </summary>
    private bool CanPublishOnline()
    {
        return !mIsPublishing && !mIsExecuting && mSession != null;
    }

    /// <summary>
    /// 机械执行可用：有批次、非提示词、且执行 / 发布未在进行。
    /// </summary>
    private bool CanExecuteDirect()
    {
        return !mIsExecuting
            && !mIsPublishing
            && HasRunnableBatch()
            && mMode == ExecutionMode.Direct;
    }

    /// <summary>
    /// 发布上线：确认后弹出进度窗，后台执行 deploy → 待撤 apply → purge。
    /// </summary>
    private async void PublishOnline()
    {
        if (mSession == null)
        {
            return;
        }

        if (!PendingPublishStore.HasDeployReady(mSession.Profile))
        {
            TextDialog.Show("无法发布", "本工作区缺少 .env.deploy。");
            StatusText = PendingPublishStore.StatusLine(mSession.Profile, PendingPublishStore.Load());
            PublishCommand.RaiseCanExecute();
            return;
        }

        var pending = PendingPublishStore.ResolveForPublish(mSession.Profile, PendingPublishStore.Load());

        var preview = PublishExecutor.RenderPreview(mSession.Profile, pending);
        if (PublishExecutor.ContainsPrune(pending) || PublishExecutor.CommandLineHasPrune(preview))
        {
            TextDialog.Show("无法发布", "参数含 prune-assets，已拒绝。");
            return;
        }

        if (!ConfirmIntentDialog.Show(
                preview,
                pending.WithdrawObjectList.Count > 0,
                "确认发布上线",
                "发布"))
        {
            return;
        }

        mIsPublishing = true;
        PublishCommand.RaiseCanExecute();
        StatusText = "正在发布上线…";
        var profile = mSession.Profile;
        var result = ProgressDialog.Run(
            "正在发布上线",
            progress => PublishExecutor.Run(profile, pending, progress));
        mIsPublishing = false;
        await ReloadAsync(profile.ProfilePath, coverGrid: true).ConfigureAwait(true);
        PublishCommand.RaiseCanExecute();
        var resultText = PublishExecutor.RenderResult(result);
        TextDialog.Show(resultText.Title, resultText.Body);
        if (!result.Ok)
        {
            StatusText = $"发布停在 {result.FailedStep}。运行日志见本机 SiteMediaStudio/run.log。";
            return;
        }

        NoteConfigStore.AppendModified(profile, DateTime.Now);

        StatusText = "已成功发布上线。"
            + (result.CompletedStepList.Count == 0 ? "" : " " + string.Join("；", result.CompletedStepList) + "。")
            + " "
            + QueueStatusLine();
    }

    /// <summary>
    /// 根据当前标记生成预演。
    /// </summary>
    private PreviewReport? BuildPreview()
    {
        if (mSession == null)
        {
            return null;
        }

        var markedList = CollectMarked();
        var document = IntentDocumentBuilder.Build(
            mSession,
            mMode,
            markedList,
            mIngestTargetWorkId);
        IntentDocumentBuilder.AppendCopy(document, CollectCopyItems());
        IntentDocumentBuilder.AppendStars(document, PeekStarItems());
        IntentDocumentBuilder.AppendReorder(document, CollectReorderItems());
        return PreviewReporter.BuildFromDocument(mSession, document);
    }

    /// <summary>
    /// 当前工作区脏草稿编成的文案意图；预演 / 执行前先落盘当前框。
    /// </summary>
    private IReadOnlyList<IntentItem> CollectCopyItems()
    {
        if (mSession == null)
        {
            return Array.Empty<IntentItem>();
        }

        CommitCopyDraft();
        return PeekCopyItems();
    }

    /// <summary>
    /// 只读检查脏草稿，不写盘、不刷新命令门禁。
    /// </summary>
    private IReadOnlyList<IntentItem> PeekCopyItems()
    {
        if (mSession == null)
        {
            return Array.Empty<IntentItem>();
        }

        return CopyDraftCompiler.ToItems(mSession, CopyDraftStore.Load());
    }

    /// <summary>
    /// 当前工作区脏排序草稿编成的意图。
    /// </summary>
    private IReadOnlyList<IntentItem> CollectReorderItems()
    {
        return PeekReorderItems();
    }

    /// <summary>
    /// 只读检查排序草稿。
    /// </summary>
    private IReadOnlyList<IntentItem> PeekReorderItems()
    {
        if (mSession == null)
        {
            return Array.Empty<IntentItem>();
        }

        return ReorderDraftCompiler.ToItems(mSession, ReorderDraftStore.Load());
    }

    /// <summary>
    /// 执行成功后删掉已写入内容层的排序草稿。
    /// </summary>
    private void AlignExecutedReorderDrafts(IntentDocument document)
    {
        if (mSession == null)
        {
            return;
        }

        var keyList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.MediaReorder)
            .Select(ReorderDraftCompiler.KeyOf)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToList();
        try
        {
            ReorderDraftStore.RemoveKeys(mSession.Profile, keyList);
        }
        catch (Exception)
        {
            // 草稿对齐失败不影响已写入的内容层。
        }
    }

    /// <summary>
    /// 执行成功后删掉已写入内容层的草稿条。
    /// </summary>
    private void AlignExecutedCopyDrafts(IntentDocument document)
    {
        if (mSession == null)
        {
            return;
        }

        var keyList = document.Items
            .Where(item => item.Intent == MediaIntentCodes.CopyUpdate)
            .Select(CopyDraftCompiler.KeyOf)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToList();
        try
        {
            CopyDraftStore.RemoveKeys(mSession.Profile, keyList);
        }
        catch (Exception)
        {
            // 草稿对齐失败不影响已写入的内容层。
        }
    }

    /// <summary>
    /// 收集本机未执行标记；跨项目、跨视图，不限于当前网格。
    /// </summary>
    private List<MarkedMedia> CollectMarked()
    {
        var markedList = new List<MarkedMedia>();
        if (mSession == null)
        {
            return markedList;
        }

        var file = MarkDraftStore.Load();
        if (!MarkDraftStore.MatchesWorkspace(file, mSession.Profile))
        {
            return markedList;
        }

        foreach (var entry in file.Entries)
        {
            var intent = MediaIntentCodes.FromCode(entry.Intent);
            if (intent == MediaIntent.None)
            {
                continue;
            }

            var (stage, site) = MarkDraftStore.Resolve(mSession, entry);
            if (stage == null && site == null)
            {
                continue;
            }

            markedList.Add(new MarkedMedia(intent, stage, site, entry.WorkId));
        }

        return markedList;
    }

    /// <summary>
    /// 清除选区标记。
    /// </summary>
    private void ClearSelectedMarks()
    {
        foreach (var card in SelectedCardList)
        {
            card.Mark = MediaIntent.None;
        }

        PersistCardMarks(SelectedCardList);
        Raise(nameof(MarkSummary));
        RaiseMarkCommands();
    }

    /// <summary>
    /// 灯箱恢复为适应窗口并清平移。
    /// </summary>
    private void ResetLightboxView()
    {
        mLightboxFit = true;
        mLightboxScale = LightboxView.DefaultScale;
        mLightboxPanX = 0;
        mLightboxPanY = 0;
        mLightboxZoomApplying = true;
        mSelectedLightboxZoom = LightboxZoomOptionList[0];
        Raise(nameof(SelectedLightboxZoom));
        mLightboxZoomApplying = false;
        RefreshLightboxLayout();
    }

    /// <summary>
    /// 改为适应窗口。
    /// </summary>
    private void ApplyLightboxFit()
    {
        mLightboxFit = true;
        mLightboxPanX = 0;
        mLightboxPanY = 0;
        RefreshLightboxLayout();
    }

    /// <summary>
    /// 改为绝对缩放。
    /// </summary>
    private void ApplyLightboxAbsolute(double scale)
    {
        mLightboxFit = false;
        mLightboxScale = LightboxView.ClampScale(scale);
        RefreshLightboxLayout();
    }

    /// <summary>
    /// 滚轮后尽量对齐到预设项，对不上则清空选中、只保留文字。
    /// </summary>
    private void SyncSelectedZoomToScale()
    {
        mLightboxZoomApplying = true;
        mSelectedLightboxZoom = LightboxZoomOptionList.FirstOrDefault(item =>
            !item.IsFit && Math.Abs(item.Scale - mLightboxScale) < 0.02);
        Raise(nameof(SelectedLightboxZoom));
        mLightboxZoomApplying = false;
    }

    /// <summary>
    /// 按当前图与视口重算显示尺寸、平移夹紧、缩放文字与小地图。
    /// </summary>
    private void RefreshLightboxLayout()
    {
        var bitmap = mInspectorImage as BitmapSource;
        var imageWidth = bitmap?.PixelWidth ?? 0;
        var imageHeight = bitmap?.PixelHeight ?? 0;
        var contain = LightboxView.ContainRatio(
            imageWidth,
            imageHeight,
            mLightboxViewportWidth,
            mLightboxViewportHeight);
        var scale = LightboxView.EffectiveScale(mLightboxFit, mLightboxScale, contain);
        var display = LightboxView.DisplaySize(imageWidth, imageHeight, scale);
        mLightboxDisplayWidth = display.Width;
        mLightboxDisplayHeight = display.Height;
        var pan = LightboxView.ClampPan(
            mLightboxPanX,
            mLightboxPanY,
            display.Width,
            display.Height,
            mLightboxViewportWidth,
            mLightboxViewportHeight);
        mLightboxPanX = pan.X;
        mLightboxPanY = pan.Y;
        CanPanLightbox = LightboxView.CanPan(
            display.Width,
            display.Height,
            mLightboxViewportWidth,
            mLightboxViewportHeight);
        LightboxZoomLabel = imageWidth <= 0
            ? "适应窗口"
            : LightboxView.FormatZoomLabel(mLightboxFit, scale);
        var mini = LightboxView.Minimap(
            display.Width,
            display.Height,
            mLightboxViewportWidth,
            mLightboxViewportHeight,
            pan.X,
            pan.Y,
            imageWidth,
            imageHeight);
        mLightboxMiniWidth = mini.Width;
        mLightboxMiniHeight = mini.Height;
        mLightboxMiniViewLeft = mini.ViewLeft;
        mLightboxMiniViewTop = mini.ViewTop;
        mLightboxMiniViewWidth = mini.ViewWidth;
        mLightboxMiniViewHeight = mini.ViewHeight;
        Raise(nameof(LightboxDisplayWidth));
        Raise(nameof(LightboxDisplayHeight));
        Raise(nameof(LightboxImageLeft));
        Raise(nameof(LightboxImageTop));
        Raise(nameof(CanPanLightbox));
        Raise(nameof(ShowLightboxMinimap));
        Raise(nameof(LightboxMiniWidth));
        Raise(nameof(LightboxMiniHeight));
        Raise(nameof(LightboxMiniViewWidth));
        Raise(nameof(LightboxMiniViewHeight));
        Raise(nameof(LightboxMiniViewMargin));
    }

    /// <summary>
    /// 当前视口相对原图的 contain 比例。
    /// </summary>
    private double CurrentContainRatio()
    {
        var bitmap = mInspectorImage as BitmapSource;
        return LightboxView.ContainRatio(
            bitmap?.PixelWidth ?? 0,
            bitmap?.PixelHeight ?? 0,
            mLightboxViewportWidth,
            mLightboxViewportHeight);
    }

    /// <summary>
    /// 刷新灯箱题名与标记角标。
    /// </summary>
    private void RaiseLightboxChrome()
    {
        Raise(nameof(LightboxCaption));
        Raise(nameof(LightboxMarkLabel));
        Raise(nameof(LightboxMarkBrush));
        Raise(nameof(LightboxPublishLabel));
        Raise(nameof(LightboxPublishBrush));
    }

    /// <summary>
    /// 适应窗口加常用绝对百分比。
    /// </summary>
    private static List<LightboxZoomChoice> CreateLightboxZoomOptions()
    {
        var list = new List<LightboxZoomChoice>
        {
            new("适应窗口", true, 0)
        };
        foreach (var percent in LightboxView.PresetPercents)
        {
            list.Add(new LightboxZoomChoice(percent + "%", false, percent / 100.0));
        }

        return list;
    }

    /// <summary>
    /// 主台脱敏筛：全部 / 未脱敏 / 已脱敏。
    /// </summary>
    private static List<SenseFilterChoice> CreateSenseFilterOptions()
    {
        return
        [
            new SenseFilterChoice(SenseFilterKind.All, "全部"),
            new SenseFilterChoice(SenseFilterKind.Unsigned, "未脱敏"),
            new SenseFilterChoice(SenseFilterKind.Signed, "已脱敏")
        ];
    }

    /// <summary>
    /// 刷新与选区相关的命令状态。
    /// </summary>
    private void RaiseMarkCommands()
    {
        MarkIngestCommand.RaiseCanExecute();
        MarkRecycleCommand.RaiseCanExecute();
        MarkHideCommand.RaiseCanExecute();
        MarkWithdrawCommand.RaiseCanExecute();
        ClearMarkCommand.RaiseCanExecute();
        PreviewCommand.RaiseCanExecute();
        CopyPromptCommand.RaiseCanExecute();
        ExecuteCommand.RaiseCanExecute();
        PublishCommand.RaiseCanExecute();
        RelocateCommand.RaiseCanExecute();
        RestoreSelectionCommand.RaiseCanExecute();
        SelectAllCommand.RaiseCanExecute();
        Raise(nameof(MarkSummary));
        Raise(nameof(ShowIngestTarget));
        Raise(nameof(ShowRelocate));
        RaiseLightboxChrome();
    }

    /// <summary>
    /// 左侧选中的是栏目根，不是年份、作品或未登记分组。
    /// </summary>
    private bool IsSelectedChannelRoot()
    {
        return mSelectedNav != null
            && mSelectedNav.Work == null
            && !mSelectedNav.IsYearGroup
            && !mSelectedNav.IsUnregisteredGroup;
    }

    /// <summary>
    /// 当前树节点是否为摄影投放夹上的已入编作品。
    /// </summary>
    private bool IsSelectedPhotoFolderWork()
    {
        return mSelectedNav?.Work is { IsUnregistered: false } work
            && WorkRegisterRules.IsPhotoFolderChannel(work.Channel);
    }

    /// <summary>
    /// 按当前栏目刷新上页目标作品列表。
    /// </summary>
    private void RefreshIngestWorks()
    {
        IngestWorkList.Clear();
        if (mSession == null || mSelectedNav == null)
        {
            Raise(nameof(IngestTargetWorkId));
            Raise(nameof(ShowIngestTarget));
            return;
        }

        var channelKey = mSelectedNav.Channel.Key;
        foreach (var work in UnregisteredWorkDiscovery.RegisteredInChannel(mSession.Works, channelKey))
        {
            IngestWorkList.Add(work);
        }

        mIngestTargetWorkId = ResolveIngestTargetWorkId();
        if (!string.IsNullOrWhiteSpace(mIngestTargetWorkId)
            && IngestWorkList.All(item => item.Id != mIngestTargetWorkId))
        {
            mIngestTargetWorkId = null;
        }

        Raise(nameof(IngestTargetWorkId));
        Raise(nameof(ShowIngestTarget));
    }

    /// <summary>
    /// 焦点或单选资源所属作品；解析不到则空，不回落栏目第一项。
    /// </summary>
    private string? ResolveIngestTargetWorkId()
    {
        if (mSelectedNav == null)
        {
            return null;
        }

        if (mSelectedNav.IsUnregistered)
        {
            return null;
        }

        var focus = SelectedCardList.Count == 1 ? SelectedCardList[0] : mFocusCard;
        var ownerId = CopyOwner.ResolveWorkId(mSession, focus?.Stage, focus?.Site);
        if (!string.IsNullOrWhiteSpace(ownerId) && IngestWorkList.Any(item => item.Id == ownerId))
        {
            return ownerId;
        }

        return mSelectedNav.Work is { IsUnregistered: false } work ? work.Id : null;
    }

    /// <summary>
    /// 回车或失焦把当前框写入本机草稿。
    /// </summary>
    public void CommitCopyDraft()
    {
        if (mSession == null || string.IsNullOrWhiteSpace(mCopySubjectKey))
        {
            return;
        }

        var title = ShowCopyName ? CopyName : "";
        var description = ShowCopyDescription ? CopyDescription : "";
        try
        {
            CopyDraftStore.Commit(
                mSession.Profile,
                mCopySubjectKey,
                title,
                description,
                mCopyPublishedTitle,
                mCopyPublishedDescription);
            PreviewCommand.RaiseCanExecute();
            CopyPromptCommand.RaiseCanExecute();
            ExecuteCommand.RaiseCanExecute();
        }
        catch (Exception)
        {
            // 草稿写失败不打断检视与换栏。
        }
    }

    /// <summary>
    /// 按检视主体把内容层为底、草稿覆盖后装入输入框。
    /// </summary>
    private void RefreshCopyEditor()
    {
        CommitCopyDraft();
        RefreshResourceTags();
        RefreshProjectTags();
        ClearCopySubject();
        var channelKey = mSelectedNav?.Channel.Key;
        CopyName = "";
        CopyDescription = "";
        ShowCopyName = false;
        ShowCopyDescription = false;
        if (mSession == null || mSelectedNav == null)
        {
            return;
        }

        if (CopyOwner.HidesCopyName(channelKey) && CopyOwner.HidesCopyDescription(channelKey))
        {
            return;
        }

        var hideDescription = CopyOwner.HidesCopyDescription(channelKey);
        var single = SelectedCardList.Count == 1 ? SelectedCardList[0] : null;
        if (single != null)
        {
            var ownerId = CopyOwner.ResolveWorkId(mSession, single.Stage, single.Site);
            if (!string.IsNullOrWhiteSpace(ownerId) && !CopyOwner.HidesCopyName(channelKey))
            {
                ShowCopyName = true;
                ShowCopyDescription = !hideDescription;
                var work = mSession.Works.FirstOrDefault(item =>
                    item.Id == ownerId
                    && string.Equals(item.Channel, channelKey, StringComparison.Ordinal));
                if (NoteRules.IsBodyFile(channelKey, single.Stage?.FullPath))
                {
                    ApplyCopyEditor(null, work?.Title ?? "", CopyText.ForEditor(work?.Summary));
                    return;
                }

                var media = FindCopyMedia(work, single);
                var objectKey = single.Site?.ObjectKey ?? single.Stage?.MatchedObject ?? single.Stage?.StageRel;
                var subjectKey = string.IsNullOrWhiteSpace(objectKey)
                    ? null
                    : CopyDraftStore.MediaKey(channelKey ?? "", ownerId, objectKey);
                ApplyCopyEditor(
                    subjectKey,
                    CopyText.ForEditor(media?.DisplayName ?? single.Site?.DisplayName),
                    CopyText.ForEditor(media?.Description ?? single.Site?.Description));
                return;
            }
        }

        if (mSelectedNav.IsYearGroup || hideDescription)
        {
            return;
        }

        ShowCopyDescription = true;
        if (mSelectedNav.Work is { IsUnregistered: false } navWork)
        {
            var noteTitle = navWork.SourceKind == "note" ? navWork.Title : "";
            ApplyCopyEditor(
                navWork.SourceKind == "note" ? null : CopyDraftStore.WorkKey(channelKey ?? "", navWork.Id),
                noteTitle,
                CopyText.ForEditor(navWork.Summary));
            if (navWork.SourceKind == "note")
            {
                ShowCopyName = true;
            }

            return;
        }

        mSession.ChannelLeadDict.TryGetValue(channelKey ?? "", out var lead);
        ApplyCopyEditor(
            string.IsNullOrWhiteSpace(channelKey) ? null : CopyDraftStore.ChannelKey(channelKey),
            "",
            CopyText.ForEditor(lead));
    }

    /// <summary>
    /// 内容层原文叠草稿后写入框，并记下当前主体键。
    /// </summary>
    private void ApplyCopyEditor(string? subjectKey, string publishedTitle, string publishedDescription)
    {
        var entry = mSession == null || string.IsNullOrWhiteSpace(subjectKey)
            ? null
            : CopyDraftStore.Find(CopyDraftStore.Load(), mSession.Profile, subjectKey);
        var resolved = CopyDraftStore.ResolveEditorText(publishedTitle, publishedDescription, entry);
        CopyName = resolved.Title;
        CopyDescription = resolved.Description;
        mCopySubjectKey = subjectKey;
        mCopyPublishedTitle = publishedTitle;
        mCopyPublishedDescription = publishedDescription;
    }

    /// <summary>
    /// 丢掉当前主体键，避免把下一栏的字写回上一栏。
    /// </summary>
    private void ClearCopySubject()
    {
        mCopySubjectKey = null;
        mCopyPublishedTitle = "";
        mCopyPublishedDescription = "";
    }

    /// <summary>
    /// 按对象键或配对对象取资源文案。
    /// </summary>
    private static WorkMediaItem? FindCopyMedia(WorkCatalogItem? work, MediaCardViewModel card)
    {
        if (work == null)
        {
            return null;
        }

        var objectKey = card.Site?.ObjectKey ?? card.Stage?.MatchedObject;
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        return work.Media.FirstOrDefault(item =>
            string.Equals(item.Src, objectKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// 当前选区是否含投放箱卡片。
    /// </summary>
    private bool HasStageSelection()
    {
        return SelectedCardList.Any(card => card.Kind == CardKind.Stage) || FocusCard?.Kind == CardKind.Stage;
    }

    /// <summary>
    /// 上页键：投放箱入库，或站点恢复显示。投放箱已上页按键不改标记。
    /// </summary>
    private bool HasIngestSelection()
    {
        return HasStageSelection() || HasSiteSelection();
    }

    /// <summary>
    /// 当前选区是否含站点卡片。
    /// </summary>
    private bool HasSiteSelection()
    {
        return SelectedCardList.Any(card => card.Kind == CardKind.Site) || FocusCard?.Kind == CardKind.Site;
    }

    /// <summary>
    /// 是否有选区。
    /// </summary>
    private bool HasAnySelection()
    {
        return SelectedCardList.Count > 0 || FocusCard != null;
    }

    /// <summary>
    /// 是否存在未执行标记。
    /// </summary>
    private bool HasAnyMark()
    {
        return ReadMarkTally().HasAny;
    }

    /// <summary>
    /// 把站点可见卡片按插入下标重排并写入草稿。
    /// </summary>
    public bool TryReorderSiteCards(IReadOnlyList<MediaCardViewModel> movingList, int insertIndex)
    {
        if (!CanReorderSite || mSession == null || mSelectedNav?.Work == null || movingList == null)
        {
            return false;
        }

        var visibleList = Cards
            .Where(card => card.Kind == CardKind.Site && card.Site is { IsHidden: false })
            .ToList();
        var hiddenList = Cards
            .Where(card => card.Kind == CardKind.Site && card.Site is { IsHidden: true })
            .ToList();
        var moving = movingList
            .Where(card => visibleList.Contains(card))
            .ToList();
        if (moving.Count == 0)
        {
            return false;
        }

        var nextVisible = MediaOrder.MoveItems(visibleList, moving, insertIndex);
        var objectList = nextVisible
            .Select(card => JsonUtil.ToRel(card.Site!.ObjectKey))
            .ToList();
        var publishedList = MediaOrder.SrcList(mSelectedNav.Work.Media);
        if (MediaOrder.SameOrder(objectList, visibleList.Select(card => JsonUtil.ToRel(card.Site!.ObjectKey)).ToList())
            && MediaOrder.SameOrder(objectList, publishedList)
            && ReorderDraftStore.Find(
                ReorderDraftStore.Load(),
                mSession.Profile,
                ReorderDraftStore.WorkKey(mSelectedNav.Channel.Key, mSelectedNav.Work.Id)) == null)
        {
            return false;
        }

        try
        {
            ReorderDraftStore.Commit(
                mSession.Profile,
                mSelectedNav.Channel.Key,
                mSelectedNav.Work.Id,
                objectList,
                publishedList);
        }
        catch (Exception)
        {
            StatusText = "排序草稿未能写入。";
            return false;
        }

        var siteList = MediaOrder.ApplySiteItems(
            nextVisible.Concat(hiddenList).Select(card => card.Site!).ToList(),
            objectList);
        var desiredList = new List<MediaCardViewModel>(siteList.Count);
        var cardDict = Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.CardKey))
            .ToDictionary(card => card.CardKey!, StringComparer.Ordinal);
        CardGridFill.Use(mSession, fill =>
        {
            foreach (var site in siteList)
            {
                var facts = fill.ReadSite(site);
                var key = "site:" + JsonUtil.ToRel(site.ObjectKey);
                if (cardDict.TryGetValue(key, out var existing))
                {
                    existing.ApplyCatalog(CreateCard(site, facts));
                    desiredList.Add(existing);
                }
                else
                {
                    desiredList.Add(CreateCard(site, facts));
                }
            }
        });

        mCardListSyncing = true;
        try
        {
            CardGridSync.SyncCards(Cards, desiredList);
        }
        finally
        {
            mCardListSyncing = false;
        }

        PreviewCommand.RaiseCanExecute();
        CopyPromptCommand.RaiseCanExecute();
        ExecuteCommand.RaiseCanExecute();
        return true;
    }

    /// <summary>
    /// 有会写盘的标记、文案、排序或已入编星级即可预演 / 执行。只等上页的原片星级不点亮按钮。门禁只读草稿，避免写盘后再刷新命令导致重入。
    /// </summary>
    private bool HasRunnableBatch()
    {
        return HasAnyMark()
            || PeekCopyItems().Count > 0
            || PeekReorderItems().Count > 0
            || PeekStarItems().Any(StarDraftCompiler.IsContentWrite);
    }

    /// <summary>
    /// 用已填事实组装投放箱卡片，不在此处读盘。
    /// </summary>
    private static MediaCardViewModel CreateCard(StageItem stage, CardFileFacts facts)
    {
        return new MediaCardViewModel(stage, facts);
    }

    /// <summary>
    /// 用已填事实组装站点卡片，不在此处读盘。
    /// </summary>
    private static MediaCardViewModel CreateCard(SiteItem site, CardFileFacts facts)
    {
        return new MediaCardViewModel(site, facts);
    }

    /// <summary>
    /// 选区打成同一星级。灯箱打开时只打当前张。与内容层相同则清草稿。
    /// </summary>
    public void ApplyStarRating(int stars)
    {
        if (mSession == null || stars is < 0 or > 5)
        {
            return;
        }

        var targetList = IsLightboxOpen
            ? (FocusCard == null ? new List<MediaCardViewModel>() : new List<MediaCardViewModel> { FocusCard })
            : SelectedCardList.ToList();
        if (targetList.Count == 0)
        {
            return;
        }

        var wrote = false;
        foreach (var card in targetList)
        {
            if (!TryResolveStar(card, out var key, out var published))
            {
                continue;
            }

            StarDraftStore.Commit(mSession.Profile, key, stars, published);
            wrote = true;
        }

        if (!wrote)
        {
            return;
        }

        PaintAllStars();
        PreviewCommand.RaiseCanExecute();
        ExecuteCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 当前工作区脏星级草稿编成的意图。
    /// </summary>
    private IReadOnlyList<IntentItem> PeekStarItems()
    {
        if (mSession == null)
        {
            return Array.Empty<IntentItem>();
        }

        return StarDraftCompiler.ToItems(mSession, StarDraftStore.Load());
    }

    /// <summary>
    /// 执行成功后删掉已写入内容层的星级草稿。
    /// </summary>
    private void AlignExecutedStarDrafts(IntentDocument document)
    {
        if (mSession == null)
        {
            return;
        }

        var keyList = StarDraftCompiler.KeysWritten(document).ToList();
        try
        {
            StarDraftStore.RemoveKeys(mSession.Profile, keyList);
        }
        catch (Exception)
        {
            // 草稿对齐失败不影响已写入的内容层。
        }
    }

    /// <summary>
    /// 按已载入的草稿覆盖内容层，刷新网格与对照卡上的圆点。
    /// </summary>
    private void PaintAllStars()
    {
        if (mSession == null)
        {
            return;
        }

        CardGridFill.Use(mSession, fill =>
        {
            foreach (var card in Cards)
            {
                card.ApplyStars(fill.StarsFor(card));
            }

            foreach (var row in CompareRows)
            {
                if (row.Left != null)
                {
                    row.Left.ApplyStars(fill.StarsFor(row.Left));
                }

                if (row.Right != null)
                {
                    row.Right.ApplyStars(fill.StarsFor(row.Right));
                }
            }
        });

        RefreshGridSortForStars();
    }

    /// <summary>
    /// 解析打星身份。已入编用对象键；未上页用投放路径，上页时写入新对象。
    /// </summary>
    private bool TryResolveStar(MediaCardViewModel card, out string key, out int published)
    {
        return CardGridFill.TryResolve(mSession, card, out key, out published);
    }

    /// <summary>
    /// 释放投放箱监视。
    /// </summary>
    public void DisposeWatchers()
    {
        PersistNavExpand();
        CommitCopyDraft();
        mWatchDebounce?.Stop();
        mNavExpandPersistDebounce?.Stop();
        mVideoScrubDebounce?.Stop();
        mVideoScrubCts?.Cancel();
        mVideoGrabber.Dispose();
        mStageWatcher?.Dispose();
        mStageWatcher = null;
    }

    /// <summary>
    /// 按本机设置挂上或拆掉投放箱监视。
    /// </summary>
    private void AttachStageWatcher()
    {
        mStageWatcher?.Dispose();
        mStageWatcher = null;
        if (!mSettings.WatchStageEnabled || mSession == null)
        {
            return;
        }

        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(mSession.Profile, mSession.Profile.StageRoot);
        if (!Directory.Exists(stageRoot))
        {
            return;
        }

        mStageWatcher = new FileSystemWatcher(stageRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
        };
        mStageWatcher.Created += OnStageChanged;
        mStageWatcher.Deleted += OnStageChanged;
        mStageWatcher.Renamed += OnStageChanged;
        mStageWatcher.Changed += OnStageChanged;
        mStageWatcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// 投放箱文件变更后排队重扫。
    /// </summary>
    private void OnStageChanged(object sender, FileSystemEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.InvokeAsync(ScheduleWatchReload);
    }

    /// <summary>
    /// 防抖后重载；标记已落盘，刷新后按键写回。
    /// </summary>
    private void ScheduleWatchReload()
    {
        if (mSession == null || !mSettings.WatchStageEnabled || mIsExecuting)
        {
            return;
        }

        mWatchDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        mWatchDebounce.Tick -= OnWatchDebounceTick;
        mWatchDebounce.Tick += OnWatchDebounceTick;
        mWatchDebounce.Stop();
        mWatchDebounce.Start();
    }

    /// <summary>
    /// 执行防抖后的编目刷新。
    /// </summary>
    private async void OnWatchDebounceTick(object? sender, EventArgs e)
    {
        mWatchDebounce?.Stop();
        if (mSession == null || !mSettings.WatchStageEnabled || mIsExecuting)
        {
            return;
        }

        mWatchReloadPending = true;
        var refreshed = await ReloadAsync(mSession.Profile.ProfilePath, coverGrid: false).ConfigureAwait(true);
        if (refreshed && mWatchReloadPending)
        {
            StatusText = "已按投放箱变更刷新编目。";
            mWatchReloadPending = false;
        }
    }

    /// <summary>
    /// 把当前卡片的意图写入本机标记文件。
    /// </summary>
    private void PersistCardMarks(IEnumerable<MediaCardViewModel> cardList)
    {
        if (mSession == null)
        {
            return;
        }

        var writeList = new List<MarkDraftWrite>();
        foreach (var card in cardList)
        {
            var key = card.CardKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            writeList.Add(new MarkDraftWrite(
                key,
                card.Mark,
                card.Site?.ChannelKey ?? card.Stage?.ChannelKey ?? mSelectedNav?.Channel.Key ?? "",
                ResolveMarkWorkId(card)));
        }

        if (writeList.Count == 0)
        {
            return;
        }

        MarkDraftStore.CommitMany(mSession.Profile, writeList);
        RefreshNavMarkCounts();
    }

    /// <summary>
    /// 按本机标记文件写回当前网格。
    /// </summary>
    private void ApplyPersistedMarks()
    {
        if (mSession == null)
        {
            return;
        }

        var file = MarkDraftStore.Load();
        foreach (var card in Cards)
        {
            var key = card.CardKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var entry = MarkDraftStore.Find(file, mSession.Profile, key);
            card.Mark = entry == null
                ? MediaIntent.None
                : MediaIntentCodes.FromCode(entry.Intent);
        }
    }

    /// <summary>
    /// 按本机标记刷新项目栏计数。
    /// </summary>
    private void RefreshNavMarkCounts()
    {
        if (mSession == null)
        {
            return;
        }

        var file = MarkDraftStore.Load();
        foreach (var node in NavNodes)
        {
            ApplyNavMarkCounts(node, file);
        }
    }

    /// <summary>
    /// 写入一个节点及其子树的计数。
    /// </summary>
    private void ApplyNavMarkCounts(NavNodeViewModel node, MarkDraftFile file)
    {
        if (mSession == null)
        {
            return;
        }

        IReadOnlyCollection<string>? workIdSet = null;
        string? workId = node.Work?.Id;
        if (node.IsYearGroup)
        {
            node.ApplyMarkCounts(default);
            foreach (var child in node.Children)
            {
                ApplyNavMarkCounts(child, file);
            }

            return;
        }

        if (node.IsUnregisteredGroup)
        {
            workId = null;
            workIdSet = node.Children
                .Select(child => child.Work?.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);
        }

        node.ApplyMarkCounts(MarkDraftStore.TallyForNav(
            file,
            mSession.Profile,
            node.Channel.Key,
            workId,
            workIdSet));
        foreach (var child in node.Children)
        {
            ApplyNavMarkCounts(child, file);
        }
    }

    /// <summary>
    /// 打标时记下所属作品，供切项目后仍按原作品执行。
    /// </summary>
    private string? ResolveMarkWorkId(MediaCardViewModel card)
    {
        if (!string.IsNullOrWhiteSpace(card.Site?.WorkId))
        {
            return card.Site.WorkId;
        }

        var ownerId = CopyOwner.ResolveWorkId(mSession, card.Stage, card.Site);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            return ownerId;
        }

        if (mSelectedNav?.Work is { } work)
        {
            return work.Id;
        }

        return mIngestTargetWorkId ?? card.Stage?.WorkIdGuess;
    }

    /// <summary>
    /// 读取当前工作区未执行标记的四项合计。
    /// </summary>
    private MarkCountTally ReadMarkTally()
    {
        if (mSession == null)
        {
            return default;
        }

        var file = MarkDraftStore.Load();
        if (!MarkDraftStore.MatchesWorkspace(file, mSession.Profile))
        {
            return default;
        }

        return MarkDraftStore.Tally(file.Entries.Select(entry => MediaIntentCodes.FromCode(entry.Intent)));
    }

    /// <summary>
    /// 清掉指定标记键并刷新卡片与项目栏计数。执行中成功或跳过的条已按条清除；此处用于整批跳过或收尾对齐界面。
    /// </summary>
    private void ClearExecutedMarks(IReadOnlyList<string> keyList)
    {
        if (mSession == null || keyList.Count == 0)
        {
            return;
        }

        MarkDraftStore.RemoveKeys(mSession.Profile, keyList);
        var keySet = new HashSet<string>(keyList, StringComparer.Ordinal);
        foreach (var card in Cards)
        {
            if (keySet.Contains(card.CardKey ?? ""))
            {
                card.Mark = MediaIntent.None;
            }
        }

        RefreshNavMarkCounts();
    }
}
