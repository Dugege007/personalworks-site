using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 排序下拉中的一行。左数字与名称共用入列命令，右箭头只改方向。
/// </summary>
public sealed class GridSortOptionViewModel : ViewModelBase
{
    private string mPriorityText = "";
    private string mArrowGlyph = "▲";

    public GridSortOptionViewModel(GridSortKey key, Action toggle, Action flip)
    {
        Key = key;
        Label = GridSortState.Label(key);
        ToggleCommand = new RelayCommand(toggle);
        FlipCommand = new RelayCommand(flip);
    }

    public GridSortKey Key { get; }

    public string Label { get; }

    public string PriorityText
    {
        get => mPriorityText;
        private set => SetField(ref mPriorityText, value);
    }

    public string ArrowGlyph
    {
        get => mArrowGlyph;
        private set => SetField(ref mArrowGlyph, value);
    }

    public bool HasDirection => Key != GridSortKey.Stars;

    public RelayCommand ToggleCommand { get; }

    public RelayCommand FlipCommand { get; }

    /// <summary>
    /// 按当前入列状态刷新左端数字与箭头。未入列时数字留空，列宽仍占位。
    /// </summary>
    public void Apply(int priority, GridSortDir dir)
    {
        PriorityText = priority > 0 ? priority.ToString() : "";
        ArrowGlyph = HasDirection ? (dir == GridSortDir.Asc ? "▲" : "▼") : "";
    }
}
