using System.Text.Json;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 从意图文件预演或执行，窗口与 CLI 共用。
/// </summary>
public static class IntentFileRunner
{
    /// <summary>
    /// 预演通过。
    /// </summary>
    public const int ExitOk = 0;

    /// <summary>
    /// 参数或文件错误。
    /// </summary>
    public const int ExitUsage = 1;

    /// <summary>
    /// 闸门拒绝或执行中途失败。
    /// </summary>
    public const int ExitRejected = 2;

    /// <summary>
    /// 加载工作区并过闸门，不写盘。
    /// </summary>
    public static IntentFileRunResult Preview(string profilePath, string intentPath)
    {
        try
        {
            var loaded = Load(profilePath, intentPath);
            var text = PromptRenderer.RenderPreview(loaded.Preview);
            if (loaded.Preview.HasHardError)
            {
                return Fail(ExitRejected, text, loaded.Preview, null);
            }

            return new IntentFileRunResult
            {
                Ok = true,
                ExitCode = ExitOk,
                Text = text,
                Preview = loaded.Preview
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or JsonException or InvalidOperationException or ArgumentException)
        {
            return Fail(ExitUsage, ex.Message, null, null);
        }
    }

    /// <summary>
    /// 预演通过后调用同一 <see cref="BatchExecutor"/>，并合并待发布队列。
    /// </summary>
    public static IntentFileRunResult Apply(string profilePath, string intentPath, string? pendingPath = null)
    {
        try
        {
            var loaded = Load(profilePath, intentPath);
            var previewText = PromptRenderer.RenderPreview(loaded.Preview);
            if (loaded.Preview.HasHardError)
            {
                return Fail(ExitRejected, previewText, loaded.Preview, null);
            }

            File.WriteAllText(intentPath, IntentDocumentBuilder.ToJson(loaded.Document), JsonUtil.Utf8NoBom);
            var batch = BatchExecutor.Run(loaded.Session, loaded.Document, intentPath);
            if (!batch.Ok)
            {
                var failLines = new List<string>
                {
                    previewText,
                    batch.CompletedStepList.Count == 0
                        ? "尚未完成任何步。"
                        : "已完成：" + string.Join("；", batch.CompletedStepList),
                    "失败步骤：" + batch.FailedStep,
                    batch.FailureText
                };
                CommandStepTiming.AppendBlock(failLines, batch.StepTimingList, batch.Elapsed);
                CommandResourceResult.AppendBlock(failLines, batch.ResourceList);
                return Fail(
                    ExitRejected,
                    string.Join(Environment.NewLine, failLines),
                    loaded.Preview,
                    batch);
            }

            if (batch.Pending != null)
            {
                PendingPublishStore.MergeSave(batch.Pending, pendingPath);
            }

            var completed = batch.CompletedStepList.Count == 0
                ? "没有需要写盘的步骤。"
                : "已完成：" + string.Join("；", batch.CompletedStepList);
            var lineList = new List<string> { previewText, completed };
            CommandStepTiming.AppendBlock(lineList, batch.StepTimingList, batch.Elapsed);
            CommandResourceResult.AppendBlock(lineList, batch.ResourceList);
            return new IntentFileRunResult
            {
                Ok = true,
                ExitCode = ExitOk,
                Text = string.Join(Environment.NewLine, lineList),
                Preview = loaded.Preview,
                Batch = batch
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or JsonException or InvalidOperationException or ArgumentException)
        {
            return Fail(ExitUsage, ex.Message, null, null);
        }
    }

    /// <summary>
    /// 读取配置与意图并生成预演。
    /// </summary>
    private static LoadedIntent Load(string profilePath, string intentPath)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            throw new FileNotFoundException("找不到工作区配置。", profilePath);
        }

        var session = WorkspaceSession.Load(profilePath);
        var document = IntentDocumentBuilder.FromFile(intentPath);
        IntentCatalogBinder.PrepareForCli(document, session);
        var preview = PreviewReporter.BuildFromDocument(session, document);
        return new LoadedIntent(session, document, preview);
    }

    /// <summary>
    /// 组装失败结果。
    /// </summary>
    private static IntentFileRunResult Fail(
        int exitCode,
        string text,
        PreviewReport? preview,
        BatchRunResult? batch)
    {
        return new IntentFileRunResult
        {
            Ok = false,
            ExitCode = exitCode,
            Text = text,
            Preview = preview,
            Batch = batch
        };
    }

    private sealed record LoadedIntent(
        WorkspaceSession Session,
        IntentDocument Document,
        PreviewReport Preview);
}

/// <summary>
/// 意图文件预演或执行的汇总。
/// </summary>
public sealed class IntentFileRunResult
{
    public bool Ok { get; init; }
    public int ExitCode { get; init; }
    public string Text { get; init; } = "";
    public PreviewReport? Preview { get; init; }
    public BatchRunResult? Batch { get; init; }
}
