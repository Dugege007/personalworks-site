using System.Collections;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 把卡片当前取值交给查看比较。比较时读取源集合下标，不改动 <c>Cards</c>。
/// </summary>
internal sealed class CardGridSortComparer : IComparer
{
    private readonly IList<MediaCardViewModel> mCardList;
    private readonly IReadOnlyList<GridSortRule> mRuleList;

    public CardGridSortComparer(IList<MediaCardViewModel> cardList, IReadOnlyList<GridSortRule> ruleList)
    {
        mCardList = cardList;
        mRuleList = ruleList;
    }

    /// <summary>
    /// 按当前查看规则比较两张卡片。
    /// </summary>
    public int Compare(object? x, object? y)
    {
        if (x is not MediaCardViewModel left || y is not MediaCardViewModel right)
        {
            return 0;
        }

        return GridSort.Compare(ToFact(left), ToFact(right), mRuleList);
    }

    /// <summary>
    /// 取出名称、时间、星级、大小与编目下标。
    /// </summary>
    private GridSortFact ToFact(MediaCardViewModel card)
    {
        var index = mCardList.IndexOf(card);
        return new GridSortFact(
            index < 0 ? int.MaxValue : index,
            card.SortName,
            card.SortTimeUtc,
            card.StarCount,
            card.SortSize);
    }
}
