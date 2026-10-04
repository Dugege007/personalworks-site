using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 内部用普通 TextBlock，不改窗口字体。整名能放下则连写，否则钉住扩展名。
/// </summary>
public sealed class StickyExtLabel : Grid
{
    public static readonly DependencyProperty FullTextProperty = DependencyProperty.Register(
        nameof(FullText),
        typeof(string),
        typeof(StickyExtLabel),
        new FrameworkPropertyMetadata("", OnTextChanged));

    public static readonly DependencyProperty StemProperty = DependencyProperty.Register(
        nameof(Stem),
        typeof(string),
        typeof(StickyExtLabel),
        new FrameworkPropertyMetadata("", OnTextChanged));

    public static readonly DependencyProperty ExtensionProperty = DependencyProperty.Register(
        nameof(Extension),
        typeof(string),
        typeof(StickyExtLabel),
        new FrameworkPropertyMetadata("", OnTextChanged));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(StickyExtLabel),
        new FrameworkPropertyMetadata(
            TextElement.ForegroundProperty.DefaultMetadata.DefaultValue,
            FrameworkPropertyMetadataOptions.Inherits,
            OnForegroundChanged));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(StickyExtLabel),
        new FrameworkPropertyMetadata(
            TextElement.FontSizeProperty.DefaultMetadata.DefaultValue,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnFontSizeChanged));

    private readonly TextBlock mFullBlock = NewLineBlock(TextTrimming.None);
    private readonly TextBlock mStemBlock = NewLineBlock(TextTrimming.CharacterEllipsis);
    private readonly TextBlock mExtBlock = NewLineBlock(TextTrimming.None);
    private readonly DockPanel mSplitPanel = new() { LastChildFill = true };

    public StickyExtLabel()
    {
        VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(mExtBlock, Dock.Right);
        mSplitPanel.Children.Add(mExtBlock);
        mSplitPanel.Children.Add(mStemBlock);
        Children.Add(mFullBlock);
        Children.Add(mSplitPanel);
        SyncText();
        SyncLocalTypeface();
    }

    public string FullText
    {
        get => (string)GetValue(FullTextProperty);
        set => SetValue(FullTextProperty, value);
    }

    public string Stem
    {
        get => (string)GetValue(StemProperty);
        set => SetValue(StemProperty, value);
    }

    public string Extension
    {
        get => (string)GetValue(ExtensionProperty);
        set => SetValue(ExtensionProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        SyncText();
        SyncLocalTypeface();
        mFullBlock.Measure(new Size(double.PositiveInfinity, constraint.Height));
        if (MediaPathRules.FitsWholeCaption(constraint.Width, mFullBlock.DesiredSize.Width))
        {
            mSplitPanel.Measure(new Size(0, 0));
            return mFullBlock.DesiredSize;
        }

        mSplitPanel.Measure(constraint);
        return new Size(
            double.IsInfinity(constraint.Width) ? mSplitPanel.DesiredSize.Width : constraint.Width,
            mSplitPanel.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var useFull = MediaPathRules.FitsWholeCaption(finalSize.Width, mFullBlock.DesiredSize.Width);
        if (useFull)
        {
            mFullBlock.Arrange(new Rect(finalSize));
            mSplitPanel.Arrange(new Rect(0, 0, 0, 0));
        }
        else
        {
            mFullBlock.Arrange(new Rect(0, 0, 0, 0));
            mSplitPanel.Arrange(new Rect(finalSize));
        }

        return finalSize;
    }

    /// <summary>
    /// 生成单行文本块，字体沿用窗口，不在此指定族与默认字号。
    /// </summary>
    private static TextBlock NewLineBlock(TextTrimming trimming)
    {
        return new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = trimming
        };
    }

    /// <summary>
    /// 属性变化后重测。
    /// </summary>
    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is StickyExtLabel label)
        {
            label.SyncText();
            label.InvalidateMeasure();
        }
    }

    /// <summary>
    /// 仅当模板显式设了前景时写到内部块。
    /// </summary>
    private static void OnForegroundChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is StickyExtLabel label)
        {
            label.SyncLocalTypeface();
            label.InvalidateVisual();
        }
    }

    /// <summary>
    /// 仅当模板显式设了字号时写到内部块（如路径行 11）。
    /// </summary>
    private static void OnFontSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is StickyExtLabel label)
        {
            label.SyncLocalTypeface();
            label.InvalidateMeasure();
        }
    }

    /// <summary>
    /// 把完整名、主体与扩展名写到内部文本块。
    /// </summary>
    private void SyncText()
    {
        var full = !string.IsNullOrEmpty(FullText) ? FullText : (Stem ?? "") + (Extension ?? "");
        mFullBlock.Text = full;
        mStemBlock.Text = Stem ?? "";
        mExtBlock.Text = Extension ?? "";
    }

    /// <summary>
    /// 不写 FontFamily。未设字号则清掉，让内部块继承窗口字体。
    /// </summary>
    private void SyncLocalTypeface()
    {
        foreach (var block in new[] { mFullBlock, mStemBlock, mExtBlock })
        {
            block.ClearValue(TextBlock.FontFamilyProperty);
            if (ReadLocalValue(FontSizeProperty) == DependencyProperty.UnsetValue)
            {
                block.ClearValue(TextBlock.FontSizeProperty);
            }
            else
            {
                block.FontSize = FontSize;
            }

            if (ReadLocalValue(ForegroundProperty) == DependencyProperty.UnsetValue)
            {
                block.ClearValue(TextBlock.ForegroundProperty);
            }
            else
            {
                block.Foreground = Foreground;
            }
        }
    }
}
