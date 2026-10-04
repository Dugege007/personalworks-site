using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Controls;

/// <summary>
/// 可复用的标签输入：已选芯片、字面提交、模糊下拉。不写内容层。
/// </summary>
public partial class TagEditor : UserControl
{
    /// <summary>
    /// 下拉预设。顺序由宿主排好。
    /// </summary>
    public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(
        nameof(Suggestions),
        typeof(IEnumerable),
        typeof(TagEditor),
        new PropertyMetadata(null, OnSourceChanged));

    /// <summary>
    /// 已选芯片。
    /// </summary>
    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems),
        typeof(IEnumerable),
        typeof(TagEditor),
        new PropertyMetadata(null, OnSourceChanged));

    /// <summary>
    /// 未上页时关闭输入与芯片叉。
    /// </summary>
    public static readonly DependencyProperty IsEditingEnabledProperty = DependencyProperty.Register(
        nameof(IsEditingEnabled),
        typeof(bool),
        typeof(TagEditor),
        new PropertyMetadata(true));

    /// <summary>
    /// 为假时只留芯片：不显示输入框与下拉。项目汇总用。
    /// </summary>
    public static readonly DependencyProperty ShowsComposerProperty = DependencyProperty.Register(
        nameof(ShowsComposer),
        typeof(bool),
        typeof(TagEditor),
        new PropertyMetadata(true, OnShowsComposerChanged));

    /// <summary>
    /// 字面被拒绝时的原因。空字符串表示没有提示。宿主把这段红字放在「标签」标题右侧。
    /// </summary>
    public static readonly DependencyProperty NoticeTextProperty = DependencyProperty.Register(
        nameof(NoticeText),
        typeof(string),
        typeof(TagEditor),
        new PropertyMetadata(""));

    private bool mArrowPicked;
    private string? mNoticeForText;
    private Window? mClickWindow;
    private List<TagEditorItem> mFilteredList = new();

    public TagEditor()
    {
        InitializeComponent();
        Unloaded += (_, _) => DetachOutsideClick();
    }

    /// <summary>
    /// 下拉预设。
    /// </summary>
    public IEnumerable? Suggestions
    {
        get => (IEnumerable?)GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    /// <summary>
    /// 已选芯片。
    /// </summary>
    public IEnumerable? SelectedItems
    {
        get => (IEnumerable?)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    /// <summary>
    /// 是否允许改当前资源上的标签。
    /// </summary>
    public bool IsEditingEnabled
    {
        get => (bool)GetValue(IsEditingEnabledProperty);
        set => SetValue(IsEditingEnabledProperty, value);
    }

    /// <summary>
    /// 是否显示输入框与下拉。项目汇总为假。
    /// </summary>
    public bool ShowsComposer
    {
        get => (bool)GetValue(ShowsComposerProperty);
        set => SetValue(ShowsComposerProperty, value);
    }

    /// <summary>
    /// 字面不能落成时的原因。没有拒绝时为空。
    /// </summary>
    public string NoticeText
    {
        get => (string)GetValue(NoticeTextProperty);
        private set => SetValue(NoticeTextProperty, value);
    }

    /// <summary>
    /// 回车或空格提交输入框文字。宿主把 <see cref="TagEditorCommitEventArgs.Accepted"/> 设为真后清空输入。
    /// </summary>
    public event EventHandler<TagEditorCommitEventArgs>? TextCommitRequested;

    /// <summary>
    /// 点中下拉项，或方向键移动后再回车。
    /// </summary>
    public event EventHandler<TagEditorItemEventArgs>? SuggestionPicked;

    /// <summary>
    /// 芯片叉：从当前资源去掉。
    /// </summary>
    public event EventHandler<TagEditorItemEventArgs>? ChipRemoveRequested;

    /// <summary>
    /// 汇总芯片被点中。不改标签。
    /// </summary>
    public event EventHandler<TagEditorItemEventArgs>? ChipChosen;

    /// <summary>
    /// 下拉项叉：请求删除预设。
    /// </summary>
    public event EventHandler<TagEditorItemEventArgs>? PresetDeleteRequested;

    /// <summary>
    /// 关掉输入后收起下拉。
    /// </summary>
    private static void OnShowsComposerChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TagEditor editor && e.NewValue is false)
        {
            editor.ClosePopup();
        }
    }

    /// <summary>
    /// 预设或已选变化时重画。
    /// </summary>
    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TagEditor editor)
        {
            editor.ClearInputNotice();
            editor.RefreshChips();
            editor.RefreshSuggestions();
        }
    }

    /// <summary>
    /// 重画已选芯片。
    /// </summary>
    private void RefreshChips()
    {
        ChipList.ItemsSource = ReadItems(SelectedItems);
    }

    /// <summary>
    /// 按当前输入过滤下拉。方向键未动时不预选第一项。
    /// </summary>
    private void RefreshSuggestions()
    {
        var selected = ReadItems(SelectedItems);
        mFilteredList = TagEditorCatalog.Filter(ReadItems(Suggestions), InputBox.Text)
            .Where(item => !selected.Any(chip =>
                chip.Kind == item.Kind
                && string.Equals(chip.Id, item.Id, StringComparison.Ordinal)))
            .ToList();
        SuggestList.ItemsSource = mFilteredList;
        if (!mArrowPicked)
        {
            SuggestList.SelectedIndex = -1;
        }
        else if (SuggestList.SelectedIndex >= mFilteredList.Count)
        {
            SuggestList.SelectedIndex = mFilteredList.Count - 1;
        }
    }

    /// <summary>
    /// 读出绑定里的标签项。
    /// </summary>
    private static List<TagEditorItem> ReadItems(IEnumerable? source)
    {
        var list = new List<TagEditorItem>();
        if (source == null)
        {
            return list;
        }

        foreach (var item in source)
        {
            if (item is TagEditorItem tag)
            {
                list.Add(tag);
            }
        }

        return list;
    }

    /// <summary>
    /// 回车提交字面或高亮项；空格提交字面。组字中的键不提交。
    /// </summary>
    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsEditingEnabled || e.Key == Key.ImeProcessed || e.ImeProcessedKey != Key.None)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            ClosePopup();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            MoveHighlight(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            MoveHighlight(-1);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            if (mArrowPicked && SuggestList.SelectedItem is TagEditorItem picked)
            {
                RaisePick(picked);
            }
            else if (!string.IsNullOrWhiteSpace(InputBox.Text))
            {
                RaiseCommit();
            }
            else
            {
                ClosePopup();
            }

            return;
        }

        if (e.Key == Key.Space)
        {
            e.Handled = true;
            if (!string.IsNullOrWhiteSpace(InputBox.Text))
            {
                RaiseCommit();
            }
        }
    }

    /// <summary>
    /// 键入时重新过滤，并取消方向键预选。不能落成的字面立刻写出原因，下拉收起。
    /// </summary>
    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        mArrowPicked = false;
        var text = InputBox.Text;
        if (mNoticeForText != null && !string.Equals(text, mNoticeForText, StringComparison.Ordinal))
        {
            ClearInputNotice();
        }

        RefreshSuggestions();
        var rejected = RejectedLiteral(text);
        if (rejected != null)
        {
            ShowInputNotice(rejected);
            return;
        }

        if (IsEditingEnabled && InputBox.IsKeyboardFocused)
        {
            OpenPopup();
        }
    }

    /// <summary>
    /// 获得焦点时展开全部预设。
    /// </summary>
    private void OnInputGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!IsEditingEnabled)
        {
            return;
        }

        mArrowPicked = false;
        RefreshSuggestions();
        OpenPopup();
    }

    /// <summary>
    /// 焦点离开且指针不在下拉上时收起。
    /// </summary>
    private void OnInputLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(ClosePopupIfIdle, DispatcherPriority.Input);
    }

    /// <summary>
    /// 点下拉行即选中。点叉则删除预设，不选中。
    /// 叉放在列表项里，按钮 Click 等不到松手，所以在按下时删除。
    /// </summary>
    private void OnSuggestMouseDown(object sender, MouseButtonEventArgs e)
    {
        var button = FindButton(e.OriginalSource as DependencyObject);
        if (button != null)
        {
            RaisePresetDelete(button);
            e.Handled = true;
            return;
        }

        var item = FindTag(e.OriginalSource as DependencyObject);
        if (item == null)
        {
            return;
        }

        RaisePick(item);
        e.Handled = true;
    }

    /// <summary>
    /// 汇总模式下点整枚芯片。编辑模式仍只走叉。
    /// </summary>
    private void OnChipClick(object sender, MouseButtonEventArgs e)
    {
        if (ShowsComposer || sender is not FrameworkElement { DataContext: TagEditorItem item })
        {
            return;
        }

        ChipChosen?.Invoke(this, new TagEditorItemEventArgs(item));
        e.Handled = true;
    }

    /// <summary>
    /// 芯片叉。
    /// </summary>
    private void OnChipRemoveClick(object sender, RoutedEventArgs e)
    {
        if (!IsEditingEnabled || sender is not FrameworkElement { DataContext: TagEditorItem item })
        {
            return;
        }

        var args = new TagEditorItemEventArgs(item);
        ChipRemoveRequested?.Invoke(this, args);
    }

    /// <summary>
    /// 预设叉。
    /// </summary>
    private void OnPresetDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        RaisePresetDelete(element);
        e.Handled = true;
    }

    /// <summary>
    /// 自由标签才允许从预设里删除。
    /// </summary>
    private void RaisePresetDelete(FrameworkElement source)
    {
        if (source.DataContext is not TagEditorItem item || !item.AllowPresetDelete)
        {
            return;
        }

        PresetDeleteRequested?.Invoke(this, new TagEditorItemEventArgs(item));
    }

    /// <summary>
    /// 方向键移动高亮。之后回车选中高亮项。
    /// </summary>
    private void MoveHighlight(int delta)
    {
        if (mFilteredList.Count == 0)
        {
            return;
        }

        OpenPopup();
        var index = SuggestList.SelectedIndex;
        if (index < 0)
        {
            index = delta > 0 ? 0 : mFilteredList.Count - 1;
        }
        else
        {
            index = Math.Clamp(index + delta, 0, mFilteredList.Count - 1);
        }

        mArrowPicked = true;
        SuggestList.SelectedIndex = index;
        SuggestList.ScrollIntoView(SuggestList.SelectedItem);
    }

    /// <summary>
    /// 提交输入框文字。
    /// </summary>
    private void RaiseCommit()
    {
        var args = new TagEditorCommitEventArgs(InputBox.Text);
        TextCommitRequested?.Invoke(this, args);
        if (!string.IsNullOrEmpty(args.Notice))
        {
            ShowInputNotice(args.Notice);
            return;
        }

        if (!args.Accepted)
        {
            return;
        }

        ClearInputNotice();
        InputBox.Clear();
        ClosePopup();
    }

    /// <summary>
    /// 选中一条预设。
    /// </summary>
    private void RaisePick(TagEditorItem item)
    {
        var args = new TagEditorItemEventArgs(item);
        SuggestionPicked?.Invoke(this, args);
        if (!string.IsNullOrEmpty(args.Notice))
        {
            ShowInputNotice(args.Notice);
            return;
        }

        if (!args.Accepted)
        {
            return;
        }

        ClearInputNotice();
        InputBox.Clear();
        ClosePopup();
    }

    /// <summary>
    /// 当前文字若不能当自由标签，返回原因。空文本与可写入的词返回空。
    /// </summary>
    private string? RejectedLiteral(string text)
    {
        if (TagEditorCatalog.TryCommitText(text, ReadItems(Suggestions), out _, out _, out var error))
        {
            return null;
        }

        return string.IsNullOrEmpty(error) ? null : error;
    }

    /// <summary>
    /// 写出拒绝原因。不写入，输入框里的字留着，下拉收起。
    /// </summary>
    private void ShowInputNotice(string text)
    {
        mNoticeForText = InputBox.Text;
        NoticeText = text;
        ClosePopup();
    }

    /// <summary>
    /// 收起拒绝原因。
    /// </summary>
    private void ClearInputNotice()
    {
        mNoticeForText = null;
        if (string.IsNullOrEmpty(NoticeText))
        {
            return;
        }

        NoticeText = "";
    }

    /// <summary>
    /// 展开下拉，宽度对齐输入框。
    /// </summary>
    private void OpenPopup()
    {
        if (!ShowsComposer || !IsEditingEnabled || mFilteredList.Count == 0)
        {
            ClosePopup();
            return;
        }

        SuggestPopup.Width = Math.Max(160, InputBox.ActualWidth);
        SuggestPopup.IsOpen = true;
        AttachOutsideClick();
    }

    /// <summary>
    /// 收起下拉。
    /// </summary>
    private void ClosePopup()
    {
        SuggestPopup.IsOpen = false;
        mArrowPicked = false;
        DetachOutsideClick();
    }

    /// <summary>
    /// 下拉展开时，点主窗里输入框以外的位置就收起。下拉自己在另一层，点选项不受影响。
    /// </summary>
    private void AttachOutsideClick()
    {
        var window = Window.GetWindow(this);
        if (window == null || window == mClickWindow)
        {
            return;
        }

        DetachOutsideClick();
        mClickWindow = window;
        window.PreviewMouseDown += OnOwnerPreviewMouseDown;
    }

    /// <summary>
    /// 不再监听主窗点击。
    /// </summary>
    private void DetachOutsideClick()
    {
        if (mClickWindow == null)
        {
            return;
        }

        mClickWindow.PreviewMouseDown -= OnOwnerPreviewMouseDown;
        mClickWindow = null;
    }

    /// <summary>
    /// 点到输入框以外就收起下拉。
    /// </summary>
    private void OnOwnerPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!SuggestPopup.IsOpen)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && IsInside(InputBox, source))
        {
            return;
        }

        ClosePopup();
    }

    /// <summary>
    /// 点击是否落在这块控件里。
    /// </summary>
    private static bool IsInside(DependencyObject root, DependencyObject? source)
    {
        while (source != null)
        {
            if (source == root)
            {
                return true;
            }

            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    /// <summary>
    /// 焦点已离开且指针不在下拉里才收起，避免点选时先被关掉。
    /// </summary>
    private void ClosePopupIfIdle()
    {
        if (InputBox.IsKeyboardFocused || SuggestPopup.IsMouseOver)
        {
            return;
        }

        ClosePopup();
    }

    /// <summary>
    /// 沿可视树找按钮，用来把叉和行点击分开。
    /// </summary>
    private static Button? FindButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button button)
            {
                return button;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    /// <summary>
    /// 沿可视树找这一行的标签项。
    /// </summary>
    private static TagEditorItem? FindTag(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { DataContext: TagEditorItem item })
            {
                return item;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}

/// <summary>
/// 字面提交。宿主接受后控件清空输入框。
/// </summary>
public sealed class TagEditorCommitEventArgs : EventArgs
{
    public TagEditorCommitEventArgs(string text)
    {
        Text = text;
    }

    /// <summary>
    /// 输入框原文。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 宿主已收下这次提交。
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// 字面不能落成时的原因。有字则由宿主写在「标签」标题右侧，不弹窗。
    /// </summary>
    public string? Notice { get; set; }
}

/// <summary>
/// 点选、去掉芯片或删除预设。
/// </summary>
public sealed class TagEditorItemEventArgs : EventArgs
{
    public TagEditorItemEventArgs(TagEditorItem item)
    {
        Item = item;
    }

    /// <summary>
    /// 被操作的那一枚。
    /// </summary>
    public TagEditorItem Item { get; }

    /// <summary>
    /// 宿主已收下这次操作。点选时为真才清空输入框。
    /// </summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// 这次点选被拒绝时的原因。有字则由宿主写在「标签」标题右侧，不弹窗。
    /// </summary>
    public string? Notice { get; set; }
}
