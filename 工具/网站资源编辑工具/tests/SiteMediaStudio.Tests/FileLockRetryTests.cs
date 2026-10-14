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

    private static IOException WithCode(int code)
    {
        var error = new IOException("文件被占用");
        error.HResult = unchecked((int)0x80070000) | code;
        return error;
    }
}
