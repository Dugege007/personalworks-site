using System.IO;
using System.Threading;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 换表前填好的一张卡所用事实。时间、大小、星级与预览路径都在这里，卡片只接收。
/// </summary>
internal readonly record struct CardFileFacts
{
    public long SortSize { get; init; }

    public string? PreviewPath { get; init; }

    public bool UsesWebPreview { get; init; }

    public DateTime PreviewWriteUtc { get; init; }

    public bool SortTimeReady { get; init; }

    public DateTime? SortTimeUtc { get; init; }

    public int Stars { get; init; }
}

/// <summary>
/// 一次重建的填充。星级草稿只读一份；文件属性与按需拍摄时间只在本类型打开文件。
/// </summary>
internal sealed class CardGridFill
{
    private static readonly AsyncLocal<CardGridFill?> sCurrent = new();

    private readonly WorkspaceSession mSession;
    private readonly StarDraftFile mDraft;
    private readonly bool mReadFileFacts;

    private CardGridFill(WorkspaceSession session, bool readFileFacts)
    {
        mSession = session;
        mDraft = StarDraftStore.Load();
        mReadFileFacts = readFileFacts;
    }

    /// <summary>
    /// 当前重建或刷星正在使用的填充。尚未开始时不可组卡。
    /// </summary>
    internal static CardGridFill Current
    {
        get
        {
            var fill = sCurrent.Value;
            if (fill == null)
            {
                throw new InvalidOperationException("卡片填充尚未开始。");
            }

            return fill;
        }
    }

    /// <summary>
    /// 包住一次组卡或刷星，期间只读一份星级草稿。
    /// </summary>
    internal static T Use<T>(WorkspaceSession session, Func<CardGridFill, T> body)
    {
        return Use(session, readFileFacts: true, body);
    }

    /// <summary>
    /// 只组装可立即显示的卡片壳，不探测文件大小与修改时间。
    /// </summary>
    internal static T UseShells<T>(WorkspaceSession session, Func<CardGridFill, T> body)
    {
        return Use(session, readFileFacts: false, body);
    }

    /// <summary>
    /// 按指定读取边界包住一次组卡。
    /// </summary>
    private static T Use<T>(
        WorkspaceSession session,
        bool readFileFacts,
        Func<CardGridFill, T> body)
    {
        var fill = new CardGridFill(session, readFileFacts);
        var prior = sCurrent.Value;
        sCurrent.Value = fill;
        try
        {
            return body(fill);
        }
        finally
        {
            sCurrent.Value = prior;
        }
    }

    /// <summary>
    /// 包住一次组卡或刷星，期间只读一份星级草稿。
    /// </summary>
    internal static void Use(WorkspaceSession session, Action<CardGridFill> body)
    {
        Use(session, fill =>
        {
            body(fill);
            return 0;
        });
    }

    /// <summary>
    /// 读投放箱卡片在换表前需要的文件事实与星级。
    /// </summary>
    internal CardFileFacts ReadStage(StageItem stage)
    {
        //TODO: 新的按文件卡片事实只加在换表前的填充
        if (!mReadFileFacts)
        {
            var shellPreview = stage.IsPack
                ? stage.ThumbPath
                : !string.IsNullOrWhiteSpace(stage.WebFullPath)
                    ? stage.WebFullPath
                    : stage.FullPath;
            return new CardFileFacts
            {
                SortSize = stage.Length,
                PreviewPath = shellPreview,
                UsesWebPreview = !stage.IsPack && !string.IsNullOrWhiteSpace(stage.WebFullPath),
                Stars = StarsForStage(stage)
            };
        }

        string? preview;
        bool usesWeb;
        if (stage.IsPack)
        {
            preview = !string.IsNullOrWhiteSpace(stage.ThumbPath) && File.Exists(stage.ThumbPath)
                ? stage.ThumbPath
                : null;
            usesWeb = false;
        }
        else
        {
            preview = PreviewPathRules.PreferWebThenOriginal(stage.WebFullPath, stage.FullPath);
            usesWeb = !string.IsNullOrWhiteSpace(stage.WebFullPath)
                && string.Equals(preview, stage.WebFullPath, StringComparison.OrdinalIgnoreCase);
        }

        return new CardFileFacts
        {
            SortSize = stage.Length,
            PreviewPath = preview,
            UsesWebPreview = usesWeb,
            PreviewWriteUtc = ReadWriteUtc(preview),
            Stars = StarsForStage(stage)
        };
    }

    /// <summary>
    /// 读站点卡片在换表前需要的文件事实与星级。正式位缺失时大小为 0。
    /// </summary>
    internal CardFileFacts ReadSite(SiteItem site)
    {
        //TODO: 新的按文件卡片事实只加在换表前的填充
        if (!mReadFileFacts)
        {
            return new CardFileFacts
            {
                PreviewPath = site.FullPath,
                UsesWebPreview = site.FullPath != null,
                Stars = StarsForSite(site)
            };
        }

        long size = 0;
        if (!string.IsNullOrWhiteSpace(site.FullPath) && File.Exists(site.FullPath))
        {
            try
            {
                size = new FileInfo(site.FullPath).Length;
            }
            catch (IOException)
            {
                size = 0;
            }
            catch (UnauthorizedAccessException)
            {
                size = 0;
            }
        }

        return new CardFileFacts
        {
            SortSize = size,
            PreviewPath = site.FullPath,
            UsesWebPreview = site.FullPath != null,
            PreviewWriteUtc = ReadWriteUtc(site.FullPath),
            Stars = StarsForSite(site)
        };
    }

    /// <summary>
    /// 按已载入的草稿解析一张卡的星数。整包与无身份的卡为 0。
    /// </summary>
    internal int StarsFor(MediaCardViewModel? card)
    {
        if (card == null || !TryResolve(mSession, card, out var key, out var published))
        {
            return 0;
        }

        var entry = StarDraftStore.Find(mDraft, mSession.Profile, key);
        return entry?.Stars ?? published;
    }

    /// <summary>
    /// 在独立线程读拍摄时间，不占用主窗消息循环，也不把读取同步封送回主窗。
    /// </summary>
    internal static void ReadSortTimes(IReadOnlyList<MediaCardViewModel> cardList)
    {
        if (cardList.Count == 0)
        {
            return;
        }

        using var done = new ManualResetEventSlim(false);
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var card in cardList)
                {
                    if (card.IsSortTimeReady)
                    {
                        continue;
                    }

                    card.ApplyFilledSortTime(MediaCaptureTime.ResolveUtc(card.SortTimePath));
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        });
        thread.IsBackground = true;
        thread.Name = "SortTime";
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        done.Wait();
        if (error != null)
        {
            throw error;
        }
    }

    /// <summary>
    /// 解析打星身份。已入编用对象键；未上页用投放路径。
    /// </summary>
    internal static bool TryResolve(WorkspaceSession? session, MediaCardViewModel card, out string key, out int published)
    {
        key = "";
        published = 0;
        if (card.Kind == CardKind.Site)
        {
            if (card.Site == null || string.IsNullOrWhiteSpace(card.Site.ObjectKey))
            {
                return false;
            }

            key = StarDraftStore.ObjectKey(card.Site.ObjectKey);
            published = card.Site.Stars;
            return true;
        }

        if (card.Stage == null || card.Stage.IsPack || string.IsNullOrWhiteSpace(card.Stage.StageRel))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(card.Stage.MatchedObject))
        {
            key = StarDraftStore.ObjectKey(card.Stage.MatchedObject);
            published = PublishedStars(session, card.Stage.MatchedObject);
            return true;
        }

        key = StarDraftStore.StageKey(card.Stage.StageRel);
        return true;
    }

    /// <summary>
    /// 投放箱条目的有效星级。
    /// </summary>
    private int StarsForStage(StageItem stage)
    {
        if (stage.IsPack || string.IsNullOrWhiteSpace(stage.StageRel))
        {
            return 0;
        }

        string key;
        int published;
        if (!string.IsNullOrWhiteSpace(stage.MatchedObject))
        {
            key = StarDraftStore.ObjectKey(stage.MatchedObject);
            published = PublishedStars(mSession, stage.MatchedObject);
        }
        else
        {
            key = StarDraftStore.StageKey(stage.StageRel);
            published = 0;
        }

        var entry = StarDraftStore.Find(mDraft, mSession.Profile, key);
        return entry?.Stars ?? published;
    }

    /// <summary>
    /// 站点条目的有效星级。没有对象键则为 0。
    /// </summary>
    private int StarsForSite(SiteItem site)
    {
        if (string.IsNullOrWhiteSpace(site.ObjectKey))
        {
            return 0;
        }

        var entry = StarDraftStore.Find(mDraft, mSession.Profile, StarDraftStore.ObjectKey(site.ObjectKey));
        return entry?.Stars ?? site.Stars;
    }

    /// <summary>
    /// 内容层该对象的星级。没有媒体槽则为 0。
    /// </summary>
    private static int PublishedStars(WorkspaceSession? session, string objectKey)
    {
        if (session == null)
        {
            return 0;
        }

        var normalized = JsonUtil.ToRel(objectKey);
        foreach (var work in session.Works)
        {
            var media = work.Media.FirstOrDefault(item =>
                string.Equals(JsonUtil.ToRel(item.Src ?? ""), normalized, StringComparison.Ordinal));
            if (media != null)
            {
                return media.Stars ?? 0;
            }
        }

        return 0;
    }

    /// <summary>
    /// 读取预览文件的修改时间；缺失则视为默认。
    /// </summary>
    private static DateTime ReadWriteUtc(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return default;
        }

        return File.GetLastWriteTimeUtc(path);
    }
}
