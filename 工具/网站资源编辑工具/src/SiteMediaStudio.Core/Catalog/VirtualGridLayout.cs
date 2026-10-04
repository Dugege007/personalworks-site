namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 等大卡片换行网格的可见范围计算，供虚拟化面板与单测共用。
/// </summary>
public static class VirtualGridLayout
{
    /// <summary>
    /// 按视口宽度与单元格宽度取列数，至少 1 列。
    /// </summary>
    public static int ColumnCount(double viewportWidth, double itemWidth)
    {
        if (itemWidth <= 0 || double.IsNaN(itemWidth) || double.IsInfinity(itemWidth))
        {
            return 1;
        }

        if (viewportWidth <= 0 || double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth))
        {
            return 1;
        }

        return Math.Max(1, (int)(viewportWidth / itemWidth));
    }

    /// <summary>
    /// 按条目数与列数取行数。
    /// </summary>
    public static int RowCount(int itemCount, int columns)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        var safeColumns = Math.Max(1, columns);
        return (itemCount + safeColumns - 1) / safeColumns;
    }

    /// <summary>
    /// 内容总高度。
    /// </summary>
    public static double ExtentHeight(int itemCount, int columns, double itemHeight)
    {
        if (itemHeight <= 0 || double.IsNaN(itemHeight) || double.IsInfinity(itemHeight))
        {
            return 0;
        }

        return RowCount(itemCount, columns) * itemHeight;
    }

    /// <summary>
    /// 当前滚动位置对应的可见下标闭区间；无条目时返回空区间。
    /// </summary>
    public static VisibleIndexRange VisibleRange(
        double verticalOffset,
        double viewportHeight,
        double itemHeight,
        int columns,
        int itemCount,
        int extraRows)
    {
        if (itemCount <= 0 || itemHeight <= 0)
        {
            return VisibleIndexRange.Empty;
        }

        var safeColumns = Math.Max(1, columns);
        var safeExtra = Math.Max(0, extraRows);
        var offset = verticalOffset < 0 ? 0 : verticalOffset;
        var height = viewportHeight < 0 ? 0 : viewportHeight;
        var firstRow = (int)Math.Floor(offset / itemHeight) - safeExtra;
        var lastRow = (int)Math.Ceiling((offset + height) / itemHeight) - 1 + safeExtra;
        if (lastRow < firstRow)
        {
            lastRow = firstRow;
        }

        var maxRow = RowCount(itemCount, safeColumns) - 1;
        firstRow = Math.Clamp(firstRow, 0, Math.Max(0, maxRow));
        lastRow = Math.Clamp(lastRow, firstRow, Math.Max(0, maxRow));
        var firstIndex = firstRow * safeColumns;
        var lastIndex = Math.Min(itemCount - 1, (lastRow + 1) * safeColumns - 1);
        if (firstIndex > lastIndex)
        {
            return VisibleIndexRange.Empty;
        }

        return new VisibleIndexRange(firstIndex, lastIndex);
    }

    /// <summary>
    /// 按线性下标左右移动：行尾接下一行行首，行首接上一行行尾；首尾不绕圈。
    /// </summary>
    public static int StepIndex(int currentIndex, int delta, int itemCount)
    {
        if (itemCount <= 0)
        {
            return -1;
        }

        if (delta == 0)
        {
            return currentIndex >= 0 && currentIndex < itemCount ? currentIndex : -1;
        }

        if (currentIndex < 0)
        {
            return delta > 0 ? 0 : -1;
        }

        var next = currentIndex + delta;
        if (next < 0 || next >= itemCount)
        {
            return currentIndex;
        }

        return next;
    }

    /// <summary>
    /// 与轴对齐框相交的单元格下标，按行优先。框可为任意对角；无重叠则空。
    /// </summary>
    public static List<int> IntersectingIndexes(
        double x1,
        double y1,
        double x2,
        double y2,
        int columns,
        double itemWidth,
        double itemHeight,
        int itemCount)
    {
        var indexList = new List<int>();
        if (itemCount <= 0
            || itemWidth <= 0
            || itemHeight <= 0
            || double.IsNaN(itemWidth)
            || double.IsNaN(itemHeight))
        {
            return indexList;
        }

        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var right = Math.Max(x1, x2);
        var bottom = Math.Max(y1, y2);
        if (right <= left || bottom <= top)
        {
            return indexList;
        }

        var safeColumns = Math.Max(1, columns);
        var rowCount = RowCount(itemCount, safeColumns);
        if (rowCount <= 0)
        {
            return indexList;
        }

        var gridRight = safeColumns * itemWidth;
        var gridBottom = rowCount * itemHeight;
        if (right <= 0 || bottom <= 0 || left >= gridRight || top >= gridBottom)
        {
            return indexList;
        }

        var firstCol = Math.Clamp((int)Math.Floor(left / itemWidth), 0, safeColumns - 1);
        var lastCol = Math.Clamp((int)Math.Ceiling(right / itemWidth) - 1, 0, safeColumns - 1);
        var firstRow = Math.Clamp((int)Math.Floor(top / itemHeight), 0, rowCount - 1);
        var lastRow = Math.Clamp((int)Math.Ceiling(bottom / itemHeight) - 1, 0, rowCount - 1);
        if (lastCol < firstCol || lastRow < firstRow)
        {
            return indexList;
        }

        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var col = firstCol; col <= lastCol; col++)
            {
                var index = row * safeColumns + col;
                if (index >= 0 && index < itemCount)
                {
                    indexList.Add(index);
                }
            }
        }

        return indexList;
    }

    /// <summary>
    /// 指针落到「插入到该下标之前」；可等于条目数表示追加到末尾。
    /// </summary>
    public static int DropIndex(
        double x,
        double y,
        int columns,
        double itemWidth,
        double itemHeight,
        int itemCount)
    {
        if (itemCount <= 0 || itemWidth <= 0 || itemHeight <= 0)
        {
            return 0;
        }

        var safeColumns = Math.Max(1, columns);
        var col = (int)Math.Floor(x / itemWidth);
        var row = (int)Math.Floor(y / itemHeight);
        if (double.IsNaN(x) || double.IsInfinity(x))
        {
            col = 0;
        }

        if (double.IsNaN(y) || double.IsInfinity(y))
        {
            row = 0;
        }

        col = Math.Clamp(col, 0, safeColumns - 1);
        if (row < 0)
        {
            return 0;
        }

        var index = row * safeColumns + col;
        var localX = x - col * itemWidth;
        if (localX > itemWidth / 2)
        {
            index++;
        }

        return Math.Clamp(index, 0, itemCount);
    }

    /// <summary>
    /// 条目所在行的顶部偏移。
    /// </summary>
    public static double RowOffset(int itemIndex, int columns, double itemHeight)
    {
        if (itemIndex < 0 || itemHeight <= 0)
        {
            return 0;
        }

        var row = itemIndex / Math.Max(1, columns);
        return row * itemHeight;
    }
}

/// <summary>
/// 需要物化的条目下标闭区间。
/// </summary>
public readonly record struct VisibleIndexRange(int FirstIndex, int LastIndex)
{
    public static VisibleIndexRange Empty { get; } = new(0, -1);

    public bool IsEmpty => LastIndex < FirstIndex;

    public int Count => IsEmpty ? 0 : LastIndex - FirstIndex + 1;
}
