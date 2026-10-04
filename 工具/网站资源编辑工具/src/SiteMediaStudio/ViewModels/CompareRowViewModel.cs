using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 对照一行：左投放箱、右站点。
/// </summary>
public sealed class CompareRowViewModel : ViewModelBase
{
    private CompareRow mRow;
    private MediaCardViewModel? mLeft;
    private MediaCardViewModel? mRight;
    private string mStatusLabel;
    private bool mIsPinned;

    public CompareRowViewModel(CompareRow row, MediaCardViewModel? left, MediaCardViewModel? right)
    {
        mRow = row;
        mLeft = left;
        mRight = right;
        mStatusLabel = row.StatusLabel;
        mIsPinned = row.Kind == CompareRowKind.ManualPin;
    }

    public CompareRow Row
    {
        get => mRow;
        private set => SetField(ref mRow, value);
    }

    public MediaCardViewModel? Left
    {
        get => mLeft;
        private set => SetField(ref mLeft, value);
    }

    public MediaCardViewModel? Right
    {
        get => mRight;
        private set => SetField(ref mRight, value);
    }

    public string StatusLabel
    {
        get => mStatusLabel;
        private set => SetField(ref mStatusLabel, value);
    }

    public bool IsPinned
    {
        get => mIsPinned;
        private set => SetField(ref mIsPinned, value);
    }

    public bool HasLeft => Left != null;
    public bool HasRight => Right != null;
    public bool LeftIsVideo => Left?.IsVideo == true;
    public bool RightIsVideo => Right?.IsVideo == true;

    /// <summary>
    /// 对照行稳定键：左右卡片键加上配对种类。
    /// </summary>
    public string RowKey => ReadRowKey(Row);

    /// <summary>
    /// 就地写入配对与左右卡片引用。
    /// </summary>
    public void ApplyCatalog(CompareRowViewModel incoming)
    {
        Row = incoming.Row;
        Left = incoming.Left;
        Right = incoming.Right;
        StatusLabel = incoming.StatusLabel;
        IsPinned = incoming.IsPinned;
        Raise(nameof(HasLeft));
        Raise(nameof(HasRight));
        Raise(nameof(LeftIsVideo));
        Raise(nameof(RightIsVideo));
        Raise(nameof(RowKey));
    }

    /// <summary>
    /// 按对照行领域对象生成稳定键。
    /// </summary>
    public static string ReadRowKey(CompareRow row)
    {
        var left = row.Stage == null ? "none" : "stage:" + JsonUtil.ToRel(row.Stage.StageRel);
        var right = row.Site == null ? "none" : "site:" + JsonUtil.ToRel(row.Site.ObjectKey);
        return left + "|" + right + "|" + row.Kind;
    }
}
