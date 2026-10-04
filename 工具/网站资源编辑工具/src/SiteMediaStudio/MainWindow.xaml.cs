using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PersonalWorks.SiteMediaStudio.Controls;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 主窗口：目录、网格与检视。
/// </summary>
public partial class MainWindow : Window
{
    private const double NavMin = 180;
    private const double NavMax = 360;
    private const double InspectorMin = 180;
    private const double MainMin = 580;
    private const double SplitterPx = 6;

    private readonly ShellViewModel mViewModel = new();
    private PaneDragKind mDragKind = PaneDragKind.None;
    private double mDragOriginX;
    private double mDragOriginWidth;
    private bool mLightboxPanning;
    private bool mLightboxMinimapDragging;
    private Point mLightboxDragOrigin;
    private bool mMarqueePending;
    private bool mMarqueeDragging;
    private Point mMarqueeOrigin;
    private CardSelectionSnapshot? mMarqueeOriginSnapshot;
    private List<MediaCardViewModel>? mMarqueeKeepList;
    private bool mSuppressNavSelect;
    private bool mReorderPending;
    private bool mReorderDragging;
    private Point mReorderOrigin;
    private MediaCardViewModel? mReorderCard;

    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(TreeViewItem),
            RequestBringIntoViewEvent,
            new RequestBringIntoViewEventHandler(OnNavTreeItemBringIntoView));
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = mViewModel;
        mViewModel.NavTreeRestored += OnNavTreeRestored;
        mViewModel.CardSelectionRestored += OnCardSelectionRestored;
        mViewModel.VisibleSurfaceChanged += OnVisibleSurfaceChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
        CardList.Loaded += OnPreviewListLoaded;
        CompareList.Loaded += OnPreviewListLoaded;
        NavTree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnNavExpandChanged), true);
        NavTree.AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(OnNavExpandChanged), true);
    }

    /// <summary>
    /// 名称栏回车确认后移出焦点，不换行。
    /// </summary>
    private void OnCopyNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (sender is TextBox box)
        {
            box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        mViewModel.CommitCopyDraft();
    }

    /// <summary>
    /// 名称栏与描述栏失焦即写入本机草稿。
    /// </summary>
    private void OnCopyFieldLostFocus(object sender, RoutedEventArgs e)
    {
        mViewModel.CommitCopyDraft();
    }

    /// <summary>
    /// 标签输入框回车或空格。
    /// </summary>
    private void OnResourceTagCommit(object sender, TagEditorCommitEventArgs e)
    {
        mViewModel.CommitResourceTag(e);
    }

    /// <summary>
    /// 点下拉中的一条预设。
    /// </summary>
    private void OnResourceTagPick(object sender, TagEditorItemEventArgs e)
    {
        mViewModel.PickResourceTag(e);
    }

    /// <summary>
    /// 芯片叉，从当前资源去掉。
    /// </summary>
    private void OnResourceTagRemove(object sender, TagEditorItemEventArgs e)
    {
        mViewModel.RemoveResourceTag(e);
    }

    /// <summary>
    /// 下拉叉，删除预设。
    /// </summary>
    private void OnResourceTagPresetDelete(object sender, TagEditorItemEventArgs e)
    {
        mViewModel.DeleteResourceTagPreset(e.Item);
    }

    /// <summary>
    /// 点项目汇总里的一枚标签，选中带着它的资源。
    /// </summary>
    private void OnProjectTagChosen(object sender, TagEditorItemEventArgs e)
    {
        mViewModel.ChooseProjectTag(e.Item);
    }

    /// <summary>
    /// 启动后加载工作区，并选中 Start 已恢复的栏目，避免总落到第一项。
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        mViewModel.Start(App.ProfileArg);
        SelectLoadedNav();
    }

    /// <summary>
    /// 编目刷新后等容器生成，再按 SelectedNav 补树高亮。
    /// </summary>
    private void OnNavTreeRestored()
    {
        Dispatcher.BeginInvoke(SelectLoadedNav, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 维护者折叠或展开项目栏后写入本机设置。
    /// </summary>
    private void OnNavExpandChanged(object sender, RoutedEventArgs e)
    {
        mViewModel.SchedulePersistNavExpand();
    }

    /// <summary>
    /// 按当前 SelectedNav 选中对应树节点，不把其它栏目强行展开。
    /// </summary>
    private void SelectLoadedNav()
    {
        if (!NavTree.HasItems || mViewModel.SelectedNav == null)
        {
            return;
        }

        var match = FindTreeItem(NavTree, mViewModel.SelectedNav);
        if (match != null)
        {
            match.IsSelected = true;
        }
    }

    /// <summary>
    /// 在已生成的树容器里按稳定键查找节点。
    /// </summary>
    private static TreeViewItem? FindTreeItem(ItemsControl parent, NavNodeViewModel target)
    {
        for (var i = 0; i < parent.Items.Count; i++)
        {
            if (parent.ItemContainerGenerator.ContainerFromIndex(i) is not TreeViewItem item)
            {
                continue;
            }

            if (item.DataContext is NavNodeViewModel node && node.NavKey == target.NavKey)
            {
                return item;
            }

            var nested = FindTreeItem(item, target);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// 关闭时释放目录监视。
    /// </summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        mViewModel.NavTreeRestored -= OnNavTreeRestored;
        mViewModel.CardSelectionRestored -= OnCardSelectionRestored;
        mViewModel.VisibleSurfaceChanged -= OnVisibleSurfaceChanged;
        mViewModel.DisposeWatchers();
    }

    /// <summary>
    /// 截住项目栏滚入：鼠标点选不纵向滚动（点下的条目已在画面内）；键盘仍只滚标题行。
    /// </summary>
    private static void OnNavTreeItemBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (sender is not TreeViewItem item)
        {
            return;
        }

        var tree = FindAncestor<TreeView>(item);
        if (tree == null || tree.Name != "NavTree")
        {
            return;
        }

        e.Handled = true;
        if (Mouse.LeftButton == MouseButtonState.Pressed
            || Mouse.RightButton == MouseButtonState.Pressed)
        {
            var scroller = FindScrollViewer(tree);
            scroller?.ScrollToHorizontalOffset(scroller.HorizontalOffset);
            return;
        }

        ScrollNavHeaderVerticalOnly(tree, item);
    }

    /// <summary>
    /// 只把标题行竖直滚进可视区；已在画面内则不动。标题未量完时不滚，避免把整棵子树高度当成标题。
    /// </summary>
    private static void ScrollNavHeaderVerticalOnly(TreeView tree, TreeViewItem item)
    {
        var scroller = FindScrollViewer(tree);
        if (scroller == null)
        {
            return;
        }

        item.ApplyTemplate();
        var header = item.Template?.FindName("Bd", item) as FrameworkElement
            ?? item.Template?.FindName("PART_Header", item) as FrameworkElement;
        if (header == null || header.ActualHeight <= 0 || scroller.ViewportHeight <= 0)
        {
            return;
        }

        Point topLeft;
        try
        {
            topLeft = header.TransformToAncestor(scroller).Transform(new Point(0, 0));
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var keepX = scroller.HorizontalOffset;
        var top = topLeft.Y;
        var bottom = top + header.ActualHeight;
        if (top >= 0 && bottom <= scroller.ViewportHeight)
        {
            scroller.ScrollToHorizontalOffset(keepX);
            return;
        }

        if (top < 0)
        {
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset + top);
        }
        else
        {
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset + bottom - scroller.ViewportHeight);
        }

        scroller.ScrollToHorizontalOffset(keepX);
    }

    /// <summary>
    /// 沿可视树向上查找指定类型祖先。
    /// </summary>
    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Ctrl 切换、Shift 连选；折叠钮仍交给展开逻辑。
    /// </summary>
    private void OnNavPreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindAncestor<ToggleButton>(source) != null)
        {
            return;
        }

        var item = FindAncestor<TreeViewItem>(source);
        if (item?.DataContext is not NavNodeViewModel node)
        {
            return;
        }

        var toggle = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var extend = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        ApplyNavPointer(item, node, toggle, extend);
        e.Handled = true;
    }

    /// <summary>
    /// 右键已选中的节点保留多选；点在选区外则改为只选该项。
    /// </summary>
    private void OnNavPreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = FindAncestor<TreeViewItem>(source);
        if (item?.DataContext is not NavNodeViewModel node)
        {
            return;
        }

        mSuppressNavSelect = true;
        try
        {
            if (node.IsSelected)
            {
                mViewModel.FocusNavKeepingSelection(node);
            }
            else
            {
                mViewModel.SelectNavExclusive(node);
            }

            item.IsSelected = true;
            item.Focus();
        }
        finally
        {
            mSuppressNavSelect = false;
        }
    }

    /// <summary>
    /// 键盘改选时收成单选。指针手势期间忽略 TreeView 自己的单选回写。
    /// </summary>
    private void OnNavSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (mSuppressNavSelect)
        {
            return;
        }

        if (e.NewValue is NavNodeViewModel node)
        {
            mViewModel.SelectNavExclusive(node);
        }
    }

    /// <summary>
    /// 写下指针选区，并让 TreeView 焦点落在点中项，但不让它清掉其余高亮。
    /// </summary>
    private void ApplyNavPointer(TreeViewItem item, NavNodeViewModel node, bool toggle, bool extend)
    {
        mSuppressNavSelect = true;
        try
        {
            mViewModel.SelectNavByPointer(node, toggle, extend);
            item.IsSelected = true;
            item.Focus();
        }
        finally
        {
            mSuppressNavSelect = false;
        }
    }

    /// <summary>
    /// 挂上滚动，按可视区补缩略图。
    /// </summary>
    private void OnPreviewListLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBox list)
        {
            return;
        }

        var scroller = FindScrollViewer(list);
        if (scroller == null)
        {
            return;
        }

        scroller.ScrollChanged -= OnPreviewScrollChanged;
        scroller.ScrollChanged += OnPreviewScrollChanged;
        RequestThumbsFromScroller(list, scroller);
    }

    /// <summary>
    /// 滚动后继续异步解码未入视口的图。
    /// </summary>
    private void OnPreviewScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroller)
        {
            return;
        }

        var list = mViewModel.IsComparePane ? CompareList : CardList;
        RequestThumbsFromScroller(list, scroller);
    }

    /// <summary>
    /// 按列表几何估算两屏卡片并请求解码。
    /// </summary>
    private void RequestThumbsFromScroller(ListBox list, ScrollViewer scroller)
    {
        if (list.Items.Count == 0)
        {
            return;
        }

        var itemWidth = Math.Max(140, mViewModel.CardWidth + 20);
        var itemHeight = Math.Max(80, mViewModel.CardImageHeight + 56);
        if (mViewModel.IsComparePane)
        {
            var rowHeight = Math.Max(itemHeight + 10, 1);
            var firstRow = (int)(scroller.VerticalOffset / rowHeight);
            var visibleRows = (int)Math.Ceiling(scroller.ViewportHeight / rowHeight) + 1;
            mViewModel.RequestVisibleCompareThumbnails(Math.Max(0, firstRow), visibleRows + visibleRows);
            return;
        }

        var viewportWidth = scroller.ViewportWidth > 0 ? scroller.ViewportWidth : list.ActualWidth;
        var viewportHeight = scroller.ViewportHeight > 0 ? scroller.ViewportHeight : 560;
        var columns = VirtualGridLayout.ColumnCount(viewportWidth, mViewModel.CardCellWidth);
        var range = VirtualGridLayout.VisibleRange(
            scroller.VerticalOffset,
            viewportHeight,
            mViewModel.CardCellHeight,
            columns,
            list.Items.Count,
            extraRows: 1);
        if (range.IsEmpty)
        {
            return;
        }

        mViewModel.RequestVisibleThumbnails(range.FirstIndex, range.Count);
    }

    /// <summary>
    /// 找到列表内部的滚动视口。
    /// </summary>
    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer)
        {
            return viewer;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 布局后再按当前滚动视口补缩略图。集合前几项不等于正在看的那一屏。
    /// </summary>
    private void OnVisibleSurfaceChanged()
    {
        Dispatcher.BeginInvoke(RefreshViewportThumbnails, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 用当前列表的滚动视口请求缩略图，与滚轮走同一条路径。
    /// </summary>
    private void RefreshViewportThumbnails()
    {
        var list = mViewModel.IsComparePane ? CompareList : CardList;
        var scroller = FindScrollViewer(list);
        if (scroller == null)
        {
            return;
        }

        RequestThumbsFromScroller(list, scroller);
    }

    /// <summary>
    /// 编目刷新后把仍在的选区写回 ListBox，避免只剩 ViewModel 选中、格子无高亮。
    /// </summary>
    private void OnCardSelectionRestored()
    {
        Dispatcher.BeginInvoke(ApplyCardListSelection, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 按 SelectedCardList 恢复网格高亮；对照页不走 ListBox 多选。
    /// </summary>
    private void ApplyCardListSelection()
    {
        if (mViewModel.IsComparePane || CardList.Items.Count == 0)
        {
            return;
        }

        CardList.SelectionChanged -= OnCardsSelected;
        try
        {
            CardList.SelectedItems.Clear();
            foreach (var card in mViewModel.SelectedCardList)
            {
                if (CardList.Items.Contains(card))
                {
                    CardList.SelectedItems.Add(card);
                }
            }

            if (!mViewModel.ScrollRestoredCardSelection)
            {
                return;
            }

            if (mViewModel.FocusCard != null
                && CardList.Items.Contains(mViewModel.FocusCard))
            {
                CardList.ScrollIntoView(mViewModel.FocusCard);
                return;
            }

            var scroller = FindScrollViewer(CardList);
            scroller?.ScrollToVerticalOffset(0);
        }
        finally
        {
            CardList.SelectionChanged += OnCardsSelected;
        }
    }

    /// <summary>
    /// 同步网格多选。
    /// </summary>
    private void OnCardsSelected(object sender, SelectionChangedEventArgs e)
    {
        mViewModel.SyncSelection(CardList.SelectedItems.Cast<MediaCardViewModel>());
    }

    /// <summary>
    /// 选单空白处左键：对照仍点空白取消；投放箱 / 站点开始框选，未拖动则取消选区。
    /// </summary>
    private void OnContentListPreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list)
        {
            return;
        }

        if (!IsListBlankHit(e.OriginalSource as DependencyObject))
        {
            if (ReferenceEquals(list, CardList))
            {
                BeginCardReorder(e);
            }

            return;
        }

        if (ReferenceEquals(list, CardList))
        {
            BeginCardMarquee(e);
            return;
        }

        if (mViewModel.SelectedCardList.Count == 0 && mViewModel.FocusCard == null)
        {
            return;
        }

        mViewModel.ClearCardSelection();
        list.Focus();
        e.Handled = true;
    }

    /// <summary>
    /// 记下起点并捕获鼠标，等位移超过阈值再画框。
    /// </summary>
    private void BeginCardMarquee(MouseButtonEventArgs e)
    {
        mMarqueePending = true;
        mMarqueeDragging = false;
        mMarqueeOrigin = e.GetPosition(MarqueeHost);
        mMarqueeOriginSnapshot = mViewModel.SnapshotSelection();
        mMarqueeKeepList = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            ? mViewModel.SelectedCardList.ToList()
            : null;
        CardList.Focus();
        CardList.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 记下站点卡片起点，过阈值再启拖排序。
    /// </summary>
    private void BeginCardReorder(MouseButtonEventArgs e)
    {
        if (!mViewModel.CanReorderSite
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        var card = FindCardFromSource(e.OriginalSource as DependencyObject);
        if (card?.Site == null || card.Site.IsHidden)
        {
            return;
        }

        mReorderPending = true;
        mReorderDragging = false;
        mReorderCard = card;
        mReorderOrigin = e.GetPosition(MarqueeHost);
    }

    /// <summary>
    /// 拖过阈值后显示选框并按格子交集更新选区，拖动中不写入历史。
    /// </summary>
    private void OnCardListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (mReorderPending || mReorderDragging)
        {
            UpdateCardReorder(e);
            return;
        }

        if ((!mMarqueePending && !mMarqueeDragging) || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var now = e.GetPosition(MarqueeHost);
        if (!mMarqueeDragging)
        {
            if ((now - mMarqueeOrigin).Length < 4)
            {
                return;
            }

            mMarqueeDragging = true;
            mMarqueePending = false;
            MarqueeRect.Visibility = Visibility.Visible;
        }

        UpdateMarqueeRect(now);
        ApplyMarqueeHits(now, recordHistory: false);
        e.Handled = true;
    }

    /// <summary>
    /// 松开：已框选则一次记入历史；只是点击空白则取消选区。
    /// </summary>
    private void OnCardListPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (mReorderPending || mReorderDragging)
        {
            FinishCardReorder(e);
            return;
        }

        if (!mMarqueePending && !mMarqueeDragging)
        {
            return;
        }

        var dragged = mMarqueeDragging;
        var originSnapshot = mMarqueeOriginSnapshot;
        var now = e.GetPosition(MarqueeHost);
        if (dragged)
        {
            ApplyMarqueeHits(now, recordHistory: false);
            EndCardMarquee();
            if (originSnapshot != null)
            {
                mViewModel.RememberSelection(originSnapshot);
            }
        }
        else
        {
            EndCardMarquee();
            CardList.UnselectAll();
        }

        e.Handled = true;
    }

    /// <summary>
    /// 捕获丢失时收起选框，不改选区。
    /// </summary>
    private void OnCardListLostCapture(object sender, MouseEventArgs e)
    {
        if (mReorderPending || mReorderDragging)
        {
            EndCardReorder();
        }

        if (mMarqueePending || mMarqueeDragging)
        {
            EndCardMarquee();
        }
    }

    /// <summary>
    /// 按宿主坐标放置半透明选框。
    /// </summary>
    private void UpdateMarqueeRect(Point now)
    {
        var left = Math.Min(mMarqueeOrigin.X, now.X);
        var top = Math.Min(mMarqueeOrigin.Y, now.Y);
        Canvas.SetLeft(MarqueeRect, left);
        Canvas.SetTop(MarqueeRect, top);
        MarqueeRect.Width = Math.Abs(now.X - mMarqueeOrigin.X);
        MarqueeRect.Height = Math.Abs(now.Y - mMarqueeOrigin.Y);
    }

    /// <summary>
    /// 把选框换算到换行网格内容坐标，选中相交的可见卡片。
    /// </summary>
    private void ApplyMarqueeHits(Point now, bool recordHistory)
    {
        var panel = FindVisualChild<VirtualizingWrapPanel>(CardList);
        if (panel == null)
        {
            return;
        }

        var originInPanel = MarqueeHost.TranslatePoint(mMarqueeOrigin, panel);
        var nowInPanel = MarqueeHost.TranslatePoint(now, panel);
        var indexList = VirtualGridLayout.IntersectingIndexes(
            originInPanel.X,
            originInPanel.Y + panel.VerticalOffset,
            nowInPanel.X,
            nowInPanel.Y + panel.VerticalOffset,
            VirtualGridLayout.ColumnCount(panel.ViewportWidth, mViewModel.CardCellWidth),
            mViewModel.CardCellWidth,
            mViewModel.CardCellHeight,
            CardList.Items.Count);
        var visibleList = mViewModel.VisibleCardList();
        var hitList = new List<MediaCardViewModel>();
        foreach (var index in indexList)
        {
            if (index >= 0 && index < visibleList.Count && !hitList.Contains(visibleList[index]))
            {
                hitList.Add(visibleList[index]);
            }
        }

        if (mMarqueeKeepList != null)
        {
            foreach (var card in mMarqueeKeepList)
            {
                if (!hitList.Contains(card))
                {
                    hitList.Add(card);
                }
            }
        }

        CardList.SelectionChanged -= OnCardsSelected;
        try
        {
            CardList.SelectedItems.Clear();
            foreach (var card in hitList)
            {
                if (CardList.Items.Contains(card))
                {
                    CardList.SelectedItems.Add(card);
                }
            }
        }
        finally
        {
            CardList.SelectionChanged += OnCardsSelected;
        }

        mViewModel.SyncSelection(hitList, recordHistory);
    }

    /// <summary>
    /// 收起选框并释放捕获。
    /// </summary>
    private void EndCardMarquee()
    {
        mMarqueePending = false;
        mMarqueeDragging = false;
        mMarqueeOriginSnapshot = null;
        mMarqueeKeepList = null;
        MarqueeRect.Visibility = Visibility.Collapsed;
        MarqueeRect.Width = 0;
        MarqueeRect.Height = 0;
        if (CardList.IsMouseCaptured)
        {
            CardList.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// 过阈值后画插入线；未过阈值不截获点击。
    /// </summary>
    private void UpdateCardReorder(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || mReorderCard == null)
        {
            return;
        }

        var now = e.GetPosition(MarqueeHost);
        if (!mReorderDragging)
        {
            if ((now - mReorderOrigin).Length < 4)
            {
                return;
            }

            mReorderDragging = true;
            mReorderPending = false;
            CardList.CaptureMouse();
            CardList.Cursor = Cursors.SizeAll;
            ReorderInsert.Visibility = Visibility.Visible;
        }

        PlaceReorderInsert(ReadReorderDropIndex(now));
        e.Handled = true;
    }

    /// <summary>
    /// 已拖则写入顺序；未过阈值交给点选。
    /// </summary>
    private void FinishCardReorder(MouseButtonEventArgs e)
    {
        var dragged = mReorderDragging;
        var card = mReorderCard;
        var now = e.GetPosition(MarqueeHost);
        EndCardReorder();
        if (!dragged || card == null)
        {
            return;
        }

        var movingList = mViewModel.SelectedCardList.Contains(card)
            ? mViewModel.SelectedCardList.ToList()
            : new List<MediaCardViewModel> { card };
        mViewModel.TryReorderSiteCards(movingList, ReadReorderDropIndex(now));
        e.Handled = true;
    }

    /// <summary>
    /// 收起插入线并恢复指针。
    /// </summary>
    private void EndCardReorder()
    {
        mReorderPending = false;
        mReorderDragging = false;
        mReorderCard = null;
        ReorderInsert.Visibility = Visibility.Collapsed;
        CardList.Cursor = Cursors.Arrow;
        if (CardList.IsMouseCaptured)
        {
            CardList.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// 指针对应的可见卡片插入下标。
    /// </summary>
    private int ReadReorderDropIndex(Point hostPoint)
    {
        var panel = FindVisualChild<VirtualizingWrapPanel>(CardList);
        if (panel == null)
        {
            return 0;
        }

        var visibleCount = mViewModel.Cards.Count(card =>
            card.Kind == CardKind.Site && card.Site is { IsHidden: false });
        var pointInPanel = MarqueeHost.TranslatePoint(hostPoint, panel);
        return VirtualGridLayout.DropIndex(
            pointInPanel.X,
            pointInPanel.Y + panel.VerticalOffset,
            VirtualGridLayout.ColumnCount(panel.ViewportWidth, mViewModel.CardCellWidth),
            mViewModel.CardCellWidth,
            mViewModel.CardCellHeight,
            visibleCount);
    }

    /// <summary>
    /// 在网格坐标上放 2px 松针插入线。
    /// </summary>
    private void PlaceReorderInsert(int dropIndex)
    {
        var panel = FindVisualChild<VirtualizingWrapPanel>(CardList);
        if (panel == null)
        {
            return;
        }

        var visibleCount = mViewModel.Cards.Count(card =>
            card.Kind == CardKind.Site && card.Site is { IsHidden: false });
        var columns = VirtualGridLayout.ColumnCount(panel.ViewportWidth, mViewModel.CardCellWidth);
        var cellWidth = mViewModel.CardCellWidth;
        var cellHeight = mViewModel.CardCellHeight;
        double localX;
        double localY;
        if (visibleCount == 0)
        {
            localX = 0;
            localY = 0;
        }
        else if (dropIndex >= visibleCount)
        {
            var last = visibleCount - 1;
            var lastCol = last % columns;
            var lastRow = last / columns;
            if (lastCol + 1 >= columns)
            {
                localX = 0;
                localY = (lastRow + 1) * cellHeight;
            }
            else
            {
                localX = (lastCol + 1) * cellWidth;
                localY = lastRow * cellHeight;
            }
        }
        else
        {
            localX = (dropIndex % columns) * cellWidth;
            localY = (dropIndex / columns) * cellHeight;
        }

        var hostPoint = panel.TranslatePoint(new Point(localX, localY - panel.VerticalOffset), MarqueeHost);
        Canvas.SetLeft(ReorderInsert, hostPoint.X);
        Canvas.SetTop(ReorderInsert, hostPoint.Y);
        ReorderInsert.Height = cellHeight;
    }

    /// <summary>
    /// 从命中源找回站点卡片。
    /// </summary>
    private static MediaCardViewModel? FindCardFromSource(DependencyObject? source)
    {
        if (source == null)
        {
            return null;
        }

        var item = FindAncestor<ListBoxItem>(source);
        return item?.DataContext as MediaCardViewModel;
    }

    /// <summary>
    /// 沿可视树向下查找指定类型。
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            return match;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindVisualChild<T>(VisualTreeHelper.GetChild(root, i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 选单空白处才弹出右键菜单；点在卡片或滚动条上不拦截系统外的其它菜单。
    /// </summary>
    private void OnContentListContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!IsListBlankHit(e.OriginalSource as DependencyObject))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 点在列表项或滚动条上则不是选单空白处。
    /// </summary>
    private static bool IsListBlankHit(DependencyObject? source)
    {
        if (source == null)
        {
            return false;
        }

        return FindAncestor<ListBoxItem>(source) == null
            && FindAncestor<ScrollBar>(source) == null;
    }

    /// <summary>
    /// 双击打开或关闭窗口内灯箱。心得正文改开 Typora。
    /// </summary>
    private void OnCardDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!mViewModel.TryOpenNoteBody())
        {
            mViewModel.ToggleLightbox();
        }
    }

    /// <summary>
    /// 对照行点选左侧投放箱。
    /// </summary>
    private void OnCompareLeftDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CompareRowViewModel row)
        {
            mViewModel.FocusCompareCard(row.Left, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            if (e.ClickCount == 2)
            {
                mViewModel.OpenLightbox();
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// 对照行松开左侧，避免与列表选中冲突。
    /// </summary>
    private void OnCompareLeftUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    /// 对照行点选右侧站点。
    /// </summary>
    private void OnCompareRightDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CompareRowViewModel row)
        {
            mViewModel.FocusCompareCard(row.Right, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            if (e.ClickCount == 2)
            {
                mViewModel.OpenLightbox();
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// 对照行松开右侧，避免与列表选中冲突。
    /// </summary>
    private void OnCompareRightUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    /// 快捷键：标记与预览。
    /// </summary>
    /// <summary>
    /// 点在输入框外面时，收起检视栏三个文本框的光标。
    /// </summary>
    private void OnBlankPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is not TextBox)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source || KeepsTextFocus(source))
        {
            return;
        }

        RootDock.Focus();
    }

    /// <summary>
    /// 点击落在文本框、下拉或密码框内部时，保持原焦点。
    /// </summary>
    private static bool KeepsTextFocus(DependencyObject source)
    {
        while (true)
        {
            if (source is TextBox or ComboBox or PasswordBox)
            {
                return true;
            }

            var parent = VisualTreeHelper.GetParent(source);
            if (parent == null)
            {
                return false;
            }

            source = parent;
        }
    }

    /// <summary>
    /// 主键盘与小键盘 0～5。其它数字与带修饰键的数字不打星。
    /// </summary>
    private static bool TryReadStarKey(Key key, out int stars)
    {
        if (key is >= Key.D0 and <= Key.D5)
        {
            stars = key - Key.D0;
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad5)
        {
            stars = key - Key.NumPad0;
            return true;
        }

        stars = 0;
        return false;
    }

    /// <summary>
    /// 展开排序层。弹出层不直接绑定 IsOpen，避免点外部关闭后被绑定重新打开。
    /// </summary>
    private void OnGridSortChecked(object sender, RoutedEventArgs e)
    {
        GridSortPopup.IsOpen = true;
    }

    /// <summary>
    /// 收起排序层。
    /// </summary>
    private void OnGridSortUnchecked(object sender, RoutedEventArgs e)
    {
        GridSortPopup.IsOpen = false;
    }

    /// <summary>
    /// 点外部关闭后，同步收起按钮，数字键恢复打星。
    /// </summary>
    private void OnGridSortPopupClosed(object? sender, EventArgs e)
    {
        if (GridSortBox.IsChecked == true)
        {
            GridSortBox.IsChecked = false;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is ComboBox or ComboBoxItem or TextBox or System.Windows.Controls.Primitives.TextBoxBase
            || SenseFilterBox.IsDropDownOpen
            || LightboxZoomBox.IsDropDownOpen
            || mViewModel.IsGridSortOpen)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && TryReadStarKey(e.Key, out var stars))
        {
            mViewModel.ApplyStarRating(stars);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (mViewModel.SelectAllCommand.CanExecute(null))
            {
                mViewModel.SelectAllCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.I)
        {
            mViewModel.MarkIngestCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.R || e.Key == Key.Delete)
        {
            mViewModel.MarkRecycleCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.H)
        {
            mViewModel.MarkHideCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.W)
        {
            mViewModel.MarkWithdrawCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (mViewModel.IsLightboxOpen)
            {
                mViewModel.CloseLightbox();
                Mouse.OverrideCursor = null;
            }
            else
            {
                mViewModel.ClearMarkCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            mViewModel.ToggleLightbox();
            if (!mViewModel.IsLightboxOpen)
            {
                Mouse.OverrideCursor = null;
            }

            e.Handled = true;
        }
        else if ((e.Key == Key.Left || e.Key == Key.Right)
            && (mViewModel.IsLightboxOpen || (!mViewModel.IsComparePane && CardList.IsKeyboardFocusWithin)))
        {
            MoveCardFocus(e.Key == Key.Right ? 1 : -1);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 网格与灯箱共用线性下标移动，行尾接下一行，首尾不绕圈。
    /// </summary>
    private void MoveCardFocus(int delta)
    {
        if (!mViewModel.TryStepFocus(delta))
        {
            return;
        }

        SyncGridToFocus();
    }

    /// <summary>
    /// 把网格选中与滚动对齐到当前焦点；灯箱打开时不抢键盘焦点。
    /// </summary>
    private void SyncGridToFocus()
    {
        var card = mViewModel.FocusCard;
        if (card == null || mViewModel.IsComparePane)
        {
            return;
        }

        var next = mViewModel.VisibleCardList().IndexOf(card);
        if (next < 0)
        {
            return;
        }

        CardList.SelectedIndex = next;
        CardList.ScrollIntoView(card);
        if (!mViewModel.IsLightboxOpen
            && CardList.ItemContainerGenerator.ContainerFromIndex(next) is ListBoxItem item)
        {
            item.Focus();
        }
    }

    /// <summary>
    /// 灯箱滚轮按指针位置缩放，不滚动底层网格。
    /// </summary>
    private void OnLightboxWheel(object sender, MouseWheelEventArgs e)
    {
        if (!mViewModel.IsLightboxOpen)
        {
            return;
        }

        var pos = e.GetPosition(LightboxViewport);
        mViewModel.ZoomLightbox(e.Delta, pos.X, pos.Y);
        e.Handled = true;
    }

    /// <summary>
    /// 视口尺寸变化后重算适应比例与小地图。
    /// </summary>
    private void OnLightboxViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        mViewModel.ReportLightboxViewport(e.NewSize.Width, e.NewSize.Height);
    }

    /// <summary>
    /// 点灯箱空白处关闭；顶栏、下拉、小地图与图片不关。
    /// </summary>
    private void OnLightboxBackdropDown(object sender, MouseButtonEventArgs e)
    {
        if (LightboxZoomBox.IsDropDownOpen || e.OriginalSource is ComboBoxItem)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject node
            && (IsWithin(node, LightboxChrome)
                || IsWithin(node, LightboxMinimapHost)
                || e.OriginalSource is System.Windows.Controls.Image))
        {
            return;
        }

        mViewModel.CloseLightbox();
        Mouse.OverrideCursor = null;
        e.Handled = true;
    }

    /// <summary>
    /// 图大于视口时按抓手拖移。
    /// </summary>
    private void OnLightboxImageDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Image image
            || IsWithin(image, LightboxMinimapHost)
            || !mViewModel.CanPanLightbox)
        {
            return;
        }

        mLightboxPanning = true;
        mLightboxDragOrigin = e.GetPosition(LightboxViewport);
        LightboxViewport.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 按指针位移拖移灯箱图。
    /// </summary>
    private void OnLightboxImageMove(object sender, MouseEventArgs e)
    {
        if (!mLightboxPanning || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var now = e.GetPosition(LightboxViewport);
        mViewModel.PanLightbox(now.X - mLightboxDragOrigin.X, now.Y - mLightboxDragOrigin.Y);
        mLightboxDragOrigin = now;
    }

    /// <summary>
    /// 结束灯箱拖移。
    /// </summary>
    private void OnLightboxImageUp(object sender, MouseButtonEventArgs e)
    {
        if (!mLightboxPanning)
        {
            return;
        }

        mLightboxPanning = false;
        if (LightboxViewport.IsMouseCaptured)
        {
            LightboxViewport.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// 小地图点击或拖动，把对应位置移到视口中心。
    /// </summary>
    private void OnLightboxMinimapDown(object sender, MouseButtonEventArgs e)
    {
        mLightboxMinimapDragging = true;
        LightboxMinimapHost.CaptureMouse();
        var pos = e.GetPosition(LightboxMinimapHost);
        mViewModel.PanLightboxToMinimap(pos.X, pos.Y);
        e.Handled = true;
    }

    /// <summary>
    /// 在小地图上拖动视口框。
    /// </summary>
    private void OnLightboxMinimapMove(object sender, MouseEventArgs e)
    {
        if (!mLightboxMinimapDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pos = e.GetPosition(LightboxMinimapHost);
        mViewModel.PanLightboxToMinimap(pos.X, pos.Y);
    }

    /// <summary>
    /// 结束小地图拖动。
    /// </summary>
    private void OnLightboxMinimapUp(object sender, MouseButtonEventArgs e)
    {
        if (!mLightboxMinimapDragging)
        {
            return;
        }

        mLightboxMinimapDragging = false;
        if (LightboxMinimapHost.IsMouseCaptured)
        {
            LightboxMinimapHost.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// 判断节点是否落在指定祖先内。
    /// </summary>
    private static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    #region 分栏拉伸

    /// <summary>
    /// 开始拖动项目栏右缘。
    /// </summary>
    private void OnNavSplitterDown(object sender, MouseButtonEventArgs e)
    {
        BeginPaneDrag(PaneDragKind.Nav, (IInputElement)sender, NavColumn.ActualWidth, e);
    }

    /// <summary>
    /// 开始拖动检视栏左缘。
    /// </summary>
    private void OnInspectorSplitterDown(object sender, MouseButtonEventArgs e)
    {
        BeginPaneDrag(PaneDragKind.Inspector, (IInputElement)sender, InspectorColumn.ActualWidth, e);
    }

    /// <summary>
    /// 按指针相对按下点的位移写入夹紧后的栏宽，不累计越界拖量。
    /// </summary>
    private void OnSplitterMouseMove(object sender, MouseEventArgs e)
    {
        if (mDragKind == PaneDragKind.None || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(ShellGrid).X - mDragOriginX;
        if (mDragKind == PaneDragKind.Nav)
        {
            ApplyNavWidth(mDragOriginWidth + delta);
        }
        else
        {
            ApplyInspectorWidth(mDragOriginWidth - delta);
        }
    }

    /// <summary>
    /// 结束分栏拖动。
    /// </summary>
    private void OnSplitterMouseUp(object sender, MouseButtonEventArgs e)
    {
        EndPaneDrag((IInputElement)sender);
    }

    /// <summary>
    /// 捕获丢失时清掉拖动状态。
    /// </summary>
    private void OnSplitterLostCapture(object sender, MouseEventArgs e)
    {
        mDragKind = PaneDragKind.None;
    }

    /// <summary>
    /// 最外层内容区变化时按真实可视宽度夹紧栏宽。
    /// </summary>
    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (mDragKind != PaneDragKind.None)
        {
            return;
        }

        ClampColumnsToViewport(e.NewSize.Width);
    }

    /// <summary>
    /// 还原最大化时等内容区写出新宽度后再夹紧。
    /// </summary>
    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        QueueColumnClamp();
    }

    /// <summary>
    /// 记录拖动起点并捕获鼠标。
    /// </summary>
    private void BeginPaneDrag(PaneDragKind kind, IInputElement splitter, double originWidth, MouseButtonEventArgs e)
    {
        mDragKind = kind;
        mDragOriginX = e.GetPosition(ShellGrid).X;
        mDragOriginWidth = originWidth;
        splitter.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 释放鼠标捕获。
    /// </summary>
    private void EndPaneDrag(IInputElement splitter)
    {
        if (mDragKind == PaneDragKind.None)
        {
            return;
        }

        mDragKind = PaneDragKind.None;
        if (splitter.IsMouseCaptured)
        {
            splitter.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// 项目栏宽度限制在 180～360，且不得把中间选单压过 580。
    /// </summary>
    private void ApplyNavWidth(double desired)
    {
        var inspector = StoredPixelWidth(InspectorColumn, InspectorMin);
        var maxByMain = ShellGrid.ActualWidth - SplitterPx * 2 - MainMin - inspector;
        var max = Math.Min(NavMax, maxByMain);
        if (max < NavMin)
        {
            max = NavMin;
        }

        NavColumn.Width = new GridLength(Math.Clamp(desired, NavMin, max));
    }

    /// <summary>
    /// 检视栏宽度下限 180，上限由中间选单 580 决定。
    /// </summary>
    private void ApplyInspectorWidth(double desired)
    {
        var nav = StoredPixelWidth(NavColumn, NavMin);
        var maxByMain = ShellGrid.ActualWidth - SplitterPx * 2 - MainMin - nav;
        var max = Math.Max(InspectorMin, maxByMain);
        InspectorColumn.Width = new GridLength(Math.Clamp(desired, InspectorMin, max));
    }

    /// <summary>
    /// 按写入的像素栏宽夹紧，避免用已被 Grid 挤过的 ActualWidth 误判为已收住。
    /// </summary>
    private void ClampColumnsToViewport(double? gridWidth = null)
    {
        var width = gridWidth ?? ShellGrid.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var nav = StoredPixelWidth(NavColumn, 240);
        var inspector = StoredPixelWidth(InspectorColumn, 300);
        var overflow = nav + inspector + SplitterPx * 2 + MainMin - width;
        if (overflow > 0)
        {
            var inspectorCut = Math.Min(overflow, inspector - InspectorMin);
            inspector -= inspectorCut;
            overflow -= inspectorCut;
            if (overflow > 0)
            {
                nav = Math.Max(NavMin, nav - overflow);
            }
        }

        NavColumn.Width = new GridLength(nav);
        InspectorColumn.Width = new GridLength(inspector);
    }

    /// <summary>
    /// 等还原后的视口宽度落地再夹紧栏宽。
    /// </summary>
    private void QueueColumnClamp()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ClampColumnsToViewport(RootDock.ActualWidth);
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// 读取列上写入的像素宽；未写入时退回 ActualWidth。
    /// </summary>
    private static double StoredPixelWidth(ColumnDefinition column, double fallback)
    {
        if (column.Width.GridUnitType == GridUnitType.Pixel && column.Width.Value > 0)
        {
            return column.Width.Value;
        }

        return column.ActualWidth > 0 ? column.ActualWidth : fallback;
    }

    #endregion

    /// <summary>
    /// 选择任意站点的 profile.json。
    /// </summary>
    private void OnOpenProfile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "工作区配置|profile.json;*.json|全部|*.*",
            Title = "打开工作区配置"
        };
        if (dialog.ShowDialog(this) == true)
        {
            mViewModel.OpenProfile(dialog.FileName);
        }
    }

    /// <summary>
    /// 当前正在拖动的分栏。
    /// </summary>
    private enum PaneDragKind
    {
        None,
        Nav,
        Inspector
    }
}
