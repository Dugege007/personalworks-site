using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 只读长文本窗，用于预演、执行结果与排障。正文不可改，可选中复制。
/// </summary>
public sealed class TextDialog : Window
{
    private readonly FlowDocument mDocument;
    private readonly RichTextBox mBody;

    private TextDialog(string title, IReadOnlyList<ResultTextLine> lineList)
    {
        Title = title;
        Width = 720;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        mDocument = new FlowDocument
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, 微软雅黑"),
            FontSize = 13,
            PagePadding = new Thickness(0),
            Background = BrushCache.Freeze("#0B0D10")
        };
        foreach (var line in lineList)
        {
            mDocument.Blocks.Add(ParagraphOf(line));
        }

        mBody = new RichTextBox
        {
            Document = mDocument,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            IsUndoEnabled = false,
            AcceptsTab = false,
            AllowDrop = false,
            AutoWordSelection = false,
            BorderThickness = new Thickness(0),
            Background = BrushCache.Freeze("#0B0D10"),
            Foreground = BrushCache.Text,
            CaretBrush = BrushCache.Text,
            Padding = new Thickness(16),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Content = mBody;
        CommandManager.AddPreviewExecutedHandler(mBody, OnPreviewCopyPlainText);
        Loaded += (_, _) => FitPageWidth();
        SizeChanged += (_, _) => FitPageWidth();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>
    /// 显示只读文本。
    /// </summary>
    public static void Show(string title, string body)
    {
        var lineList = body.Replace("\r\n", "\n").Split('\n')
            .Select(ParsePlainLine)
            .ToList();
        Show(title, lineList);
    }

    /// <summary>
    /// 显示带色义的执行结果。
    /// </summary>
    public static void Show(string title, IReadOnlyList<ResultTextLine> lineList)
    {
        var owner = Application.Current?.MainWindow;
        WindowActivation.BringToFront(owner);
        var dialog = new TextDialog(title, lineList)
        {
            Owner = owner
        };
        dialog.ShowDialog();
        WindowActivation.BringToFront(owner);
    }

    /// <summary>
    /// 复制选区为纯文本。富文本复制会带上 RTF，中文在里面是字库字节，粘到别的程序会变成乱码。
    /// </summary>
    private void OnPreviewCopyPlainText(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Copy)
        {
            return;
        }

        var text = new TextRange(mBody.Selection.Start, mBody.Selection.End).Text;
        if (text.Length == 0)
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            e.Handled = true;
        }
        catch (ExternalException)
        {
            // 剪贴板被占用时留给控件自己的复制。
        }
    }

    /// <summary>
    /// 随窗宽收紧文档页宽，避免右侧留白或横向溢出。
    /// </summary>
    private void FitPageWidth()
    {
        var width = mBody.ViewportWidth;
        if (width <= 0)
        {
            width = mBody.ActualWidth - mBody.Padding.Left - mBody.Padding.Right;
        }

        if (width > 32)
        {
            mDocument.PageWidth = width;
        }
    }

    /// <summary>
    /// 上页 / 显示绿，隐藏灰，待撤黄，回收红；<c>[成功]</c> / <c>[失败]</c> 只给标签上色，右侧保持正文色。
    /// </summary>
    private static Paragraph ParagraphOf(ResultTextLine line)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 20
        };
        if (TrySplitOutcomeMark(line.Text, out var mark, out var rest))
        {
            paragraph.Inlines.Add(new Run(mark)
            {
                Foreground = mark.StartsWith("[失败]", StringComparison.Ordinal)
                    ? BrushCache.NavMarkRecycle
                    : BrushCache.NavMarkIngest
            });
            if (rest.Length > 0)
            {
                paragraph.Inlines.Add(new Run(rest) { Foreground = BrushCache.Text });
            }

            return paragraph;
        }

        paragraph.Inlines.Add(new Run(line.Text) { Foreground = BrushOf(line) });
        return paragraph;
    }

    /// <summary>
    /// 上页 / 显示对齐左侧栏绿，隐藏灰，待撤黄，回收红。
    /// </summary>
    private static Brush BrushOf(ResultTextLine line)
    {
        return line.Kind switch
        {
            ResultLineKind.CountIngest or ResultLineKind.CountRestore => BrushCache.NavMarkIngest,
            ResultLineKind.CountHide => BrushCache.NavMarkHide,
            ResultLineKind.CountWithdraw => BrushCache.NavMarkWithdraw,
            ResultLineKind.CountRecycle => BrushCache.NavMarkRecycle,
            ResultLineKind.Failure => BrushCache.NavMarkRecycle,
            _ => BrushCache.Text
        };
    }

    /// <summary>
    /// 拆出行首的成败标签；没有则整行按色义着色。
    /// </summary>
    private static bool TrySplitOutcomeMark(string text, out string mark, out string rest)
    {
        foreach (var tag in new[] { "[成功]", "[失败]" })
        {
            if (text.StartsWith(tag, StringComparison.Ordinal))
            {
                mark = tag;
                rest = text[tag.Length..];
                return true;
            }
        }

        mark = "";
        rest = text;
        return false;
    }

    private static ResultTextLine ParsePlainLine(string line)
    {
        if (line.StartsWith("[失败]", StringComparison.Ordinal))
        {
            return new ResultTextLine(line, ResultLineKind.Failure);
        }

        if (line.StartsWith("[成功]", StringComparison.Ordinal))
        {
            return new ResultTextLine(line, ResultLineKind.Success);
        }

        return new ResultTextLine(line);
    }
}
