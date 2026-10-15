namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 判断一张心得配图是否已经发布到网站。已发布的配图不能压缩、恢复原图或重新压缩。
/// 检视栏按钮、一键压缩和右键菜单都应调用这里，不要各自再写一套判断。
/// </summary>
public static class NoteImageGuard
{
    /// <summary>
    /// 已发布配图不能压缩时的统一说明。不附带操作建议。
    /// </summary>
    public const string PublishedBlockText = "这张配图已发布到网站，不能压缩";

    /// <summary>
    /// 这张配图若已发布则返回不能压缩的原因，否则返回空字符串。
    /// 主条件读取 <see cref="NoteCatalog.ApplyPublishState"/> 写入的 <see cref="StageItem.IsNoteListed"/>：
    /// 心得索引中存在文件夹相同、<c>Draft</c> 为假、<c>Slug</c> 不为空的条目，并且这张配图的投放路径出现在该条目的 Images 里。
    /// 整篇笔记现在是显示还是隐藏都不影响判断。
    /// 兜底条件是媒体台账状态为 published，用来挡住上页中途失败、台账已写而索引未收录的配图。
    /// </summary>
    public static string PublishedReason(StageItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (item.IsNoteListed || PublishStatus.IsLedgerPublished(item.LedgerStatus))
        {
            return PublishedBlockText;
        }

        return "";
    }
}
