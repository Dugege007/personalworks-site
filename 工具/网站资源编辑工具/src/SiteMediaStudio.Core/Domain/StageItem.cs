namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 投放箱中的一份图像、视频或游戏发布包。
/// </summary>
public sealed class StageItem
{
    public required string StageRel { get; init; }
    public required string FullPath { get; init; }
    public required string ChannelKey { get; init; }
    public string? WorkIdGuess { get; init; }

    /// <summary>
    /// 栏目内作品夹相对路径：第一层，或已登记容器下的一层。
    /// </summary>
    public string? StageFolderGuess { get; init; }
    public long Length { get; init; }
    public DateTime LastWriteUtc { get; init; }
    public string? MatchedObject { get; init; }
    public string? WebFullPath { get; init; }
    public string? LedgerStatus { get; init; }

    /// <summary>
    /// 对象已在待发布撤下队列，台账仍为 published。加载时写入，卡片显示已撤下。
    /// </summary>
    public bool IsPendingWithdraw { get; set; }

    /// <summary>
    /// 心得发布索引已收录该正文或配图。不写入媒体台账，再次上页仍允许。
    /// </summary>
    public bool IsNoteListed { get; set; }

    /// <summary>
    /// 心得索引将该篇标为隐藏。卡片显示已隐藏，媒体台账保持原状。
    /// </summary>
    public bool IsNoteHidden { get; set; }

    public bool IsStock { get; init; }

    /// <summary>
    /// 是否为 <c>webgl</c> / <c>build</c> 整包节点。
    /// </summary>
    public bool IsPack { get; init; }

    /// <summary>
    /// 工具网格缩略图路径；整包按 Logo / 图标顺序解析。
    /// </summary>
    public string? ThumbPath { get; init; }
}
