namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 替换或恢复进行到哪一步。测试可以在该步完成后抛出 <see cref="IOException"/>，用来验证失败会回退。
/// </summary>
public enum NoteImageBackupPhase
{
    /// <summary>
    /// 新内容已经写到同一文件夹里以点开头的临时文件，正式文件还没被移走。
    /// </summary>
    TempWritten,

    /// <summary>
    /// 正式位置上的文件已经移开。第一次备份时它在 .originals 里；已经有备份时它在旁边的临时文件里，原备份不动。
    /// </summary>
    OriginalMoved,

    /// <summary>
    /// 临时文件已经改成正式文件名。
    /// </summary>
    TempRenamed,

    /// <summary>
    /// 创建时间和修改时间已经写到新文件上。备份和旁边的临时文件都还没删除。
    /// </summary>
    TimestampsApplied
}

/// <summary>
/// 一次替换、恢复或清理的结果。
/// </summary>
public enum NoteImageBackupKind
{
    /// <summary>
    /// 正式文件已经换成新内容。
    /// </summary>
    Replaced,

    /// <summary>
    /// 正式文件已经换回备份里的原图，备份已删除。
    /// </summary>
    Restored,

    /// <summary>
    /// 这份备份已经交给回收站。
    /// </summary>
    Purged,

    /// <summary>
    /// 文件被占用，没有改动。
    /// </summary>
    Locked,

    /// <summary>
    /// 没有完成。替换和恢复失败时，正式位置上的文件与调用前相同。
    /// </summary>
    Failed
}

/// <summary>
/// 一张配图的替换、恢复或清理结果。
/// </summary>
public sealed class NoteImageBackupResult
{
    /// <summary>
    /// 结果种类。
    /// </summary>
    public NoteImageBackupKind Kind { get; init; }

    /// <summary>
    /// 相对笔记文件夹的路径，使用正斜杠。失败发生在定位之前时可能只是文件名。
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 占用程序的名称。没有查到时为空列表。
    /// </summary>
    public IReadOnlyList<string> HolderNameList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 给人看的一句说明。
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// 这次替换是否新写了一份原图备份。已经有备份时为假，表示没有覆盖第一次的原图。
    /// </summary>
    public bool CreatedBackup { get; init; }

    /// <summary>
    /// 替换成功。
    /// </summary>
    public static NoteImageBackupResult Replaced(string relativePath, bool createdBackup)
    {
        return new NoteImageBackupResult
        {
            Kind = NoteImageBackupKind.Replaced,
            RelativePath = relativePath,
            CreatedBackup = createdBackup,
            Message = createdBackup ? "已替换，原图已备份。" : "已替换，原有备份未覆盖。"
        };
    }

    /// <summary>
    /// 恢复成功。
    /// </summary>
    public static NoteImageBackupResult Restored(string relativePath)
    {
        return new NoteImageBackupResult
        {
            Kind = NoteImageBackupKind.Restored,
            RelativePath = relativePath,
            Message = "已恢复原图，备份已删除。"
        };
    }

    /// <summary>
    /// 备份已送进回收站。
    /// </summary>
    public static NoteImageBackupResult Purged(string relativePath)
    {
        return new NoteImageBackupResult
        {
            Kind = NoteImageBackupKind.Purged,
            RelativePath = relativePath,
            Message = "已送进回收站。"
        };
    }

    /// <summary>
    /// 文件被占用，调用方应跳过这一张。
    /// </summary>
    public static NoteImageBackupResult Locked(string relativePath, IReadOnlyList<string>? holderNameList)
    {
        var names = holderNameList ?? Array.Empty<string>();
        return new NoteImageBackupResult
        {
            Kind = NoteImageBackupKind.Locked,
            RelativePath = relativePath,
            HolderNameList = names,
            Message = LockedMessage(relativePath, names)
        };
    }

    /// <summary>
    /// 操作没有完成。
    /// </summary>
    public static NoteImageBackupResult Failed(string relativePath, string message)
    {
        return new NoteImageBackupResult
        {
            Kind = NoteImageBackupKind.Failed,
            RelativePath = relativePath,
            Message = string.IsNullOrWhiteSpace(message) ? "操作失败。" : message.Trim()
        };
    }

    /// <summary>
    /// 被占用时的说明。查到程序名称时写上名称，查不到时只说明被其他程序占用。
    /// 重试按钮的那句提示由界面另加，这里不写。
    /// </summary>
    public static string LockedMessage(string relativePath, IReadOnlyList<string> holderNameList)
    {
        var fileName = FileNameOf(relativePath);
        if (holderNameList == null || holderNameList.Count == 0)
        {
            return fileName + " 正被其他程序占用，已跳过。";
        }

        return fileName + " 正被 " + string.Join("、", holderNameList) + " 占用，已跳过。";
    }

    /// <summary>
    /// 从相对路径里取出文件名。
    /// </summary>
    private static string FileNameOf(string relativePath)
    {
        var name = Path.GetFileName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? "这张配图" : name;
    }
}

/// <summary>
/// .originals 里备份的数量和总字节数，供清理前的确认框使用。
/// </summary>
public readonly record struct NoteImageBackupMeasure(int FileCount, long TotalBytes);

/// <summary>
/// 清理一篇笔记的原图备份后，每个文件的结果。
/// </summary>
public sealed class NoteImagePurgeResult
{
    /// <summary>
    /// 每个备份文件的结果。没有备份时为空列表。
    /// </summary>
    public IReadOnlyList<NoteImageBackupResult> FileList { get; init; } = Array.Empty<NoteImageBackupResult>();

    /// <summary>
    /// 已经送进回收站的文件数。
    /// </summary>
    public int RecycledCount => FileList.Count(item => item.Kind == NoteImageBackupKind.Purged);

    /// <summary>
    /// 因为被占用而留下的文件数。
    /// </summary>
    public int LockedCount => FileList.Count(item => item.Kind == NoteImageBackupKind.Locked);

    /// <summary>
    /// 没能送进回收站的文件数。
    /// </summary>
    public int FailedCount => FileList.Count(item => item.Kind == NoteImageBackupKind.Failed);
}

/// <summary>
/// 替换心得配图、恢复原图，以及把原图备份送进回收站。
/// 临时文件与配图在同一文件夹，文件名以点开头并以 <c>.compressing</c> 结尾，编目不把它当作图片。
/// </summary>
public static class NoteImageBackup
{
    /// <summary>
    /// 临时文件名的固定后缀。完整例子是 <c>.a.png.compressing</c>。
    /// </summary>
    public const string CompressingSuffix = ".compressing";

    /// <summary>
    /// 用新内容替换笔记里的一张配图。
    /// 先写临时文件，再把原图移到 .originals（保持相对子路径），最后把临时文件改成正式文件名。
    /// 已经有备份时不覆盖第一次的原图。任何一步失败都把正式位置恢复成调用前的文件。
    /// 替换成功后，新文件沿用原文件的创建时间，修改时间改为现在。
    /// </summary>
    public static NoteImageBackupResult Replace(
        string noteFolder,
        string imagePath,
        byte[] newContent,
        Action<NoteImageBackupPhase>? onPhase = null)
    {
        if (newContent == null)
        {
            throw new ArgumentNullException(nameof(newContent));
        }

        if (!TryLocate(noteFolder, imagePath, out var place, out var failure))
        {
            return failure;
        }

        if (!File.Exists(place.ImagePath))
        {
            return NoteImageBackupResult.Failed(place.RelativePath, "找不到要替换的配图。");
        }

        var swap = new Swap(place, onPhase);
        return swap.Replace(newContent);
    }

    /// <summary>
    /// 用 .originals 里的原图换回正式文件，然后删除这份备份。
    /// 没有备份、或任一步失败时，正式位置上的文件保持调用前的样子，备份也还在。
    /// </summary>
    public static NoteImageBackupResult Restore(
        string noteFolder,
        string imagePath,
        Action<NoteImageBackupPhase>? onPhase = null)
    {
        if (!TryLocate(noteFolder, imagePath, out var place, out var failure))
        {
            return failure;
        }

        if (!File.Exists(place.BackupPath))
        {
            return NoteImageBackupResult.Failed(place.RelativePath, "没有原图备份，不能恢复。");
        }

        var swap = new Swap(place, onPhase);
        return swap.Restore();
    }

    /// <summary>
    /// 统计一篇笔记的 .originals 里有多少个文件、一共多少字节。没有备份时数量和字节数都是 0。
    /// </summary>
    public static NoteImageBackupMeasure Measure(string noteFolder)
    {
        var noteRoot = RequireNoteFolder(noteFolder);
        var originals = OriginalsRoot(noteRoot);
        if (!Directory.Exists(originals))
        {
            return new NoteImageBackupMeasure(0, 0);
        }

        var count = 0;
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(originals, "*", SearchOption.AllDirectories))
        {
            count++;
            total += new FileInfo(file).Length;
        }

        return new NoteImageBackupMeasure(count, total);
    }

    /// <summary>
    /// 把一篇笔记 .originals 里的备份送进回收站。被占用的文件跳过，其余文件继续。
    /// 不传回收动作时使用 <see cref="RecycleService.SendToRecycleBin"/>。
    /// 全部送走后，空的 .originals 目录会删掉。
    /// </summary>
    public static NoteImagePurgeResult Purge(string noteFolder, Action<string>? sendToRecycle = null)
    {
        var noteRoot = RequireNoteFolder(noteFolder);
        var originals = OriginalsRoot(noteRoot);
        if (!Directory.Exists(originals))
        {
            return new NoteImagePurgeResult();
        }

        var recycle = sendToRecycle ?? RecycleService.SendToRecycleBin;
        var fileList = Directory.EnumerateFiles(originals, "*", SearchOption.AllDirectories).ToList();
        var resultList = new List<NoteImageBackupResult>();
        foreach (var file in fileList)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            var relative = JsonUtil.ToRel(Path.GetRelativePath(noteRoot, file));
            FileLockReport report;
            try
            {
                report = FileLockProbe.Probe(file);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }

            if (report.IsLocked)
            {
                resultList.Add(NoteImageBackupResult.Locked(relative, report.HolderNameList));
                continue;
            }

            try
            {
                recycle(file);
                resultList.Add(NoteImageBackupResult.Purged(relative));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException
                or FileNotFoundException)
            {
                resultList.Add(NoteImageBackupResult.Failed(relative, "没能把备份送进回收站：" + WithPeriod(exception.Message)));
            }
        }

        DeleteEmptyDirectories(originals);
        return new NoteImagePurgeResult { FileList = resultList };
    }

    /// <summary>
    /// 正式替换用的临时文件路径。例如 <c>图/a.png</c> 对应 <c>图/.a.png.compressing</c>。
    /// </summary>
    public static string CompressingFilePath(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("配图路径不能为空。", nameof(imagePath));
        }

        var full = Path.GetFullPath(imagePath);
        var directory = Path.GetDirectoryName(full) ?? "";
        return Path.Combine(directory, "." + Path.GetFileName(full) + CompressingSuffix);
    }

    /// <summary>
    /// 这张配图对应的原图备份路径。配图必须在笔记文件夹里面。
    /// </summary>
    public static string BackupFilePath(string noteFolder, string imagePath)
    {
        if (!TryLocate(noteFolder, imagePath, out var place, out var failure))
        {
            throw new ArgumentException(failure.Message, nameof(imagePath));
        }

        return place.BackupPath;
    }

    #region 定位

    /// <summary>
    /// 笔记文件夹必须存在。
    /// </summary>
    private static string RequireNoteFolder(string noteFolder)
    {
        if (string.IsNullOrWhiteSpace(noteFolder))
        {
            throw new ArgumentException("笔记文件夹路径不能为空。", nameof(noteFolder));
        }

        var full = Path.GetFullPath(noteFolder);
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException("找不到笔记文件夹：" + full);
        }

        return full;
    }

    /// <summary>
    /// .originals 目录的完整路径。目录可以还不存在。
    /// </summary>
    private static string OriginalsRoot(string noteRoot)
    {
        return Path.Combine(noteRoot, NoteRules.OriginalsDirName);
    }

    /// <summary>
    /// 把配图定位到笔记文件夹内。落在文件夹外、原图备份或网页成品里时返回失败，不改文件。
    /// </summary>
    private static bool TryLocate(
        string noteFolder,
        string imagePath,
        out LocatedImage located,
        out NoteImageBackupResult failure)
    {
        located = default;
        failure = NoteImageBackupResult.Failed("", "配图路径不能为空。");
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("配图路径不能为空。", nameof(imagePath));
        }

        var noteRoot = RequireNoteFolder(noteFolder);
        var imageFull = Path.IsPathRooted(imagePath)
            ? Path.GetFullPath(imagePath)
            : Path.GetFullPath(Path.Combine(noteRoot, imagePath));
        var prefix = noteRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!imageFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            failure = NoteImageBackupResult.Failed(Path.GetFileName(imageFull), "这张配图不在这篇笔记文件夹里。");
            return false;
        }

        var relative = JsonUtil.ToRel(Path.GetRelativePath(noteRoot, imageFull));
        if (relative.Length == 0 || relative.StartsWith("../", StringComparison.Ordinal))
        {
            failure = NoteImageBackupResult.Failed(Path.GetFileName(imageFull), "这张配图不在这篇笔记文件夹里。");
            return false;
        }

        var top = relative.Split('/')[0];
        if (string.Equals(top, NoteRules.OriginalsDirName, StringComparison.OrdinalIgnoreCase))
        {
            failure = NoteImageBackupResult.Failed(relative, "不能替换原图备份文件夹里的文件。");
            return false;
        }

        if (string.Equals(top, NoteRules.ReadyDirName, StringComparison.OrdinalIgnoreCase))
        {
            failure = NoteImageBackupResult.Failed(relative, "不能替换网页成品文件夹里的文件。");
            return false;
        }

        located = new LocatedImage(
            noteRoot,
            imageFull,
            relative,
            Path.Combine(OriginalsRoot(noteRoot), relative.Replace('/', Path.DirectorySeparatorChar)),
            CompressingFilePath(imageFull));
        failure = NoteImageBackupResult.Failed(relative, "");
        return true;
    }

    private readonly record struct LocatedImage(
        string NoteFolder,
        string ImagePath,
        string RelativePath,
        string BackupPath,
        string TempPath);

    #endregion

    #region 替换与恢复

    /// <summary>
    /// 一次替换或恢复使用的文件移动。失败时按已经完成的步骤倒回去。
    /// </summary>
    private sealed class Swap
    {
        private readonly LocatedImage mPlace;
        private readonly Action<NoteImageBackupPhase>? mOnPhase;
        private readonly List<string> mCreatedDirList = new();
        private string? mHoldPath;
        private bool mCreatedBackup;
        private bool mMovedCurrentAside;
        private bool mRenamedTemp;
        private bool mBackupDeleted;
        private bool mImageExistedAtStart;

        public Swap(LocatedImage place, Action<NoteImageBackupPhase>? onPhase)
        {
            mPlace = place;
            mOnPhase = onPhase;
        }

        /// <summary>
        /// 执行替换。文件被占用时直接返回，不写临时文件。
        /// </summary>
        public NoteImageBackupResult Replace(byte[] newContent)
        {
            var locked = LockIfBusy(mPlace.ImagePath);
            if (locked != null)
            {
                return locked;
            }

            mImageExistedAtStart = true;
            var creationUtc = File.GetCreationTimeUtc(mPlace.ImagePath);
            try
            {
                File.WriteAllBytes(mPlace.TempPath, newContent);
                Notify(NoteImageBackupPhase.TempWritten);
                MoveCurrentAsideForReplace();
                Notify(NoteImageBackupPhase.OriginalMoved);
                RenameTemp();
                Notify(NoteImageBackupPhase.TempRenamed);
                ApplyTimestamps(mPlace.ImagePath, creationUtc);
                Notify(NoteImageBackupPhase.TimestampsApplied);
                DeleteHold();
                return NoteImageBackupResult.Replaced(mPlace.RelativePath, mCreatedBackup);
            }
            catch (Exception exception) when (IsRollbackException(exception))
            {
                return Fail(exception);
            }
        }

        /// <summary>
        /// 执行恢复。正式文件和备份只要有一个被占用，就都不改。
        /// </summary>
        public NoteImageBackupResult Restore()
        {
            mImageExistedAtStart = File.Exists(mPlace.ImagePath);
            if (mImageExistedAtStart)
            {
                var lockedImage = LockIfBusy(mPlace.ImagePath);
                if (lockedImage != null)
                {
                    return lockedImage;
                }
            }

            var lockedBackup = LockIfBusy(mPlace.BackupPath);
            if (lockedBackup != null)
            {
                return lockedBackup;
            }

            var creationUtc = File.GetCreationTimeUtc(mPlace.BackupPath);
            try
            {
                EnsureParent(mPlace.TempPath);
                File.Copy(mPlace.BackupPath, mPlace.TempPath, overwrite: true);
                Notify(NoteImageBackupPhase.TempWritten);
                if (mImageExistedAtStart)
                {
                    ParkCurrent();
                }

                Notify(NoteImageBackupPhase.OriginalMoved);
                EnsureParent(mPlace.ImagePath);
                RenameTemp();
                Notify(NoteImageBackupPhase.TempRenamed);
                ApplyTimestamps(mPlace.ImagePath, creationUtc);
                Notify(NoteImageBackupPhase.TimestampsApplied);
                File.Delete(mPlace.BackupPath);
                mBackupDeleted = true;
                TryDeleteHold();
                RemoveCreatedDirectories();
                return NoteImageBackupResult.Restored(mPlace.RelativePath);
            }
            catch (Exception exception) when (IsRollbackException(exception))
            {
                if (mBackupDeleted)
                {
                    TryDeleteHold();
                    return NoteImageBackupResult.Restored(mPlace.RelativePath);
                }

                return Fail(exception);
            }
        }

        /// <summary>
        /// 文件被占用时返回结果，否则返回空。
        /// </summary>
        private NoteImageBackupResult? LockIfBusy(string path)
        {
            var report = FileLockProbe.Probe(path);
            if (!report.IsLocked)
            {
                return null;
            }

            return NoteImageBackupResult.Locked(mPlace.RelativePath, report.HolderNameList);
        }

        /// <summary>
        /// 第一次替换把原图移进备份；已经有备份时把当前文件挪到旁边，不覆盖那份原图。
        /// </summary>
        private void MoveCurrentAsideForReplace()
        {
            if (!File.Exists(mPlace.BackupPath))
            {
                EnsureParent(mPlace.BackupPath);
                File.Move(mPlace.ImagePath, mPlace.BackupPath);
                mCreatedBackup = true;
                return;
            }

            ParkCurrent();
        }

        /// <summary>
        /// 把正式位置上的文件挪到旁边，腾出文件名。旁边的文件不是原图备份。
        /// </summary>
        private void ParkCurrent()
        {
            var directory = Path.GetDirectoryName(mPlace.ImagePath) ?? "";
            mHoldPath = Path.Combine(
                directory,
                "." + Path.GetFileName(mPlace.ImagePath) + ".replacing-" + Guid.NewGuid().ToString("N"));
            File.Move(mPlace.ImagePath, mHoldPath);
            mMovedCurrentAside = true;
        }

        /// <summary>
        /// 把临时文件改成正式文件名。
        /// </summary>
        private void RenameTemp()
        {
            if (File.Exists(mPlace.ImagePath))
            {
                throw new IOException("正式文件名仍被占用，不能改名。");
            }

            File.Move(mPlace.TempPath, mPlace.ImagePath);
            mRenamedTemp = true;
        }

        /// <summary>
        /// 删掉旁边那份已经被换掉的文件。删不掉就抛出，让替换回退。
        /// </summary>
        private void DeleteHold()
        {
            if (!mMovedCurrentAside || mHoldPath == null || !File.Exists(mHoldPath))
            {
                mMovedCurrentAside = false;
                return;
            }

            File.Delete(mHoldPath);
            mMovedCurrentAside = false;
        }

        /// <summary>
        /// 尽量删掉旁边的文件。恢复已经删掉唯一的原图备份之后，不能再因为这一步回退。
        /// </summary>
        private void TryDeleteHold()
        {
            try
            {
                DeleteHold();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// 通知测试当前步骤已经完成。
        /// </summary>
        private void Notify(NoteImageBackupPhase phase)
        {
            mOnPhase?.Invoke(phase);
        }

        /// <summary>
        /// 回退并返回失败。回退成功时说明原图仍在原来的位置。
        /// </summary>
        private NoteImageBackupResult Fail(Exception exception)
        {
            var restored = TryRollback();
            return NoteImageBackupResult.Failed(mPlace.RelativePath, DescribeFailure(exception, restored));
        }

        /// <summary>
        /// 按相反的顺序把正式位置恢复成调用前的文件，并删掉临时文件。
        /// </summary>
        private bool TryRollback()
        {
            try
            {
                if (mRenamedTemp && File.Exists(mPlace.ImagePath))
                {
                    File.Delete(mPlace.ImagePath);
                    mRenamedTemp = false;
                }

                if (mCreatedBackup && File.Exists(mPlace.BackupPath) && !File.Exists(mPlace.ImagePath))
                {
                    File.Move(mPlace.BackupPath, mPlace.ImagePath);
                    mCreatedBackup = false;
                }

                if (mMovedCurrentAside && mHoldPath != null && File.Exists(mHoldPath))
                {
                    if (File.Exists(mPlace.ImagePath))
                    {
                        File.Delete(mPlace.ImagePath);
                    }

                    File.Move(mHoldPath, mPlace.ImagePath);
                    mMovedCurrentAside = false;
                }

                if (File.Exists(mPlace.TempPath))
                {
                    File.Delete(mPlace.TempPath);
                }

                RemoveCreatedDirectories();
                return mImageExistedAtStart ? File.Exists(mPlace.ImagePath) : !File.Exists(mPlace.ImagePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// 失败说明。回退成功和没能回退要说清楚，避免用户以为原图已经丢了。
        /// </summary>
        private string DescribeFailure(Exception exception, bool restored)
        {
            var detail = WithPeriod(exception.Message);
            if (restored)
            {
                return "操作失败：" + detail + "原图仍在原来的位置。";
            }

            if (File.Exists(mPlace.BackupPath))
            {
                return "操作失败：" + detail + "原图没能放回原处，请到「" + mPlace.BackupPath + "」查看。";
            }

            return "操作失败：" + detail + "原图没能放回原处。";
        }

        /// <summary>
        /// 建立文件所在的目录。记下这次新建的目录，回退时如果是空的就删掉。
        /// </summary>
        private void EnsureParent(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                EnsureDirectory(directory);
            }
        }

        /// <summary>
        /// 从缺失的那一层开始创建目录，不创建笔记文件夹本身。
        /// </summary>
        private void EnsureDirectory(string directory)
        {
            var noteRoot = Path.GetFullPath(mPlace.NoteFolder);
            var current = Path.GetFullPath(directory);
            var pending = new List<string>();
            while (!Directory.Exists(current))
            {
                if (string.Equals(current, noteRoot, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                pending.Add(current);
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }

            for (var index = pending.Count - 1; index >= 0; index--)
            {
                Directory.CreateDirectory(pending[index]);
                mCreatedDirList.Add(pending[index]);
            }
        }

        /// <summary>
        /// 删掉这次新建且已经空了的目录。里面还有别的文件时保留。
        /// </summary>
        private void RemoveCreatedDirectories()
        {
            for (var index = mCreatedDirList.Count - 1; index >= 0; index--)
            {
                var directory = mCreatedDirList[index];
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
        }
    }

    /// <summary>
    /// 先写回创建时间，再把修改时间设为现在。
    /// 没有拍摄时间的配图靠创建时间排序，所以创建时间必须留住。
    /// 缩略图缓存按修改时间作键，修改时间必须变新，缓存才会刷新。
    /// </summary>
    private static void ApplyTimestamps(string path, DateTime creationUtc)
    {
        var creation = creationUtc.Kind == DateTimeKind.Utc
            ? creationUtc
            : DateTime.SpecifyKind(creationUtc.ToUniversalTime(), DateTimeKind.Utc);
        File.SetCreationTimeUtc(path, creation);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
    }

    /// <summary>
    /// 磁盘和权限上的失败才回退。测试注入的失败也使用 <see cref="IOException"/>。
    /// </summary>
    private static bool IsRollbackException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    #endregion

    #region 清理

    /// <summary>
    /// 从最深的空目录开始删。有文件留下时，它所在的目录保留。
    /// </summary>
    private static void DeleteEmptyDirectories(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            DeleteEmptyDirectories(child);
        }

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    /// <summary>
    /// 给说明补上句号，避免两句粘在一起。
    /// </summary>
    private static string WithPeriod(string? text)
    {
        var detail = string.IsNullOrWhiteSpace(text) ? "没有说明原因" : text.Trim();
        if (detail.EndsWith('。') || detail.EndsWith('.'))
        {
            return detail.EndsWith('.') ? detail[..^1] + "。" : detail;
        }

        return detail + "。";
    }

    #endregion
}
