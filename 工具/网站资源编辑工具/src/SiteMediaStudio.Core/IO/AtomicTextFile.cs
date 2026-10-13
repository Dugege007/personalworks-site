namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把文本先写到同目录的临时文件，再替换目标文件。
/// 写到一半中断时，原来的文件保持不变。
/// </summary>
public static class AtomicTextFile
{
    /// <summary>
    /// 用完整内容替换目标文件。临时文件与目标文件在同一目录。
    /// </summary>
    public static void Write(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(tempPath, content, JsonUtil.Utf8NoBom);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // 临时文件清不掉时，仍抛出原来的写入异常。
                }
                catch (UnauthorizedAccessException)
                {
                    // 临时文件清不掉时，仍抛出原来的写入异常。
                }
            }

            throw;
        }
    }
}
