using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 读取对象台账。
/// </summary>
public static class LedgerReader
{
    /// <summary>
    /// 按对象键建立台账字典；文件缺失时返回空表。
    /// </summary>
    public static Dictionary<string, LedgerRecord> LoadDict(WorkspaceProfile profile)
    {
        var resultDict = new Dictionary<string, LedgerRecord>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(profile.LedgerPath))
        {
            return resultDict;
        }

        var fullPath = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.LedgerPath);
        if (!File.Exists(fullPath))
        {
            return resultDict;
        }

        var json = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            return resultDict;
        }

        foreach (var item in records.EnumerateArray())
        {
            var objectKey = item.TryGetProperty("object", out var objectEl) ? objectEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(objectKey))
            {
                continue;
            }

            var stageRel = item.TryGetProperty("stageRel", out var stageEl) ? stageEl.GetString() : null;
            var sourceStageRel = item.TryGetProperty("sourceStageRel", out var sourceEl) ? sourceEl.GetString() : null;
            var status = item.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : "";
            resultDict[objectKey] = new LedgerRecord
            {
                Object = objectKey,
                StageRel = string.IsNullOrWhiteSpace(stageRel) ? null : JsonUtil.ToRel(stageRel),
                SourceStageRel = string.IsNullOrWhiteSpace(sourceStageRel) ? null : JsonUtil.ToRel(sourceStageRel),
                Status = status ?? ""
            };
        }

        return resultDict;
    }
}
