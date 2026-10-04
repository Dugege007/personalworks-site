using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class VirtualGridLayoutTests
{
    [Theory]
    [InlineData(800, 200, 4)]
    [InlineData(199, 200, 1)]
    [InlineData(0, 200, 1)]
    [InlineData(800, 0, 1)]
    public void ColumnCount_ClampsToAtLeastOne(double viewportWidth, double itemWidth, int expected)
    {
        Assert.Equal(expected, VirtualGridLayout.ColumnCount(viewportWidth, itemWidth));
    }

    [Fact]
    public void RowCount_CoversRemainder()
    {
        Assert.Equal(0, VirtualGridLayout.RowCount(0, 4));
        Assert.Equal(3, VirtualGridLayout.RowCount(9, 4));
        Assert.Equal(3, VirtualGridLayout.RowCount(10, 4));
    }

    [Fact]
    public void VisibleRange_OnlyMaterializesViewportPlusExtraRows()
    {
        var range = VirtualGridLayout.VisibleRange(
            verticalOffset: 400,
            viewportHeight: 400,
            itemHeight: 200,
            columns: 4,
            itemCount: 400,
            extraRows: 1);

        Assert.Equal(4, range.FirstIndex);
        Assert.Equal(19, range.LastIndex);
        Assert.Equal(16, range.Count);
        Assert.True(range.Count < 400);
    }

    [Fact]
    public void VisibleRange_EmptyWhenNoItems()
    {
        var range = VirtualGridLayout.VisibleRange(0, 400, 200, 4, 0, 1);
        Assert.True(range.IsEmpty);
        Assert.Equal(0, range.Count);
    }

    [Fact]
    public void VisibleRange_ClampsToLastPage()
    {
        var range = VirtualGridLayout.VisibleRange(
            verticalOffset: 10_000,
            viewportHeight: 400,
            itemHeight: 200,
            columns: 4,
            itemCount: 10,
            extraRows: 1);

        Assert.Equal(8, range.FirstIndex);
        Assert.Equal(9, range.LastIndex);
    }

    [Fact]
    public void VisibleRange_LastPageIncludesTail()
    {
        const int itemCount = 652;
        const int columns = 6;
        const double itemHeight = 212;
        const double viewportHeight = 800;
        var extent = VirtualGridLayout.ExtentHeight(itemCount, columns, itemHeight);
        var offset = Math.Max(0, extent - viewportHeight);
        var range = VirtualGridLayout.VisibleRange(offset, viewportHeight, itemHeight, columns, itemCount, 1);

        Assert.False(range.IsEmpty);
        Assert.Equal(itemCount - 1, range.LastIndex);
        Assert.InRange(range.FirstIndex, 0, itemCount - 1);
    }

    [Fact]
    public void ShorterRowHeight_OvershootsLastPageStart()
    {
        const int itemCount = 652;
        const int columns = 6;
        const double panelHeight = 212;
        const double requestHeight = 200;
        const double viewportHeight = 800;
        var extent = VirtualGridLayout.ExtentHeight(itemCount, columns, panelHeight);
        var offset = Math.Max(0, extent - viewportHeight);
        var first = (int)(offset / requestHeight) * columns;

        Assert.True(first >= itemCount);
    }

    [Fact]
    public void ExtentHeight_IsRowsTimesItemHeight()
    {
        Assert.Equal(600, VirtualGridLayout.ExtentHeight(10, 4, 200));
        Assert.Equal(0, VirtualGridLayout.ExtentHeight(0, 4, 200));
    }

    [Fact]
    public void RowOffset_UsesColumnWrap()
    {
        Assert.Equal(400, VirtualGridLayout.RowOffset(9, 4, 200));
    }

    [Fact]
    public void IntersectingIndexes_EmptyWhenNoOverlapOrNoItems()
    {
        Assert.Empty(VirtualGridLayout.IntersectingIndexes(0, 0, 10, 10, 4, 100, 100, 0));
        Assert.Empty(VirtualGridLayout.IntersectingIndexes(0, 0, 0, 10, 4, 100, 100, 8));
        Assert.Empty(VirtualGridLayout.IntersectingIndexes(-40, 10, -1, 40, 4, 100, 100, 8));
        Assert.Empty(VirtualGridLayout.IntersectingIndexes(400, 0, 480, 80, 4, 100, 100, 8));
    }

    [Fact]
    public void IntersectingIndexes_SelectsPartialOverlapAndAnyDiagonal()
    {
        var fromTopLeft = VirtualGridLayout.IntersectingIndexes(10, 10, 150, 150, 4, 100, 100, 8);
        Assert.Equal(new[] { 0, 1, 4, 5 }, fromTopLeft);

        var fromBottomRight = VirtualGridLayout.IntersectingIndexes(150, 150, 10, 10, 4, 100, 100, 8);
        Assert.Equal(fromTopLeft, fromBottomRight);
    }

    [Fact]
    public void IntersectingIndexes_DoesNotCrossLastItem()
    {
        var indexList = VirtualGridLayout.IntersectingIndexes(0, 100, 400, 200, 4, 100, 100, 6);
        Assert.Equal(new[] { 4, 5 }, indexList);
    }

    [Fact]
    public void StepIndex_WrapsAcrossRowsButNotListEnds()
    {
        Assert.Equal(6, VirtualGridLayout.StepIndex(5, 1, 10));
        Assert.Equal(5, VirtualGridLayout.StepIndex(6, -1, 10));
        Assert.Equal(0, VirtualGridLayout.StepIndex(0, -1, 10));
        Assert.Equal(9, VirtualGridLayout.StepIndex(9, 1, 10));
        Assert.Equal(0, VirtualGridLayout.StepIndex(-1, 1, 10));
        Assert.Equal(-1, VirtualGridLayout.StepIndex(-1, -1, 10));
        Assert.Equal(-1, VirtualGridLayout.StepIndex(0, 1, 0));
    }

    [Fact]
    public void DropIndex_UsesHalfCellAndClampsToCount()
    {
        Assert.Equal(0, VirtualGridLayout.DropIndex(10, 10, 4, 100, 100, 6));
        Assert.Equal(1, VirtualGridLayout.DropIndex(60, 10, 4, 100, 100, 6));
        Assert.Equal(6, VirtualGridLayout.DropIndex(350, 150, 4, 100, 100, 6));
        Assert.Equal(0, VirtualGridLayout.DropIndex(10, 10, 4, 100, 100, 0));
    }
}
