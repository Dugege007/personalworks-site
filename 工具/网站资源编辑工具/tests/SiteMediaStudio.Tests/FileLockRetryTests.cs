using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class FileLockRetryTests
{
    [Fact]
    public void IsTransient_RecognizesWindowsSharingCodes()
    {
        Assert.True(FileLockRetry.IsTransient(WithCode(5)));
        Assert.True(FileLockRetry.IsTransient(WithCode(32)));
        Assert.True(FileLockRetry.IsTransient(WithCode(33)));
        Assert.True(FileLockRetry.IsTransient(WithCode(1175)));
        Assert.True(FileLockRetry.IsTransient(new UnauthorizedAccessException("占用")));
        Assert.False(FileLockRetry.IsTransient(new IOException("磁盘错误")));
        Assert.False(FileLockRetry.IsTransient(new InvalidOperationException("不是文件占用")));
    }

    [Fact]
    public void Invoke_RetriesTransientLockThenSucceeds()
    {
        var runs = 0;
        FileLockRetry.Invoke(() =>
        {
            runs++;
            if (runs < 3)
            {
                throw WithCode(32);
            }
        }, _ => TimeSpan.Zero);

        Assert.Equal(3, runs);
    }

    [Fact]
    public void Invoke_StopsAfterTheLastTransientFailure()
    {
        var runs = 0;
        var error = Assert.Throws<IOException>(() => FileLockRetry.Invoke(() =>
        {
            runs++;
            throw WithCode(32);
        }, _ => TimeSpan.Zero));

        Assert.Equal(32, error.HResult & 0xFFFF);
        Assert.Equal(FileLockRetry.MaxAttempts, runs);
    }

    [Fact]
    public void DefaultDelay_TotalsTwoSeconds()
    {
        var total = TimeSpan.Zero;
        for (var attempt = 1; attempt < FileLockRetry.MaxAttempts; attempt++)
        {
            total += FileLockRetry.DefaultDelay(attempt);
        }

        Assert.Equal(TimeSpan.FromSeconds(2), total);
        Assert.Equal(TimeSpan.FromMilliseconds(100), FileLockRetry.DefaultDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(200), FileLockRetry.DefaultDelay(2));
        Assert.Equal(TimeSpan.FromMilliseconds(400), FileLockRetry.DefaultDelay(3));
        Assert.Equal(TimeSpan.FromMilliseconds(800), FileLockRetry.DefaultDelay(4));
        Assert.Equal(TimeSpan.FromMilliseconds(500), FileLockRetry.DefaultDelay(5));
        Assert.Equal(TimeSpan.Zero, FileLockRetry.DefaultDelay(FileLockRetry.MaxAttempts));
    }

    private static IOException WithCode(int code)
    {
        var error = new IOException("文件被占用");
        error.HResult = unchecked((int)0x80070000) | code;
        return error;
    }
}
