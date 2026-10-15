using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

/// <summary>
/// 配图的安全替换、原图备份、恢复和清理。图片都在测试里生成，不提交成品大图。
/// 创建时间能否和修改时间分开保存，取决于文件系统；分得开时才断言创建时间不变。
/// </summary>
public sealed class NoteImageBackupTests
{
    private static bool? mCreationTimeStaysDistinct;

    [Fact]
    public void Replace_MovesOriginalToBackup_AndKeepsRelativeSubpath()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, Path.Combine("图", "a.png"), MinimalPng);
        var updated = DifferentPng();
        string? tempSeen = null;

        var result = NoteImageBackup.Replace(note, image, updated, phase =>
        {
            if (phase != NoteImageBackupPhase.TempWritten)
            {
                return;
            }

            tempSeen = NoteImageBackup.CompressingFilePath(image);
            Assert.True(File.Exists(tempSeen));
            Assert.Equal(".a.png.compressing", Path.GetFileName(tempSeen));
            Assert.False(MediaPathRules.IsCatalogFile(tempSeen));
        });

        var backup = NoteImageBackup.BackupFilePath(note, image);
        Assert.Equal(NoteImageBackupKind.Replaced, result.Kind);
        Assert.True(result.CreatedBackup);
        Assert.Equal("图/a.png", result.RelativePath);
        Assert.NotNull(tempSeen);
        Assert.False(File.Exists(tempSeen));
        Assert.Equal(updated, File.ReadAllBytes(image));
        Assert.Equal(MinimalPng, File.ReadAllBytes(backup));
        Assert.True(NoteImagePrompt.HasOriginalBackup(note, "图/a.png"));
        Assert.False(HasLeftover(note));
    }

    [Fact]
    public void Replace_WhenBackupAlreadyExists_DoesNotOverwriteTheFirstOriginal()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);
        var second = DifferentPng();
        var third = DifferentPng();
        third[0] = 0x42;

        var first = NoteImageBackup.Replace(note, image, second);
        var backup = NoteImageBackup.BackupFilePath(note, image);
        Assert.True(first.CreatedBackup);
        Assert.Equal(MinimalPng, File.ReadAllBytes(backup));

        var again = NoteImageBackup.Replace(note, image, third);

        Assert.Equal(NoteImageBackupKind.Replaced, again.Kind);
        Assert.False(again.CreatedBackup);
        Assert.Contains("未覆盖", again.Message, StringComparison.Ordinal);
        Assert.Equal(MinimalPng, File.ReadAllBytes(backup));
        Assert.Equal(third, File.ReadAllBytes(image));
        Assert.False(HasLeftover(note));
    }

    [Theory]
    [InlineData(NoteImageBackupPhase.TempWritten)]
    [InlineData(NoteImageBackupPhase.OriginalMoved)]
    [InlineData(NoteImageBackupPhase.TempRenamed)]
    [InlineData(NoteImageBackupPhase.TimestampsApplied)]
    public void Replace_WhenAStepFails_LeavesTheOriginalInPlace(NoteImageBackupPhase phase)
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);

        var result = NoteImageBackup.Replace(note, image, DifferentPng(), current =>
        {
            if (current == phase)
            {
                throw new IOException("模拟写入失败");
            }
        });

        Assert.Equal(NoteImageBackupKind.Failed, result.Kind);
        Assert.Contains("模拟写入失败", result.Message, StringComparison.Ordinal);
        Assert.Contains("原图仍在原来的位置", result.Message, StringComparison.Ordinal);
        Assert.Equal(MinimalPng, File.ReadAllBytes(image));
        Assert.False(Directory.Exists(Path.Combine(note, NoteRules.OriginalsDirName)));
        Assert.False(HasLeftover(note));
    }

    [Fact]
    public void Replace_WhenFileIsLocked_SkipsWithoutCreatingBackup()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);
        NoteImageBackupResult result;
        using (var stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            _ = stream;
            result = NoteImageBackup.Replace(note, image, DifferentPng());
            Assert.False(Directory.Exists(Path.Combine(note, NoteRules.OriginalsDirName)));
        }

        Assert.Equal(NoteImageBackupKind.Locked, result.Kind);
        Assert.Contains("a.png", result.Message, StringComparison.Ordinal);
        Assert.Contains("占用", result.Message, StringComparison.Ordinal);
        Assert.Equal(MinimalPng, File.ReadAllBytes(image));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Empty(result.HolderNameList);
        }
    }

    [Fact]
    public void Replace_WithoutCaptureTime_RefreshesWriteTimeAndKeepsCreationTimeWhenSupported()
    {
        var updated = DifferentPng();
        Assert.DoesNotContain("Exif", System.Text.Encoding.ASCII.GetString(MinimalPng), StringComparison.Ordinal);
        Assert.DoesNotContain("Exif", System.Text.Encoding.ASCII.GetString(updated), StringComparison.Ordinal);
        AssertTimestamps(MinimalPng, updated, "plain.png");
    }

    [Fact]
    public void Replace_WithCaptureTime_KeepsThatTextAndCreationTimeWhenSupported()
    {
        var original = JpegWithCaptureTime("2019:05:06 07:08:09");
        var updated = JpegWithCaptureTime("2024:08:09 10:11:12");
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "shot.jpg", original);
        StampOldTimes(image);

        var result = NoteImageBackup.Replace(note, image, updated);

        Assert.Equal(NoteImageBackupKind.Replaced, result.Kind);
        var current = File.ReadAllBytes(image);
        var backup = File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image));
        Assert.Contains("2024:08:09 10:11:12", System.Text.Encoding.ASCII.GetString(current), StringComparison.Ordinal);
        Assert.Contains("2019:05:06 07:08:09", System.Text.Encoding.ASCII.GetString(backup), StringComparison.Ordinal);
        Assert.DoesNotContain("2024:08:09 10:11:12", System.Text.Encoding.ASCII.GetString(backup), StringComparison.Ordinal);
        AssertWriteTimeIsNew(image);
        AssertCreationTimeWhenSupported(image);
    }

    [Fact]
    public void Restore_PutsTheBackupBack_AndDeletesTheBackup()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, Path.Combine("图", "a.png"), MinimalPng);
        NoteImageBackup.Replace(note, image, DifferentPng());
        var other = tree.Write(note, Path.Combine(NoteRules.OriginalsDirName, "other.png"), MinimalPng);

        var result = NoteImageBackup.Restore(note, image);

        Assert.Equal(NoteImageBackupKind.Restored, result.Kind);
        Assert.Equal(MinimalPng, File.ReadAllBytes(image));
        Assert.False(File.Exists(NoteImageBackup.BackupFilePath(note, image)));
        Assert.True(File.Exists(other));
        Assert.False(NoteImagePrompt.HasOriginalBackup(note, "图/a.png"));
    }

    [Fact]
    public void Restore_WhenCurrentFileIsMissing_RecreatesItFromTheBackup()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);
        NoteImageBackup.Replace(note, image, DifferentPng());
        File.Delete(image);

        var result = NoteImageBackup.Restore(note, image);

        Assert.Equal(NoteImageBackupKind.Restored, result.Kind);
        Assert.Equal(MinimalPng, File.ReadAllBytes(image));
        Assert.False(File.Exists(NoteImageBackup.BackupFilePath(note, image)));
    }

    [Fact]
    public void Restore_WhenBackupIsMissing_DoesNotChangeTheCurrentFile()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);

        var result = NoteImageBackup.Restore(note, image);

        Assert.Equal(NoteImageBackupKind.Failed, result.Kind);
        Assert.Contains("没有原图备份", result.Message, StringComparison.Ordinal);
        Assert.Equal(MinimalPng, File.ReadAllBytes(image));
    }

    [Theory]
    [InlineData(NoteImageBackupPhase.TempWritten)]
    [InlineData(NoteImageBackupPhase.OriginalMoved)]
    [InlineData(NoteImageBackupPhase.TempRenamed)]
    [InlineData(NoteImageBackupPhase.TimestampsApplied)]
    public void Restore_WhenAStepFails_KeepsBothTheCurrentFileAndTheBackup(NoteImageBackupPhase phase)
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);
        var updated = DifferentPng();
        NoteImageBackup.Replace(note, image, updated);

        var result = NoteImageBackup.Restore(note, image, current =>
        {
            if (current == phase)
            {
                throw new IOException("模拟恢复失败");
            }
        });

        Assert.Equal(NoteImageBackupKind.Failed, result.Kind);
        Assert.Contains("原图仍在原来的位置", result.Message, StringComparison.Ordinal);
        Assert.Equal(updated, File.ReadAllBytes(image));
        Assert.Equal(MinimalPng, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
        Assert.False(HasLeftover(note));
    }

    [Fact]
    public void Restore_WhenCurrentFileIsLocked_LeavesTheBackup()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);
        var updated = DifferentPng();
        NoteImageBackup.Replace(note, image, updated);
        var backup = NoteImageBackup.BackupFilePath(note, image);
        NoteImageBackupResult result;
        using (var stream = new FileStream(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            _ = stream;
            result = NoteImageBackup.Restore(note, image);
            Assert.Equal(MinimalPng, File.ReadAllBytes(backup));
        }

        Assert.Equal(NoteImageBackupKind.Locked, result.Kind);
        Assert.Equal(updated, File.ReadAllBytes(image));
    }

    [Fact]
    public void Purge_SendsBackupsToTheInjectedRecycleAction_AndRemovesEmptyOriginals()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, Path.Combine("图", "a.png"), MinimalPng);
        var updated = DifferentPng();
        NoteImageBackup.Replace(note, image, updated);
        var backup = NoteImageBackup.BackupFilePath(note, image);
        var measure = NoteImageBackup.Measure(note);
        Assert.Equal(1, measure.FileCount);
        Assert.Equal(MinimalPng.Length, measure.TotalBytes);
        var recycled = new List<string>();

        var result = NoteImageBackup.Purge(note, path =>
        {
            recycled.Add(path);
            File.Delete(path);
        });

        Assert.Equal(1, result.RecycledCount);
        Assert.Equal(0, result.LockedCount);
        Assert.Equal(backup, recycled[0]);
        Assert.False(File.Exists(backup));
        Assert.False(Directory.Exists(Path.Combine(note, NoteRules.OriginalsDirName)));
        Assert.Equal(updated, File.ReadAllBytes(image));
        Assert.Equal(0, NoteImageBackup.Measure(note).FileCount);
    }

    [Fact]
    public void Purge_WhenABackupIsLocked_SkipsThatFileAndRecyclesTheRest()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var first = tree.Write(note, "a.png", MinimalPng);
        var second = tree.Write(note, "b.png", DifferentPng());
        NoteImageBackup.Replace(note, first, DifferentPng());
        NoteImageBackup.Replace(note, second, MinimalPng);
        var lockedBackup = NoteImageBackup.BackupFilePath(note, first);
        var freeBackup = NoteImageBackup.BackupFilePath(note, second);
        using var stream = new FileStream(lockedBackup, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var recycled = new List<string>();

        var result = NoteImageBackup.Purge(note, path =>
        {
            recycled.Add(path);
            File.Delete(path);
        });

        Assert.Equal(1, result.RecycledCount);
        Assert.Equal(1, result.LockedCount);
        Assert.Equal(new[] { freeBackup }, recycled);
        Assert.True(File.Exists(lockedBackup));
        Assert.False(File.Exists(freeBackup));
        Assert.Contains(result.FileList, item => item.Kind == NoteImageBackupKind.Locked && item.Message.Contains("a.png", StringComparison.Ordinal));
    }

    [Fact]
    public void Purge_WithoutAnInjectedAction_UsesRecycleService()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, "a.png", MinimalPng);
        NoteImageBackup.Replace(note, image, DifferentPng());
        var backup = NoteImageBackup.BackupFilePath(note, image);

        var result = NoteImageBackup.Purge(note);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(1, result.RecycledCount);
            Assert.False(File.Exists(backup));
            return;
        }

        Assert.Equal(1, result.FailedCount);
        Assert.True(File.Exists(backup));
        Assert.Contains("回收站", result.FileList[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Replace_WhenImageIsOutsideTheNoteOrInsideOriginals_DoesNotWrite()
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var outside = tree.Write(tree.MakeDirectory("other"), "a.png", MinimalPng);
        var backupImage = tree.Write(note, Path.Combine(NoteRules.OriginalsDirName, "a.png"), MinimalPng);

        var outsideResult = NoteImageBackup.Replace(note, outside, DifferentPng());
        var backupResult = NoteImageBackup.Replace(note, backupImage, DifferentPng());

        Assert.Equal(NoteImageBackupKind.Failed, outsideResult.Kind);
        Assert.Contains("不在这篇笔记文件夹里", outsideResult.Message, StringComparison.Ordinal);
        Assert.Equal(MinimalPng, File.ReadAllBytes(outside));
        Assert.Equal(NoteImageBackupKind.Failed, backupResult.Kind);
        Assert.Contains("原图备份", backupResult.Message, StringComparison.Ordinal);
        Assert.Equal(MinimalPng, File.ReadAllBytes(backupImage));
    }

    /// <summary>
    /// 没有拍摄时间的图片和带拍摄时间的图片共用这一套时间断言。
    /// </summary>
    private static void AssertTimestamps(byte[] original, byte[] updated, string fileName)
    {
        using var tree = new TempTree();
        var note = tree.MakeDirectory("note");
        var image = tree.Write(note, fileName, original);
        StampOldTimes(image);

        var result = NoteImageBackup.Replace(note, image, updated);

        Assert.Equal(NoteImageBackupKind.Replaced, result.Kind);
        Assert.Equal(updated, File.ReadAllBytes(image));
        Assert.Equal(original, File.ReadAllBytes(NoteImageBackup.BackupFilePath(note, image)));
        AssertWriteTimeIsNew(image);
        AssertCreationTimeWhenSupported(image);
    }

    /// <summary>
    /// 把创建时间和修改时间都拨到一个足够旧的时刻，替换后才能看出修改时间变新。
    /// </summary>
    private static void StampOldTimes(string path)
    {
        var created = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetCreationTimeUtc(path, created);
        File.SetLastWriteTimeUtc(path, created);
    }

    /// <summary>
    /// 修改时间必须比替换前新，缩略图缓存才会换键。
    /// </summary>
    private static void AssertWriteTimeIsNew(string path)
    {
        var old = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        Assert.True(File.GetLastWriteTimeUtc(path) > old.AddMinutes(1));
    }

    /// <summary>
    /// 文件系统能把创建时间和修改时间分开保存时，创建时间必须仍是替换前的值。
    /// </summary>
    private static void AssertCreationTimeWhenSupported(string path)
    {
        if (!CreationTimeStaysDistinct())
        {
            return;
        }

        var expected = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var actual = File.GetCreationTimeUtc(path);
        Assert.True(Math.Abs((actual - expected).TotalSeconds) < 2, "创建时间变成了 " + actual.ToString("o"));
    }

    /// <summary>
    /// 先写创建时间再写修改时间。若读回来的创建时间被改掉，说明当前系统分不开这两个时间。
    /// </summary>
    private static bool CreationTimeStaysDistinct()
    {
        if (mCreationTimeStaysDistinct.HasValue)
        {
            return mCreationTimeStaysDistinct.Value;
        }

        var path = Path.Combine(Path.GetTempPath(), "sms-ctime-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, new byte[] { 1 });
            var created = new DateTime(2018, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            File.SetCreationTimeUtc(path, created);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            mCreationTimeStaysDistinct = Math.Abs((File.GetCreationTimeUtc(path) - created).TotalSeconds) < 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            mCreationTimeStaysDistinct = false;
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return mCreationTimeStaysDistinct.Value;
    }

    /// <summary>
    /// 目录里不应留下替换用的临时文件。
    /// </summary>
    private static bool HasLeftover(string note)
    {
        return Directory.EnumerateFiles(note, "*", SearchOption.AllDirectories).Any(path =>
        {
            var name = Path.GetFileName(path);
            return name.Contains(".replacing-", StringComparison.Ordinal)
                || name.EndsWith(NoteImageBackup.CompressingSuffix, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 另一张同样小的 PNG。只改一个字节，用来和原图区分。
    /// </summary>
    private static byte[] DifferentPng()
    {
        var copy = (byte[])MinimalPng.Clone();
        copy[^1] ^= 0x01;
        return copy;
    }

    /// <summary>
    /// 最小的 JPEG 容器，里面放上拍摄时间原文。不保证能被图片库解码。
    /// </summary>
    private static byte[] JpegWithCaptureTime(string captureTime)
    {
        var payload = System.Text.Encoding.ASCII.GetBytes("Exif\0\0" + captureTime);
        var length = payload.Length + 2;
        var bytes = new byte[4 + 2 + payload.Length + 2];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE1;
        bytes[4] = (byte)(length >> 8);
        bytes[5] = (byte)(length & 0xFF);
        payload.CopyTo(bytes, 6);
        bytes[^2] = 0xFF;
        bytes[^1] = 0xD9;
        return bytes;
    }

    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
        0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0xD4, 0xEF, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };

    /// <summary>
    /// 临时笔记文件夹。测试结束时整棵删掉。
    /// </summary>
    private sealed class TempTree : IDisposable
    {
        private readonly string mRoot = Path.Combine(Path.GetTempPath(), "sms-note-backup-" + Guid.NewGuid().ToString("N"));

        public TempTree()
        {
            Directory.CreateDirectory(mRoot);
        }

        /// <summary>
        /// 建一个子目录。
        /// </summary>
        public string MakeDirectory(string name)
        {
            var path = Path.Combine(mRoot, name);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// 在笔记文件夹里写一张图。相对路径里的子目录会一并创建。
        /// </summary>
        public string Write(string directory, string relative, byte[] content)
        {
            var path = Path.Combine(directory, relative);
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(mRoot))
                {
                    Directory.Delete(mRoot, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
