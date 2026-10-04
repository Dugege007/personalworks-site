using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Controls;

/// <summary>
/// 等大卡片换行虚拟化面板：只物化视口与少量前瞻行。
/// </summary>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private const int ExtraRows = 1;
    private Size mExtent;
    private Size mViewport;
    private Size mChildSize;
    private int mColumns = 1;
    private double mVerticalOffset;
    private bool mCanVerticallyScroll = true;
    private bool mCanHorizontallyScroll;

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public bool CanVerticallyScroll
    {
        get => mCanVerticallyScroll;
        set => mCanVerticallyScroll = value;
    }

    public bool CanHorizontallyScroll
    {
        get => mCanHorizontallyScroll;
        set => mCanHorizontallyScroll = value;
    }

    public double ExtentWidth => mExtent.Width;

    public double ExtentHeight => mExtent.Height;

    public double ViewportWidth => mViewport.Width;

    public double ViewportHeight => mViewport.Height;

    public double HorizontalOffset => 0;

    public double VerticalOffset => mVerticalOffset;

    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(mVerticalOffset - mChildSize.Height);

    public void LineDown() => SetVerticalOffset(mVerticalOffset + mChildSize.Height);

    public void LineLeft()
    {
    }

    public void LineRight()
    {
    }

    public void PageUp() => SetVerticalOffset(mVerticalOffset - mViewport.Height);

    public void PageDown() => SetVerticalOffset(mVerticalOffset + mViewport.Height);

    public void PageLeft()
    {
    }

    public void PageRight()
    {
    }

    public void MouseWheelUp() => SetVerticalOffset(mVerticalOffset - WheelDelta());

    public void MouseWheelDown() => SetVerticalOffset(mVerticalOffset + WheelDelta());

    public void MouseWheelLeft()
    {
    }

    public void MouseWheelRight()
    {
    }

    public void SetHorizontalOffset(double offset)
    {
    }

    /// <summary>
    /// 按像素夹紧纵向偏移并重测可见行。
    /// </summary>
    public void SetVerticalOffset(double offset)
    {
        var max = Math.Max(0, mExtent.Height - mViewport.Height);
        var next = Math.Clamp(offset, 0, max);
        if (Math.Abs(mVerticalOffset - next) < 0.5)
        {
            return;
        }

        mVerticalOffset = next;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    /// <summary>
    /// 把指定可视元素滚进视口。
    /// </summary>
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            if (!ReferenceEquals(InternalChildren[i], visual))
            {
                continue;
            }

            var index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index >= 0)
            {
                BringIndexIntoView(index);
            }

            break;
        }

        return rectangle;
    }

    /// <summary>
    /// 键盘或程序定位到指定下标。
    /// </summary>
    protected override void BringIndexIntoView(int index)
    {
        var top = VirtualGridLayout.RowOffset(index, mColumns, mChildSize.Height);
        var bottom = top + mChildSize.Height;
        if (top < mVerticalOffset)
        {
            SetVerticalOffset(top);
            return;
        }

        if (bottom > mVerticalOffset + mViewport.Height)
        {
            SetVerticalOffset(bottom - mViewport.Height);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = SanitizeViewport(availableSize);
        var itemCount = GetItemCount();
        mChildSize = ResolveChildSize();
        mColumns = VirtualGridLayout.ColumnCount(viewport.Width, mChildSize.Width);
        var extentHeight = VirtualGridLayout.ExtentHeight(itemCount, mColumns, mChildSize.Height);
        mExtent = new Size(viewport.Width, extentHeight);
        mViewport = viewport;
        var maxOffset = Math.Max(0, mExtent.Height - mViewport.Height);
        if (mVerticalOffset > maxOffset)
        {
            mVerticalOffset = maxOffset;
        }

        ScrollOwner?.InvalidateScrollInfo();
        var range = VirtualGridLayout.VisibleRange(
            mVerticalOffset,
            mViewport.Height,
            mChildSize.Height,
            mColumns,
            itemCount,
            ExtraRows);
        if (range.IsEmpty)
        {
            CleanUpItems(0, -1);
        }
        else
        {
            RealizeItems(range.FirstIndex, range.LastIndex);
            CleanUpItems(range.FirstIndex, range.LastIndex);
        }

        return viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var generator = ItemContainerGenerator;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            var index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index < 0)
            {
                child.Arrange(new Rect());
                continue;
            }

            var column = index % mColumns;
            var row = index / mColumns;
            var slot = new Rect(
                column * mChildSize.Width,
                row * mChildSize.Height - mVerticalOffset,
                mChildSize.Width,
                mChildSize.Height);
            child.Arrange(slot);
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                if (args.Position.Index >= 0 && args.ItemUICount > 0)
                {
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                }

                break;
            case NotifyCollectionChangedAction.Move:
                // 回收模式下只拆 Position 一段会让容器停在旧格，题号已改、图还在原地。
                if (InternalChildren.Count > 0)
                {
                    RemoveInternalChildRange(0, InternalChildren.Count);
                }

                break;
            case NotifyCollectionChangedAction.Reset:
                // 就地刷新（上页、换序、切筛）也会 Reset。偏移留在原地，由测量夹到新范围。
                if (InternalChildren.Count > 0)
                {
                    RemoveInternalChildRange(0, InternalChildren.Count);
                }

                break;
        }

        InvalidateMeasure();
    }

    /// <summary>
    /// 物化闭区间内的容器并按单元格尺寸测量。
    /// </summary>
    private void RealizeItems(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        var startPos = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = startPos.Offset == 0 ? startPos.Index : startPos.Index + 1;
        using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                if (generator.GenerateNext(out var newlyRealized) is not UIElement child)
                {
                    continue;
                }

                if (newlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    generator.PrepareItemContainer(child);
                }
                else if (!InternalChildren.Contains(child))
                {
                    InsertInternalChild(Math.Max(0, childIndex), child);
                    generator.PrepareItemContainer(child);
                }

                // 已经绑在这个格子上的容器不再 Prepare。每次插入都重绑会使前面的卡片闪。
                child.Measure(mChildSize);
            }
        }
    }

    /// <summary>
    /// 回收区间外的容器，避免大栏目挂满视觉树。
    /// </summary>
    private void CleanUpItems(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        var recycling = generator as IRecyclingItemContainerGenerator;
        for (var i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var position = new GeneratorPosition(i, 0);
            var itemIndex = generator.IndexFromGeneratorPosition(position);
            if (itemIndex >= firstIndex && itemIndex <= lastIndex)
            {
                continue;
            }

            if (recycling != null)
            {
                recycling.Recycle(position, 1);
            }
            else
            {
                generator.Remove(position, 1);
            }

            RemoveInternalChildRange(i, 1);
        }
    }

    /// <summary>
    /// 绑定单元格尺寸；未写入时退回当前已测子项或默认格。
    /// </summary>
    private Size ResolveChildSize()
    {
        var width = ItemWidth;
        var height = ItemHeight;
        if (width > 0 && height > 0)
        {
            return new Size(width, height);
        }

        if (ItemsControl.GetItemsOwner(this) is FrameworkElement owner
            && owner.DataContext is ShellViewModel shell)
        {
            return new Size(shell.CardCellWidth, shell.CardCellHeight);
        }

        if (InternalChildren.Count > 0)
        {
            var first = InternalChildren[0];
            first.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (first.DesiredSize.Width > 0 && first.DesiredSize.Height > 0)
            {
                return first.DesiredSize;
            }
        }

        return new Size(252, 200);
    }

    /// <summary>
    /// 无界可用尺寸时退回上一视口或默认工作区。
    /// </summary>
    private Size SanitizeViewport(Size available)
    {
        var width = double.IsInfinity(available.Width) || available.Width <= 0
            ? (mViewport.Width > 0 ? mViewport.Width : 800)
            : available.Width;
        var height = double.IsInfinity(available.Height) || available.Height <= 0
            ? (mViewport.Height > 0 ? mViewport.Height : 560)
            : available.Height;
        return new Size(width, height);
    }

    /// <summary>
    /// 绑定集合条数，不依赖已物化容器。
    /// </summary>
    private int GetItemCount()
    {
        var owner = ItemsControl.GetItemsOwner(this);
        return owner?.Items.Count ?? 0;
    }

    /// <summary>
    /// 滚轮步长取单元格高度的四分之三，避免一次跳过多行。
    /// </summary>
    private double WheelDelta()
    {
        var line = mChildSize.Height > 0 ? mChildSize.Height * 0.75 : 48;
        return Math.Max(48, line);
    }
}
