using System.IO;
using System.Windows;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 心得配图的压缩、恢复和清理。编排在核心层，这里只接线进度窗、结果框和按钮。
/// 本地落后于 GitHub 时这些写入会被拒绝。
/// </summary>
public sealed partial class ShellViewModel
{
    private enum NoteImageRetryKind
    {
        None,
        Compress,
        Restore,
        PurgeNote,
        PurgeAll
    }

    private bool mNoteImageCommandsReady;
    private NoteImageRetryKind mRetryKind;
    private readonly List<string> mRetryRelativePathList = new();
    private string mRetryFolder = "";
    private int? mRetryManualQuality;
    private bool mRetryStartFromBackup;
    private Window? mRetryOwner;

    public RelayCommand CompressOverLimitCommand { get; }
    public RelayCommand CompressSelectedCommand { get; }
    public RelayCommand RestoreOriginalCommand { get; }
    public RelayCommand RecompressCommand { get; }
    public RelayCommand RetryNoteImageCommand { get; }
    public RelayCommand PurgeNoteBackupsCommand { get; }

    /// <summary>
    /// 当前打开的是一篇心得时，检视栏和底栏显示压缩按钮。
    /// </summary>
    public bool ShowNoteCompress => mSelectedNav?.Work is { SourceKind: "note" };

    /// <summary>
    /// 上一批有被占用而跳过的文件，并且仍然对着当时的笔记或工作区。
    /// </summary>
    public bool ShowRetryNoteImage => mRetryKind != NoteImageRetryKind.None && RetryStillApplies();

    /// <summary>
    /// 所选配图都已发布时，悬停说明用统一文案。否则不显示提示。
    /// </summary>
    public string? CompressSelectedToolTip => SyncWriteToolTip ?? PublishedToolTip(SelectedNoteStages());

    /// <summary>
    /// 所选配图都已发布时，恢复按钮的悬停说明。
    /// </summary>
    public string? RestoreOriginalToolTip => SyncWriteToolTip ?? PublishedToolTip(SelectedNoteStages());

    /// <summary>
    /// 当前这张或全部所选都已发布时，重新压缩按钮的悬停说明。
    /// </summary>
    public string? RecompressToolTip
    {
        get
        {
            if (SyncWriteToolTip != null)
            {
                return SyncWriteToolTip;
            }

            var stageList = SelectedNoteStages();
            if (stageList.Count == 0)
            {
                return null;
            }

            if (stageList.Count == 1 || stageList.All(stage => NoteImageGuard.PublishedReason(stage).Length > 0))
            {
                return PublishedToolTip(stageList);
            }

            return null;
        }
    }

    /// <summary>
    /// 压缩当前笔记里全部超限配图。已发布的配图由压缩器跳过。
    /// </summary>
    private void CompressOverLimit()
    {
        if (RefuseWorkspaceWrite())
        {
            return;
        }

        _ = CompressAsync(OverLimitTargets(), null, false, "正在压缩超限配图", "压缩结果", null);
    }

    /// <summary>
    /// 只压缩笔记栏里选中的配图。
    /// </summary>
    private void CompressSelected()
    {
        if (RefuseWorkspaceWrite())
        {
            return;
        }

        _ = CompressAsync(SelectedCompressTargets(), null, false, "正在压缩所选配图", "压缩结果", null);
    }

    /// <summary>
    /// 从原图备份重新压缩当前选中的一张，质量由小窗决定。
    /// </summary>
    private void Recompress()
    {
        if (RefuseWorkspaceWrite())
        {
            return;
        }

        var stageList = SelectedNoteStages();
        var folderName = CurrentNoteId();
        if (stageList.Count != 1 || folderName == null || NoteImageGuard.PublishedReason(stageList[0]).Length > 0)
        {
            return;
        }

        var relative = NoteRules.RelativeToNoteFolder(stageList[0].StageRel, folderName);
        var fileName = Path.GetFileName(relative.Replace('/', Path.DirectorySeparatorChar));
        var choice = RecompressWindow.Show(fileName);
        if (!choice.Accepted)
        {
            return;
        }

        _ = CompressAsync(
            new List<NoteCompressTarget> { new() { RelativePath = relative } },
            choice.ManualQuality,
            true,
            "正在重新压缩",
            "重新压缩结果",
            null);
    }

    /// <summary>
    /// 用原图备份换回选中的配图。已发布的只记入结果，不写文件。
    /// </summary>
    private void RestoreOriginal()
    {
        if (RefuseWorkspaceWrite())
        {
            return;
        }

        _ = RestoreAsync(SelectedRestoreTargets());
    }

    /// <summary>
    /// 清理当前笔记的原图备份。执行前先确认数量和总大小。
    /// </summary>
    private void PurgeNoteBackups()
    {
        if (RefuseWorkspaceWrite())
        {
            return;
        }

        _ = PurgeNoteAsync(alreadyConfirmed: false);
    }

    /// <summary>
    /// 从设置窗清理全部笔记的原图备份。确认框和进度窗挂在设置窗上，避免被挡住。
    /// </summary>
    private void PurgeAllNoteBackups(Window owner)
    {
        if (RefuseWorkspaceWrite())
        {
            return;
        }

        _ = PurgeAllAsync(owner, alreadyConfirmed: false);
    }

    /// <summary>
    /// 只重做上一批里被占用而跳过的项。
    /// </summary>
    private void RetryNoteImage()
    {
        if (RefuseWorkspaceWrite())
        {
            return;
        }

        _ = RetryNoteImageAsync();
    }

    /// <summary>
    /// 按上一批记下的种类重试。
    /// </summary>
    private async Task RetryNoteImageAsync()
    {
        switch (mRetryKind)
        {
            case NoteImageRetryKind.Compress:
                var folderName = CurrentNoteId();
                var targets = mRetryRelativePathList.Select(path => new NoteCompressTarget
                {
                    RelativePath = path,
                    PublishedReason = folderName == null ? null : EmptyToNull(PublishedReasonFor(folderName, path))
                }).ToList();
                await CompressAsync(targets, mRetryManualQuality, mRetryStartFromBackup, "正在重试压缩", "压缩结果", null);
                break;
            case NoteImageRetryKind.Restore:
                var restoreList = mRetryRelativePathList
                    .Select(path => new NoteImageRestoreTarget { RelativePath = path })
                    .ToList();
                await RestoreAsync(restoreList);
                break;
            case NoteImageRetryKind.PurgeNote:
                await PurgeNoteAsync(alreadyConfirmed: true);
                break;
            case NoteImageRetryKind.PurgeAll:
                await PurgeAllAsync(mRetryOwner, alreadyConfirmed: true);
                break;
        }
    }

    /// <summary>
    /// 压缩一批配图，写 run.log，然后刷新一次编目并弹出结果框。
    /// </summary>
    private async Task CompressAsync(
        IReadOnlyList<NoteCompressTarget> targetList,
        int? manualQuality,
        bool startFromBackup,
        string progressTitle,
        string resultTitle,
        Window? owner)
    {
        if (mIsExecuting || mSession == null || RefuseWorkspaceWrite())
        {
            return;
        }

        var folder = CurrentNoteFolderPath();
        if (folder == null || targetList.Count == 0)
        {
            StatusText = "没有需要压缩的配图。";
            return;
        }

        var options = CreateCompressOptions(manualQuality, startFromBackup);
        if (options == null)
        {
            return;
        }

        var batch = NoteCompressBatch.Empty;
        BeginNoteBackupJob();
        try
        {
            var run = ProgressDialog.Run(progressTitle, progress =>
            {
                batch = NoteCompressBatch.Compress(folder, targetList, options, progress);
                NoteCompressBatch.WriteLog(batch);
                return new BatchRunResult { Ok = true, FailureText = batch.Summary };
            }, owner);
            if (batch.FileList.Count == 0 && !run.Ok)
            {
                StatusText = run.FailureText;
                TextDialog.Show("无法压缩", run.FailureText, owner);
                return;
            }

            RememberCompressRetry(folder, batch, manualQuality, startFromBackup);
            await RefreshAfterNoteBackupJobAsync();
            StatusText = batch.Summary;
        }
        finally
        {
            EndNoteBackupJob();
        }

        if (batch.FileList.Count == 0 && batch.DetailLineList.Count == 0)
        {
            return;
        }

        if (CompressResultWindow.Show(resultTitle, batch.Summary, batch.DetailLineList, batch.CanRetry, owner))
        {
            await RetryNoteImageAsync();
        }
    }

    /// <summary>
    /// 恢复一批配图，写 run.log，然后刷新一次编目并弹出结果框。
    /// </summary>
    private async Task RestoreAsync(IReadOnlyList<NoteImageRestoreTarget> targetList)
    {
        if (mIsExecuting || mSession == null || RefuseWorkspaceWrite())
        {
            return;
        }

        var folder = CurrentNoteFolderPath();
        if (folder == null || targetList.Count == 0)
        {
            StatusText = "没有可以恢复的配图。";
            return;
        }

        var report = new NoteBackupReport { Summary = "没有可以恢复的配图。" };
        BeginNoteBackupJob();
        try
        {
            var run = ProgressDialog.Run("正在恢复原图", progress =>
            {
                report = NoteBackupJob.Restore(folder, targetList, progress);
                NoteBackupJob.WriteLog("恢复原图", report);
                return new BatchRunResult { Ok = true, FailureText = report.Summary };
            });
            if (report.FileList.Count == 0 && !run.Ok)
            {
                StatusText = run.FailureText;
                TextDialog.Show("无法恢复", run.FailureText);
                return;
            }

            RememberFileRetry(NoteImageRetryKind.Restore, folder, report.RetryRelativePathList, null);
            await RefreshAfterNoteBackupJobAsync();
            StatusText = report.Summary;
        }
        finally
        {
            EndNoteBackupJob();
        }

        if (CompressResultWindow.Show("恢复结果", report.Summary, report.DetailLineList, report.CanRetry))
        {
            await RetryNoteImageAsync();
        }
    }

    /// <summary>
    /// 清理当前笔记的原图备份。重试时不再询问确认。
    /// </summary>
    private async Task PurgeNoteAsync(bool alreadyConfirmed)
    {
        if (mIsExecuting || !HasOpenNote() || RefuseWorkspaceWrite())
        {
            return;
        }

        var folder = CurrentNoteFolderPath();
        if (folder == null)
        {
            return;
        }

        if (!alreadyConfirmed)
        {
            NoteImageBackupMeasure measure;
            try
            {
                measure = NoteImageBackup.Measure(folder);
            }
            catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or IOException)
            {
                TextDialog.Show("无法清理", exception.Message);
                return;
            }

            if (measure.FileCount == 0)
            {
                StatusText = "这篇笔记没有原图备份。";
                TextDialog.Show("清理本篇原图备份", "这篇笔记没有原图备份。");
                return;
            }

            if (!ConfirmIntentDialog.Show(NoteBackupJob.FormatPurgeConfirm(measure), false, "清理原图备份", "清理"))
            {
                return;
            }
        }

        var report = await RunPurgeAsync(
            "正在清理本篇原图备份",
            null,
            progress => NoteBackupJob.Purge(folder, progress));
        if (report == null)
        {
            return;
        }

        RememberFileRetry(NoteImageRetryKind.PurgeNote, folder, report.RetryRelativePathList, null);
        if (CompressResultWindow.Show("清理结果", report.Summary, report.DetailLineList, report.CanRetry))
        {
            await RetryNoteImageAsync();
        }
    }

    /// <summary>
    /// 清理心得栏目里每一篇笔记的原图备份。重试时不再询问确认。
    /// </summary>
    private async Task PurgeAllAsync(Window? owner, bool alreadyConfirmed)
    {
        if (mIsExecuting || mSession == null || RefuseWorkspaceWrite())
        {
            return;
        }

        var channel = NotesChannelDirectory();
        if (string.IsNullOrWhiteSpace(channel) || !Directory.Exists(channel))
        {
            TextDialog.Show("无法清理", "找不到心得栏目，请先打开工作区。", owner);
            return;
        }

        if (!alreadyConfirmed)
        {
            var measure = NoteBackupJob.MeasureChannel(channel);
            if (measure.FileCount == 0)
            {
                StatusText = "没有需要清理的原图备份。";
                TextDialog.Show("清理全部原图备份", "没有需要清理的原图备份。", owner);
                return;
            }

            if (!ConfirmIntentDialog.Show(NoteBackupJob.FormatPurgeConfirm(measure), false, "清理原图备份", "清理", owner))
            {
                return;
            }
        }

        var report = await RunPurgeAsync(
            "正在清理全部原图备份",
            owner,
            progress => NoteBackupJob.PurgeChannel(channel, progress));
        if (report == null)
        {
            return;
        }

        RememberFileRetry(NoteImageRetryKind.PurgeAll, "", report.RetryRelativePathList, owner);
        if (CompressResultWindow.Show("清理结果", report.Summary, report.DetailLineList, report.CanRetry, owner))
        {
            await RetryNoteImageAsync();
        }
    }

    /// <summary>
    /// 在进度窗里清理备份，暂停投放箱监视，结束后只刷新一次。
    /// </summary>
    private async Task<NoteBackupReport?> RunPurgeAsync(
        string progressTitle,
        Window? owner,
        Func<IProgress<PublishProgress>, NoteBackupReport> purge)
    {
        if (mIsExecuting || RefuseWorkspaceWrite())
        {
            return null;
        }

        NoteBackupReport? report = null;
        BeginNoteBackupJob();
        try
        {
            var run = ProgressDialog.Run(progressTitle, progress =>
            {
                report = purge(progress);
                NoteBackupJob.WriteLog("清理原图备份", report);
                return new BatchRunResult { Ok = true, FailureText = report.Summary };
            }, owner);
            if (report == null)
            {
                StatusText = run.FailureText;
                TextDialog.Show("无法清理", string.IsNullOrWhiteSpace(run.FailureText) ? "清理没有返回结果。" : run.FailureText, owner);
                return null;
            }

            await RefreshAfterNoteBackupJobAsync();
            StatusText = report.Summary;
            return report;
        }
        finally
        {
            EndNoteBackupJob();
        }
    }

    /// <summary>
    /// 用本机设置和仓库里的阈值组成这次压缩的参数。阈值文件损坏时不压缩。
    /// </summary>
    private NoteCompressOptions? CreateCompressOptions(int? manualQuality, bool startFromBackup)
    {
        if (mSession == null)
        {
            return null;
        }

        NoteImageLimits limits;
        try
        {
            limits = NoteImageLimitsStore.Load(mSession.Profile);
        }
        catch (NoteImageLimitsException exception)
        {
            TextDialog.Show("无法压缩", exception.Message);
            return null;
        }

        return NoteCompressOptions.FromSettings(mSettings, limits)
            .ForImage(null, null, manualQuality, startFromBackup);
    }

    /// <summary>
    /// 当前笔记里超过阈值的配图。已发布的也列进来，压缩时跳过并写进结果。
    /// </summary>
    private List<NoteCompressTarget> OverLimitTargets()
    {
        var list = new List<NoteCompressTarget>();
        var folderName = CurrentNoteId();
        if (folderName == null || mNoteAudit == null)
        {
            return list;
        }

        foreach (var image in mNoteAudit.OverLimitList)
        {
            list.Add(new NoteCompressTarget
            {
                RelativePath = image.RelativePath,
                PublishedReason = EmptyToNull(PublishedReasonFor(folderName, image.RelativePath))
            });
        }

        return list;
    }

    /// <summary>
    /// 笔记栏当前选中的配图。
    /// </summary>
    private List<NoteCompressTarget> SelectedCompressTargets()
    {
        var list = new List<NoteCompressTarget>();
        var folderName = CurrentNoteId();
        if (folderName == null)
        {
            return list;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in SelectedNoteStages())
        {
            var relative = NoteRules.RelativeToNoteFolder(stage.StageRel, folderName);
            if (!seen.Add(JsonUtil.ToRel(relative)))
            {
                continue;
            }

            list.Add(new NoteCompressTarget
            {
                RelativePath = relative,
                PublishedReason = EmptyToNull(NoteImageGuard.PublishedReason(stage))
            });
        }

        return list;
    }

    /// <summary>
    /// 选中的、可以恢复或需要说明已发布的配图。没有备份且未发布的不进入列表。
    /// </summary>
    private List<NoteImageRestoreTarget> SelectedRestoreTargets()
    {
        var list = new List<NoteImageRestoreTarget>();
        var folderName = CurrentNoteId();
        if (folderName == null)
        {
            return list;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in SelectedNoteStages())
        {
            var relative = NoteRules.RelativeToNoteFolder(stage.StageRel, folderName);
            if (!seen.Add(JsonUtil.ToRel(relative)))
            {
                continue;
            }

            var reason = NoteImageGuard.PublishedReason(stage);
            if (reason.Length == 0 && !HasBackup(stage))
            {
                continue;
            }

            list.Add(new NoteImageRestoreTarget
            {
                RelativePath = relative,
                PublishedReason = EmptyToNull(reason)
            });
        }

        return list;
    }

    /// <summary>
    /// 压缩、恢复或清理开始时暂停投放箱监视引起的反复刷新。
    /// </summary>
    private void BeginNoteBackupJob()
    {
        mIsExecuting = true;
        mWatchDebounce?.Stop();
        RaiseNoteImageCommands();
        RaiseNoteExecutionCommands();
    }

    /// <summary>
    /// 处理结束。调用方随后应刷新一次编目。
    /// </summary>
    private void EndNoteBackupJob()
    {
        mWatchDebounce?.Stop();
        mIsExecuting = false;
        RaiseNoteImageCommands();
        RaiseNoteExecutionCommands();
    }

    /// <summary>
    /// 执行期间同步刷新依赖 mIsExecuting 的笔记动作，避免进度窗关闭后、编目刷新完成前仍可点击。
    /// </summary>
    private void RaiseNoteExecutionCommands()
    {
        ExecuteCommand.RaiseCanExecute();
        PublishNoteCommand.RaiseCanExecute();
        HideNoteCommand.RaiseCanExecute();
        RestoreNoteCommand.RaiseCanExecute();
        RecycleNoteCommand.RaiseCanExecute();
    }

    /// <summary>
    /// 文件已经落盘后重新编目一次。监视在此期间仍然暂停。
    /// </summary>
    private async Task RefreshAfterNoteBackupJobAsync()
    {
        if (mSession == null)
        {
            RefreshNoteImageAudit();
            ApplyNoteImageBadges();
            UpdateInspector();
            return;
        }

        await ReloadAsync(mSession.Profile.ProfilePath, coverGrid: false);
    }

    /// <summary>
    /// 记下这一批压缩里被占用的路径，供检视栏和结果框重试。
    /// </summary>
    private void RememberCompressRetry(string folder, NoteCompressBatchResult batch, int? manualQuality, bool startFromBackup)
    {
        ClearNoteImageRetry();
        if (!batch.CanRetry)
        {
            RaiseNoteImageCommands();
            return;
        }

        mRetryKind = NoteImageRetryKind.Compress;
        mRetryFolder = folder;
        mRetryManualQuality = manualQuality;
        mRetryStartFromBackup = startFromBackup;
        mRetryRelativePathList.AddRange(batch.RetryRelativePathList);
        RaiseNoteImageCommands();
    }

    /// <summary>
    /// 记下恢复或清理里被占用的路径。
    /// </summary>
    private void RememberFileRetry(NoteImageRetryKind kind, string folder, IReadOnlyList<string> relativePathList, Window? owner)
    {
        ClearNoteImageRetry();
        if (relativePathList.Count == 0)
        {
            RaiseNoteImageCommands();
            return;
        }

        mRetryKind = kind;
        mRetryFolder = folder;
        mRetryOwner = owner;
        mRetryRelativePathList.AddRange(relativePathList);
        RaiseNoteImageCommands();
    }

    /// <summary>
    /// 换成别的笔记后，不再对上一篇提供重试。清理全部备份不跟某一篇绑定。
    /// </summary>
    private void ClearNoteImageRetryIfFolderChanged()
    {
        if (mRetryKind is NoteImageRetryKind.None or NoteImageRetryKind.PurgeAll)
        {
            return;
        }

        var folder = CurrentNoteFolderPath();
        if (string.Equals(folder, mRetryFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ClearNoteImageRetry();
    }

    /// <summary>
    /// 清掉重试名单。
    /// </summary>
    private void ClearNoteImageRetry()
    {
        mRetryKind = NoteImageRetryKind.None;
        mRetryRelativePathList.Clear();
        mRetryFolder = "";
        mRetryManualQuality = null;
        mRetryStartFromBackup = false;
        mRetryOwner = null;
    }

    /// <summary>
    /// 重试按钮是否还对得上当前界面。
    /// </summary>
    private bool RetryStillApplies()
    {
        if (mRetryKind == NoteImageRetryKind.PurgeAll)
        {
            return mSession != null;
        }

        var folder = CurrentNoteFolderPath();
        return folder != null && string.Equals(folder, mRetryFolder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 有打开的心得，并且当前没有别的执行在进行。
    /// </summary>
    private bool HasOpenNote()
    {
        return mSession != null
            && !mIsExecuting
            && mSelectedNav?.Work is { SourceKind: "note" };
    }

    /// <summary>
    /// 本篇还有超限配图时可以一键压缩。
    /// </summary>
    private bool CanCompressOverLimit()
    {
        return HasOpenNote()
            && WorkspaceSyncAllowsWrite
            && mNoteAuditError.Length == 0
            && mNoteAudit is { OverLimitCount: > 0 };
    }

    /// <summary>
    /// 选中了至少一张尚未发布的配图时可以压缩所选。
    /// </summary>
    private bool CanCompressSelected()
    {
        if (!HasOpenNote() || !WorkspaceSyncAllowsWrite || mNoteAuditError.Length > 0)
        {
            return false;
        }

        var stageList = SelectedNoteStages();
        return stageList.Count > 0 && stageList.Any(stage => NoteImageGuard.PublishedReason(stage).Length == 0);
    }

    /// <summary>
    /// 选中的配图里有尚未发布、并且留着原图备份的，才可以恢复。
    /// </summary>
    private bool CanRestoreOriginal()
    {
        if (!HasOpenNote() || !WorkspaceSyncAllowsWrite)
        {
            return false;
        }

        return SelectedNoteStages().Any(stage =>
            NoteImageGuard.PublishedReason(stage).Length == 0 && HasBackup(stage));
    }

    /// <summary>
    /// 只对当前这一张尚未发布、并且有原图备份的配图重新压缩。
    /// </summary>
    private bool CanRecompress()
    {
        if (!HasOpenNote() || !WorkspaceSyncAllowsWrite)
        {
            return false;
        }

        var stageList = SelectedNoteStages();
        return stageList.Count == 1
            && NoteImageGuard.PublishedReason(stageList[0]).Length == 0
            && HasBackup(stageList[0]);
    }

    /// <summary>
    /// 上一批留下了可重试的占用项。
    /// </summary>
    private bool CanRetryNoteImage()
    {
        if (mSession == null || mIsExecuting || !WorkspaceSyncAllowsWrite || !ShowRetryNoteImage)
        {
            return false;
        }

        return mRetryKind switch
        {
            NoteImageRetryKind.Compress or NoteImageRetryKind.Restore => mRetryRelativePathList.Count > 0,
            NoteImageRetryKind.PurgeNote => HasOpenNote(),
            NoteImageRetryKind.PurgeAll => mRetryRelativePathList.Count > 0,
            _ => false
        };
    }

    /// <summary>
    /// 打开一篇心得时可以清理这篇的原图备份。没有备份时由点击后再说明。
    /// </summary>
    private bool CanPurgeNoteBackups()
    {
        return HasOpenNote() && WorkspaceSyncAllowsWrite;
    }

    /// <summary>
    /// 刷新压缩相关按钮的可执行状态和悬停说明。
    /// </summary>
    private void RaiseNoteImageCommands()
    {
        if (!mNoteImageCommandsReady)
        {
            return;
        }

        CompressOverLimitCommand.RaiseCanExecute();
        CompressSelectedCommand.RaiseCanExecute();
        RestoreOriginalCommand.RaiseCanExecute();
        RecompressCommand.RaiseCanExecute();
        RetryNoteImageCommand.RaiseCanExecute();
        PurgeNoteBackupsCommand.RaiseCanExecute();
        Raise(nameof(ShowNoteCompress));
        Raise(nameof(ShowRetryNoteImage));
        Raise(nameof(CompressSelectedToolTip));
        Raise(nameof(RestoreOriginalToolTip));
        Raise(nameof(RecompressToolTip));
    }

    /// <summary>
    /// 当前网格里选中的、属于这篇笔记的配图。正文和附属文件夹不算。
    /// </summary>
    private List<StageItem> SelectedNoteStages()
    {
        var list = new List<StageItem>();
        var folderName = CurrentNoteId();
        if (folderName == null)
        {
            return list;
        }

        foreach (var card in SelectedCardList)
        {
            var stage = card.Stage;
            if (stage == null || !IsStoredNoteImage(stage, folderName))
            {
                continue;
            }

            list.Add(stage);
        }

        return list;
    }

    /// <summary>
    /// 这张投放箱文件是不是这篇笔记里可压缩的配图。
    /// </summary>
    private static bool IsStoredNoteImage(StageItem stage, string folderName)
    {
        if (!NoteRules.IsNotesChannel(stage.ChannelKey))
        {
            return false;
        }

        var extension = Path.GetExtension(stage.StageRel);
        if (!NoteRules.StoredImageExtSet.Contains(extension))
        {
            return false;
        }

        if (MediaPathRules.IsNoteAuxiliaryRel(stage.StageRel))
        {
            return false;
        }

        var stageRel = JsonUtil.ToRel(stage.StageRel);
        var marker = "/" + folderName + "/";
        return stageRel.Contains(marker, StringComparison.OrdinalIgnoreCase)
            || stageRel.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按相对路径找出这篇笔记的投放箱条目，用来判断是否已发布。
    /// </summary>
    private StageItem? FindStage(string folderName, string relativePath)
    {
        if (mSession == null)
        {
            return null;
        }

        var key = JsonUtil.ToRel(relativePath);
        foreach (var stage in mSession.StageItems)
        {
            if (!IsStoredNoteImage(stage, folderName))
            {
                continue;
            }

            var relative = JsonUtil.ToRel(NoteRules.RelativeToNoteFolder(stage.StageRel, folderName));
            if (string.Equals(relative, key, StringComparison.OrdinalIgnoreCase))
            {
                return stage;
            }
        }

        return null;
    }

    /// <summary>
    /// 已发布时返回统一原因，否则返回空字符串。
    /// </summary>
    private string PublishedReasonFor(string folderName, string relativePath)
    {
        var stage = FindStage(folderName, relativePath);
        return stage == null ? "" : NoteImageGuard.PublishedReason(stage);
    }

    /// <summary>
    /// 这张配图在 .originals 里是否还有备份。
    /// </summary>
    private bool HasBackup(StageItem stage)
    {
        var folder = CurrentNoteFolderPath();
        var folderName = CurrentNoteId();
        if (folder == null || folderName == null)
        {
            return false;
        }

        var relative = NoteRules.RelativeToNoteFolder(stage.StageRel, folderName);
        return NoteImagePrompt.HasOriginalBackup(folder, relative);
    }

    /// <summary>
    /// 当前心得的文件夹名。不是心得时为空。
    /// </summary>
    private string? CurrentNoteId()
    {
        return mSelectedNav?.Work is { SourceKind: "note" } work ? work.Id : null;
    }

    /// <summary>
    /// 当前心得文件夹的完整路径。不是心得时为空。
    /// </summary>
    private string? CurrentNoteFolderPath()
    {
        var folderName = CurrentNoteId();
        if (folderName == null || mSession == null)
        {
            return null;
        }

        return NoteFolderPath(folderName);
    }

    /// <summary>
    /// 心得栏目在投放箱中的目录。
    /// </summary>
    private string? NotesChannelDirectory()
    {
        if (mSession == null)
        {
            return null;
        }

        var channel = mSession.Profile.Channels.FirstOrDefault(item => NoteRules.IsNotesChannel(item.Key));
        if (channel == null)
        {
            return null;
        }

        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(mSession.Profile, mSession.Profile.StageRoot);
        return Path.Combine(
            stageRoot,
            WorkspaceProfileLoader.ChannelStageRelative(mSession.Profile, channel).Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 所选配图全部已发布时返回悬停文案，否则返回空。
    /// </summary>
    private static string? PublishedToolTip(IReadOnlyList<StageItem> stageList)
    {
        if (stageList.Count == 0)
        {
            return null;
        }

        return stageList.All(stage => NoteImageGuard.PublishedReason(stage).Length > 0)
            ? NoteImageGuard.PublishedBlockText
            : null;
    }

    /// <summary>
    /// 空白原因改成空，便于压缩器把这张当成可以处理。
    /// </summary>
    private static string? EmptyToNull(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
