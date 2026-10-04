using System.Collections.ObjectModel;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 网格与对照行按稳定键就地增删。
/// </summary>
internal static class CardGridSync
{
    /// <summary>
    /// 复用已有卡片，只插入、移除或挪动变化项。
    /// </summary>
    public static void SyncCards(
        ObservableCollection<MediaCardViewModel> target,
        IReadOnlyList<MediaCardViewModel> desired)
    {
        var existingDict = new Dictionary<string, MediaCardViewModel>(StringComparer.Ordinal);
        foreach (var card in target)
        {
            var key = card.CardKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                existingDict[key] = card;
            }
        }

        var nextList = new List<MediaCardViewModel>(desired.Count);
        foreach (var incoming in desired)
        {
            var key = incoming.CardKey;
            if (!string.IsNullOrWhiteSpace(key) && existingDict.TryGetValue(key, out var existing))
            {
                existing.ApplyCatalog(incoming);
                nextList.Add(existing);
            }
            else
            {
                nextList.Add(incoming);
            }
        }

        KeyedListSync.Align(target, nextList, card => card.CardKey ?? "");
    }

    /// <summary>
    /// 复用已有对照行，左右卡片改指同步后的网格对象。
    /// </summary>
    public static void SyncRows(
        ObservableCollection<CompareRowViewModel> target,
        IReadOnlyList<CompareRowViewModel> desired)
    {
        var existingDict = new Dictionary<string, CompareRowViewModel>(StringComparer.Ordinal);
        foreach (var row in target)
        {
            existingDict[row.RowKey] = row;
        }

        var nextList = new List<CompareRowViewModel>(desired.Count);
        foreach (var incoming in desired)
        {
            if (existingDict.TryGetValue(incoming.RowKey, out var existing))
            {
                existing.ApplyCatalog(incoming);
                nextList.Add(existing);
            }
            else
            {
                nextList.Add(incoming);
            }
        }

        KeyedListSync.Align(target, nextList, row => row.RowKey);
    }

    /// <summary>
    /// 把对照行左右改指当前网格里同键的卡片。
    /// </summary>
    public static List<CompareRowViewModel> RemapRows(
        IEnumerable<CompareRowViewModel> rowList,
        IEnumerable<MediaCardViewModel> cardList)
    {
        var cardDict = new Dictionary<string, MediaCardViewModel>(StringComparer.Ordinal);
        foreach (var card in cardList)
        {
            var key = card.CardKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                cardDict[key] = card;
            }
        }

        var nextList = new List<CompareRowViewModel>();
        foreach (var row in rowList)
        {
            nextList.Add(new CompareRowViewModel(
                row.Row,
                Lookup(cardDict, row.Left),
                Lookup(cardDict, row.Right)));
        }

        return nextList;
    }

    /// <summary>
    /// 按卡片键找回已同步对象。
    /// </summary>
    private static MediaCardViewModel? Lookup(
        IReadOnlyDictionary<string, MediaCardViewModel> cardDict,
        MediaCardViewModel? incoming)
    {
        var key = incoming?.CardKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return incoming;
        }

        return cardDict.TryGetValue(key, out var found) ? found : incoming;
    }
}
