using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NavPointerSelectionTests
{
    [Fact]
    public void PlainClick_ReplacesSelection()
    {
        var nodes = Nodes("a", "b", "c");
        var result = NavPointerSelection.Apply(nodes, new[] { nodes[0] }, nodes[0], nodes[2], false, false);

        Assert.Equal(new[] { nodes[2] }, result.Selected);
        Assert.Same(nodes[2], result.Anchor);
        Assert.Same(nodes[2], result.Focus);
    }

    [Fact]
    public void Ctrl_TogglesMembership()
    {
        var nodes = Nodes("a", "b", "c");
        var added = NavPointerSelection.Apply(nodes, new[] { nodes[0] }, nodes[0], nodes[2], true, false);
        Assert.Equal(new[] { nodes[0], nodes[2] }, added.Selected);
        Assert.Same(nodes[2], added.Anchor);
        Assert.Same(nodes[2], added.Focus);

        var removed = NavPointerSelection.Apply(nodes, added.Selected, added.Anchor, nodes[2], true, false);
        Assert.Equal(new[] { nodes[0] }, removed.Selected);
        Assert.Same(nodes[0], removed.Focus);
    }

    [Fact]
    public void Ctrl_RemovingLast_ClearsFocus()
    {
        var nodes = Nodes("a");
        var result = NavPointerSelection.Apply(nodes, new[] { nodes[0] }, nodes[0], nodes[0], true, false);

        Assert.Empty(result.Selected);
        Assert.Null(result.Focus);
    }

    [Fact]
    public void Ctrl_RemovingFocus_MovesToNearest()
    {
        var nodes = Nodes("a", "b", "c", "d");
        var result = NavPointerSelection.Apply(
            nodes,
            new[] { nodes[0], nodes[1], nodes[3] },
            nodes[1],
            nodes[1],
            true,
            false);

        Assert.Equal(new[] { nodes[0], nodes[3] }, result.Selected);
        Assert.Same(nodes[0], result.Focus);
    }

    [Fact]
    public void Shift_SelectsInclusiveVisibleRange()
    {
        var nodes = Nodes("a", "b", "c", "d");
        var down = NavPointerSelection.Apply(nodes, new[] { nodes[0] }, nodes[1], nodes[3], false, true);
        Assert.Equal(new[] { nodes[1], nodes[2], nodes[3] }, down.Selected);
        Assert.Same(nodes[1], down.Anchor);
        Assert.Same(nodes[3], down.Focus);

        var up = NavPointerSelection.Apply(nodes, down.Selected, nodes[3], nodes[1], false, true);
        Assert.Equal(new[] { nodes[1], nodes[2], nodes[3] }, up.Selected);
        Assert.Same(nodes[3], up.Anchor);
    }

    [Fact]
    public void Shift_WithoutAnchor_SelectsOnlyClicked()
    {
        var nodes = Nodes("a", "b");
        var result = NavPointerSelection.Apply(nodes, new[] { nodes[0] }, null, nodes[1], false, true);

        Assert.Equal(new[] { nodes[1] }, result.Selected);
        Assert.Same(nodes[1], result.Anchor);
    }

    [Fact]
    public void CtrlShift_AddsRangeAndKeepsOutsideSelection()
    {
        var nodes = Nodes("a", "b", "c", "d");
        var hidden = new object();
        var result = NavPointerSelection.Apply(
            nodes,
            new object[] { nodes[0], hidden },
            nodes[1],
            nodes[3],
            true,
            true);

        Assert.Equal(new object[] { nodes[0], nodes[1], nodes[2], nodes[3], hidden }, result.Selected);
        Assert.Same(nodes[1], result.Anchor);
        Assert.Same(nodes[3], result.Focus);
    }

    private static List<object> Nodes(params string[] names)
    {
        return names.Select(name => new object()).ToList();
    }
}
