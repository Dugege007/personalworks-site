using System.IO;
using System.Windows;
using System.Windows.Threading;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 工作区是否落后于 GitHub。检查在后台进行，界面只读取最近一次结果。
/// </summary>
public sealed partial class ShellViewModel
{
    private readonly WorkspaceSyncChecker mWorkspaceSync = new(new GitProcessRunner());
    private WorkspaceSyncReport mWorkspaceSyncReport = WorkspaceSyncReport.Unchecked;
    private DispatcherTimer? mWorkspaceSyncTimer;
    private bool mWorkspaceSyncQueued;
    private string mWorkspaceSyncPath = "";
    private int mWorkspaceSyncEpoch;

    /// <summary>
    /// 检视栏和底栏的同步提示。已同步或尚未检查时为空。
    /// </summary>
    public string SyncNotice => mWorkspaceSyncReport.Notice;

    /// <summary>
    /// 有同步提示时显示。
    /// </summary>
    public bool ShowSyncNotice => mWorkspaceSyncReport.Notice.Length > 0;

    /// <summary>
    /// 落后时按钮的悬停说明。已同步或无法检查时不额外提示。
    /// </summary>
    public string? SyncWriteToolTip =>
        mWorkspaceSyncReport.Kind == WorkspaceSyncKind.Behind ? WorkspaceSyncGuard.BehindNotice : null;

    /// <summary>
    /// 当前是否允许写入投放箱和心得索引。无法检查时允许，由用户决定。
    /// </summary>
    private bool WorkspaceSyncAllowsWrite =>
        WorkspaceSyncGuard.TryBeginWrite(mWorkspaceSyncReport.Kind, out _);

    /// <summary>
    /// 落后时拒绝这次写入，并把说明留在底栏。允许写入时返回 false。
    /// </summary>
    private bool RefuseWorkspaceWrite()
    {
        if (WorkspaceSyncGuard.TryBeginWrite(mWorkspaceSyncReport.Kind, out var reason))
        {
            return false;
        }

        StatusText = reason;
        return true;
    }

    /// <summary>
    /// 设置窗和标签窗用来询问当前能不能写。能写时返回空白。
    /// </summary>
    private string? WorkspaceSyncBlockReason()
    {
        return WorkspaceSyncGuard.TryBeginWrite(mWorkspaceSyncReport.Kind, out var reason) ? null : reason;
    }

    /// <summary>
    /// 工作区就绪或选中笔记时刷新同步状态。60 秒内复用上次结果，不在界面线程上等待网络。
    /// </summary>
    private void RequestWorkspaceSyncCheck()
    {
        var path = CurrentWorkspacePath();
        if (path == null)
        {
            return;
        }

        ApplyWorkspaceSync(mWorkspaceSync.Peek(path));
        if (mWorkspaceSync.IsFresh(path))
        {
            ArmWorkspaceSyncTimer();
            return;
        }

        if (mWorkspaceSyncQueued && string.Equals(mWorkspaceSyncPath, path, WorkspacePathComparison))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        mWorkspaceSyncQueued = true;
        mWorkspaceSyncPath = path;
        var epoch = mWorkspaceSyncEpoch;
        Task.Run(() => mWorkspaceSync.Check(path)).ContinueWith(task =>
        {
            dispatcher.BeginInvoke(() =>
            {
                if (epoch != mWorkspaceSyncEpoch)
                {
                    return;
                }

                mWorkspaceSyncQueued = false;
                var current = CurrentWorkspacePath();
                if (current == null || !string.Equals(current, path, WorkspacePathComparison))
                {
                    return;
                }

                if (task.IsFaulted || task.IsCanceled)
                {
                    ApplyWorkspaceSync(WorkspaceSyncReport.Unknown(DateTimeOffset.UtcNow));
                }
                else
                {
                    ApplyWorkspaceSync(task.Result);
                }

                ArmWorkspaceSyncTimer();
            });
        });
    }

    /// <summary>
    /// 换成另一个工作区时丢掉界面上的旧结论。正在进行的检查结束后也不会再写回来。
    /// </summary>
    private void InvalidateWorkspaceSync()
    {
        mWorkspaceSyncEpoch++;
        mWorkspaceSyncQueued = false;
        mWorkspaceSyncTimer?.Stop();
        mWorkspaceSyncReport = WorkspaceSyncReport.Unchecked;
        RaiseSyncNotice();
    }

    /// <summary>
    /// 把一次检查结果显示出来，并刷新会被它拦住的按钮。
    /// </summary>
    private void ApplyWorkspaceSync(WorkspaceSyncReport report)
    {
        mWorkspaceSyncReport = report;
        RaiseSyncNotice();
        RaiseSyncCommands();
        if (ShowResourceTags)
        {
            RefreshResourceTags();
        }
    }

    /// <summary>
    /// 通知同步提示和依赖它的悬停说明。
    /// </summary>
    private void RaiseSyncNotice()
    {
        Raise(nameof(SyncNotice));
        Raise(nameof(ShowSyncNotice));
        Raise(nameof(SyncWriteToolTip));
        Raise(nameof(CompressSelectedToolTip));
        Raise(nameof(RestoreOriginalToolTip));
        Raise(nameof(RecompressToolTip));
    }

    /// <summary>
    /// 刷新会写投放箱或心得索引的命令。
    /// </summary>
    private void RaiseSyncCommands()
    {
        MarkIngestCommand.RaiseCanExecute();
        MarkHideCommand.RaiseCanExecute();
        ExecuteCommand.RaiseCanExecute();
        TagCommand.RaiseCanExecute();
        PublishNoteCommand.RaiseCanExecute();
        EditNoteMetaCommand.RaiseCanExecute();
        HideNoteCommand.RaiseCanExecute();
        RestoreNoteCommand.RaiseCanExecute();
        RaiseNoteImageCommands();
    }

    /// <summary>
    /// 在节流窗口结束时再检查一次，这样外部同步完成后按钮可以恢复。
    /// </summary>
    private void ArmWorkspaceSyncTimer()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(ArmWorkspaceSyncTimer));
            return;
        }

        var path = CurrentWorkspacePath();
        if (path == null)
        {
            return;
        }

        var delay = mWorkspaceSync.TimeUntilStale(path);
        if (delay < TimeSpan.FromSeconds(1))
        {
            delay = TimeSpan.FromSeconds(1);
        }

        mWorkspaceSyncTimer ??= new DispatcherTimer();
        mWorkspaceSyncTimer.Stop();
        mWorkspaceSyncTimer.Interval = delay;
        mWorkspaceSyncTimer.Tick -= OnWorkspaceSyncTick;
        mWorkspaceSyncTimer.Tick += OnWorkspaceSyncTick;
        mWorkspaceSyncTimer.Start();
    }

    /// <summary>
    /// 节流时间到了，再做一次后台检查。
    /// </summary>
    private void OnWorkspaceSyncTick(object? sender, EventArgs e)
    {
        mWorkspaceSyncTimer?.Stop();
        RequestWorkspaceSyncCheck();
    }

    /// <summary>
    /// 当前工作区根。还没加载时为空。
    /// </summary>
    private string? CurrentWorkspacePath()
    {
        var path = mSession?.Profile.ResolvedRoot;
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <summary>
    /// 工作区路径比较。Windows 不区分大小写。
    /// </summary>
    private static StringComparison WorkspacePathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
