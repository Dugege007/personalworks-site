using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class LightboxViewTests
{
    [Fact]
    public void ClampScale_LimitsAndRejectsInvalid()
    {
        Assert.Equal(LightboxView.DefaultScale, LightboxView.ClampScale(double.NaN));
        Assert.Equal(LightboxView.DefaultScale, LightboxView.ClampScale(-1));
        Assert.Equal(LightboxView.MinScale, LightboxView.ClampScale(0.01));
        Assert.Equal(LightboxView.MaxScale, LightboxView.ClampScale(20));
        Assert.Equal(2, LightboxView.ClampScale(2));
    }

    [Fact]
    public void ContainRatio_FitsWideImage()
    {
        Assert.Equal(0.25, LightboxView.ContainRatio(4000, 2000, 1000, 800), 3);
        Assert.Equal(1, LightboxView.ContainRatio(0, 100, 800, 600));
    }

    [Fact]
    public void EffectiveScale_PrefersContainWhenFit()
    {
        Assert.Equal(0.25, LightboxView.EffectiveScale(true, 2, 0.25), 3);
        Assert.Equal(2, LightboxView.EffectiveScale(false, 2, 0.25), 3);
    }

    [Fact]
    public void ApplyWheel_ZoomsInAndOut()
    {
        var zoomed = LightboxView.ApplyWheel(1, 120);
        var shrunk = LightboxView.ApplyWheel(1, -120);
        Assert.True(zoomed > 1);
        Assert.True(shrunk < 1);
        Assert.Equal(LightboxView.MaxScale, LightboxView.ApplyWheel(LightboxView.MaxScale, 120));
        Assert.Equal(LightboxView.MinScale, LightboxView.ApplyWheel(LightboxView.MinScale, -120));
    }

    [Fact]
    public void DisplaySize_UsesAbsoluteScale()
    {
        var fit = LightboxView.DisplaySize(4000, 2000, 0.25);
        Assert.Equal(1000, fit.Width, 3);
        Assert.Equal(500, fit.Height, 3);
        Assert.True(LightboxView.DisplaySize(0, 100, 1).IsEmpty);
    }

    [Fact]
    public void FormatZoomLabel_MarksFit()
    {
        Assert.Equal("适应 37%", LightboxView.FormatZoomLabel(true, 0.372));
        Assert.Equal("100%", LightboxView.FormatZoomLabel(false, 1));
    }

    [Fact]
    public void ClampPan_CentersWhenImageFits()
    {
        var pan = LightboxView.ClampPan(40, 20, 800, 600, 1000, 800);
        Assert.Equal(0, pan.X);
        Assert.Equal(0, pan.Y);
    }

    [Fact]
    public void ClampPan_LimitsWhenImageOverflows()
    {
        var pan = LightboxView.ClampPan(1000, 0, 2000, 500, 1000, 800);
        Assert.Equal(500, pan.X);
        Assert.Equal(0, pan.Y);
        Assert.True(LightboxView.CanPan(2000, 500, 1000, 800));
        Assert.False(LightboxView.CanPan(800, 600, 1000, 800));
    }

    [Fact]
    public void ShouldShowMinimap_HidesWhenFit()
    {
        Assert.False(LightboxView.ShouldShowMinimap(true, true, 160));
        Assert.True(LightboxView.ShouldShowMinimap(true, false, 160));
        Assert.False(LightboxView.ShouldShowMinimap(false, false, 160));
        Assert.False(LightboxView.ShouldShowMinimap(true, false, 0));
    }

    [Fact]
    public void Minimap_ViewBoxMatchesVisiblePortion()
    {
        var mini = LightboxView.Minimap(2000, 1000, 1000, 800, 0, 0, 4000, 2000);
        Assert.Equal(160, mini.Width, 3);
        Assert.Equal(80, mini.Height, 3);
        Assert.True(mini.ViewWidth < mini.Width);
        Assert.True(mini.ViewHeight <= mini.Height + 0.01);
    }

    [Fact]
    public void ZoomAtCursor_KeepsAnchorOnScreen()
    {
        const double oldScale = 0.5;
        var newScale = LightboxView.ApplyWheel(oldScale, 120);
        var factor = newScale / oldScale;
        var pan = LightboxView.ZoomAtCursor(
            0,
            0,
            oldScale,
            newScale,
            cursorX: 750,
            cursorY: 400,
            viewportWidth: 1000,
            viewportHeight: 800,
            newDisplayWidth: 4000 * newScale,
            newDisplayHeight: 2000 * newScale);

        Assert.Equal(250 * (1 - factor), pan.X, 3);
        Assert.Equal(0, pan.Y, 3);
    }

    [Fact]
    public void PanToMinimap_CentersClickedPoint()
    {
        var pan = LightboxView.PanToMinimap(0, 40, 160, 80, 2000, 1000, 1000, 800);
        Assert.Equal(500, pan.X);
        Assert.Equal(0, pan.Y, 3);
    }

    [Fact]
    public void StepIndex_PagesWithoutWrapping()
    {
        Assert.Equal(1, VirtualGridLayout.StepIndex(0, 1, 3));
        Assert.Equal(0, VirtualGridLayout.StepIndex(0, -1, 3));
        Assert.Equal(2, VirtualGridLayout.StepIndex(2, 1, 3));
    }
}
