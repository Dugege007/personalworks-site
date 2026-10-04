using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 只改台账 stageRel；失败还原文件，不删除意图文件。
/// </summary>
public static class LedgerWriter
{
    /// <summary>
    /// 按已允许的重挂条目写盘。
    /// </summary>
    public static LedgerWriteResult Apply(
        WorkspaceProfile profile,
        IReadOnlyList<IntentItem> itemList,
        bool failAfterWrite = false)
    {
        var ledgerPath = ResolveLedgerPath(profile);
        if (string.IsNullOrWhiteSpace(ledgerPath) || !File.Exists(ledgerPath))
        {
            return LedgerWriteResult.Fail("找不到对象台账。");
        }

        var original = File.ReadAllText(ledgerPath);
        LedgerFile file;
        try
        {
            file = JsonSerializer.Deserialize<LedgerFile>(original, JsonUtil.Options)
                ?? new LedgerFile();
        }
        catch (JsonException ex)
        {
            return LedgerWriteResult.Fail("台账无法解析：" + ex.Message);
        }

        var changeList = new List<string>();
        foreach (var item in itemList)
        {
            if (item.Intent != MediaIntentCodes.StageRelocate
                || string.IsNullOrWhiteSpace(item.Object)
                || string.IsNullOrWhiteSpace(item.StageRelAfter))
            {
                continue;
            }

            var record = file.Records.FirstOrDefault(candidate =>
                string.Equals(candidate.Object, item.Object, StringComparison.Ordinal));
            if (record == null)
            {
                return LedgerWriteResult.Fail("台账没有该对象：" + item.Object);
            }

            var before = record.StageRel ?? "";
            record.StageRel = JsonUtil.ToRel(item.StageRelAfter);
            changeList.Add($"{item.Object}：{before} → {record.StageRel}");
        }

        if (changeList.Count == 0)
        {
            return new LedgerWriteResult { Ok = true, ChangeList = Array.Empty<string>() };
        }

        var nextText = JsonSerializer.Serialize(file, JsonUtil.Options) + "\n";
        var transactionId = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
        var tempPath = ledgerPath + ".tmp-sitemedia-" + transactionId;
        var backupPath = ledgerPath + ".bak-sitemedia-" + transactionId;
        try
        {
            File.WriteAllText(tempPath, nextText, JsonUtil.Utf8NoBom);
            File.Copy(ledgerPath, backupPath, overwrite: true);
            File.Delete(ledgerPath);
            File.Move(tempPath, ledgerPath);
            if (failAfterWrite)
            {
                throw new InvalidOperationException("回归注入：台账写盘后失败。");
            }

            File.Delete(backupPath);
            return new LedgerWriteResult { Ok = true, ChangeList = changeList };
        }
        catch (Exception ex)
        {
            var restored = false;
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, ledgerPath, overwrite: true);
                restored = true;
                TryDelete(backupPath);
            }

            TryDelete(tempPath);
            return LedgerWriteResult.Fail(
                $"写盘失败，{(restored ? "已还原" : "未能完整还原")}台账：{ex.Message}",
                restored);
        }
    }

    /// <summary>
    /// 解析台账绝对路径。
    /// </summary>
    public static string? ResolveLedgerPath(WorkspaceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.LedgerPath))
        {
            return null;
        }

        return WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.LedgerPath);
    }

    /// <summary>
    /// 尽量删除临时文件。
    /// </summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class LedgerFile
    {
        public int Version { get; set; } = 1;
        public List<LedgerMutableRecord> Records { get; set; } = new();
    }

    private sealed class LedgerMutableRecord
    {
        public string Object { get; set; } = "";
        public string? StageRel { get; set; }
        public string? SourceStageRel { get; set; }
        public string Status { get; set; } = "";
    }
}

/// <summary>
/// 台账写盘结果。
/// </summary>
public sealed class LedgerWriteResult
{
    public bool Ok { get; init; }
    public string FailureText { get; init; } = "";
    public bool Restored { get; init; }
    public IReadOnlyList<string> ChangeList { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 组装失败结果。
    /// </summary>
    public static LedgerWriteResult Fail(string text, bool restored = false)
    {
        return new LedgerWriteResult
        {
            Ok = false,
            FailureText = text,
            Restored = restored
        };
    }
}
