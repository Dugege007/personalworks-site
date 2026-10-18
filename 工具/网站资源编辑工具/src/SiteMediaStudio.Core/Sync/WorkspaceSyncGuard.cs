namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 工作区相对 GitHub 的同步状态。尚未检查和无法检查都不禁止写入。
/// </summary>
public enum WorkspaceSyncKind
{
    /// <summary>
    /// 还没有完成过检查。
    /// </summary>
    Unchecked,

    /// <summary>
    /// 本地分支没有落后于 GitHub 上的对应分支。
    /// </summary>
    Current,

    /// <summary>
    /// 本地分支落后于 GitHub。工具内写操作应拒绝。
    /// </summary>
    Behind,

    /// <summary>
    /// 网络不通、不是仓库、没有远程或命令失败。由用户决定是否继续修改。
    /// </summary>
    Unknown
}

/// <summary>
/// 一次同步检查的结果，供界面显示和写入守卫使用。
/// </summary>
public sealed class WorkspaceSyncReport
{
    private WorkspaceSyncReport(WorkspaceSyncKind kind, string notice, DateTimeOffset checkedAt, string repositoryRoot)
    {
        Kind = kind;
        Notice = notice;
        CheckedAt = checkedAt;
        RepositoryRoot = repositoryRoot ?? "";
    }

    /// <summary>
    /// 尚未检查。不显示提示，也不禁止写入。
    /// </summary>
    public static WorkspaceSyncReport Unchecked { get; } = new(WorkspaceSyncKind.Unchecked, "", default, "");

    /// <summary>
    /// 状态。
    /// </summary>
    public WorkspaceSyncKind Kind { get; }

    /// <summary>
    /// 检视栏和底栏文案。已同步或尚未检查时为空。
    /// </summary>
    public string Notice { get; }

    /// <summary>
    /// 这次检查使用的时钟读数。尚未检查时为默认值。
    /// </summary>
    public DateTimeOffset CheckedAt { get; }

    /// <summary>
    /// git 仓库根。无法定位时为空。界面不展示。
    /// </summary>
    public string RepositoryRoot { get; }

    /// <summary>
    /// 本地没有落后。
    /// </summary>
    public static WorkspaceSyncReport Current(DateTimeOffset checkedAt, string repositoryRoot)
    {
        return new WorkspaceSyncReport(WorkspaceSyncKind.Current, "", checkedAt, repositoryRoot);
    }

    /// <summary>
    /// 本地落后于 GitHub。
    /// </summary>
    public static WorkspaceSyncReport Behind(DateTimeOffset checkedAt, string repositoryRoot)
    {
        return new WorkspaceSyncReport(
            WorkspaceSyncKind.Behind,
            WorkspaceSyncGuard.BehindNotice,
            checkedAt,
            repositoryRoot);
    }

    /// <summary>
    /// 无法完成检查。
    /// </summary>
    public static WorkspaceSyncReport Unknown(DateTimeOffset checkedAt, string repositoryRoot = "")
    {
        return new WorkspaceSyncReport(
            WorkspaceSyncKind.Unknown,
            WorkspaceSyncGuard.UnknownNotice,
            checkedAt,
            repositoryRoot);
    }
}

/// <summary>
/// 按同步状态决定能否写入投放箱或心得索引。落后时拒绝，无法检查时不硬性禁止。
/// </summary>
public static class WorkspaceSyncGuard
{
    /// <summary>
    /// 本地落后时检视栏和底栏使用的句子。
    /// </summary>
    public const string BehindNotice = "本地版本落后于 GitHub，请先同步后再修改";

    /// <summary>
    /// 无法连接或无法检查时检视栏和底栏使用的句子。
    /// </summary>
    public const string UnknownNotice = "无法连接 GitHub，同步状态未知";

    /// <summary>
    /// 尝试开始一次会改投放箱或心得索引的操作。落后时返回 false，并把原因放进 <paramref name="reason"/>。
    /// 已同步、尚未检查或无法检查时返回 true，原因是空字符串。
    /// </summary>
    public static bool TryBeginWrite(WorkspaceSyncKind kind, out string reason)
    {
        if (kind == WorkspaceSyncKind.Behind)
        {
            reason = BehindNotice;
            return false;
        }

        reason = "";
        return true;
    }
}
