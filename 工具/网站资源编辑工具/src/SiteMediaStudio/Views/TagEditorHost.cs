using System.Windows.Threading;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 标签组件的写入结果。
/// </summary>
internal enum TagWriteOutcome
{
    /// <summary>
    /// 已写入内容层。
    /// </summary>
    Written,

    /// <summary>
    /// 词已经在目标上，没有改内容层。
    /// </summary>
    Unchanged,

    /// <summary>
    /// 维护者取消，或输入为空。
    /// </summary>
    Cancelled,

    /// <summary>
    /// 预演拒绝或写入失败。
    /// </summary>
    Failed
}

/// <summary>
/// 检视栏与标签窗共用的写入。确认框与预演都在这里。
/// </summary>
internal static class TagEditorHost
{
    private static readonly object mPersistGate = new();
    private static Task mPersistChain = Task.CompletedTask;
    private static int mPersistEpoch;
    /// <summary>
    /// 把输入写到这些帧上。多张直接写入。新词在写入成功后记入预设。
    /// 字面被拒绝时把原因放进 <paramref name="inputNotice"/>，不弹窗。
    /// </summary>
    public static TagWriteOutcome CommitFrames(
        WorkspaceSession session,
        string channel,
        IReadOnlyList<(string WorkId, string ObjectKey, IReadOnlyList<string> Themes, IReadOnlyList<string> Tags)> frames,
        IReadOnlyList<TagEditorItem> suggestions,
        string text,
        out string? inputNotice,
        Action<string> onPersistFailed)
    {
        inputNotice = null;
        if (!TagEditorCatalog.TryCommitText(text, suggestions, out var existing, out var newLabel, out var error))
        {
            if (error != null)
            {
                inputNotice = error;
                return TagWriteOutcome.Failed;
            }

            return TagWriteOutcome.Cancelled;
        }

        var item = existing ?? NewFree(newLabel!);
        if (frames.Count == 0 || frames.All(frame => TagEditorCatalog.FrameHas(frame.Themes, frame.Tags, item)))
        {
            return frames.Count == 0 ? TagWriteOutcome.Cancelled : TagWriteOutcome.Unchanged;
        }

        var writes = frames.Select(frame =>
        {
            var next = TagEditorCatalog.WithAdded(frame.Themes, frame.Tags, item);
            return new TagFrameWrite
            {
                WorkId = frame.WorkId,
                ObjectKey = frame.ObjectKey,
                Themes = next.Themes,
                Tags = next.Tags
            };
        }).ToList();
        var outcome = Write(session, channel, writes, onPersistFailed);
        if (outcome == TagWriteOutcome.Written && newLabel != null)
        {
            TagVocabStore.Remember(session.Profile.ResolvedRoot, channel, newLabel);
        }

        return outcome;
    }

    /// <summary>
    /// 从这些帧上去掉一枚。多张直接写入。
    /// </summary>
    public static TagWriteOutcome RemoveFromFrames(
        WorkspaceSession session,
        string channel,
        IReadOnlyList<(string WorkId, string ObjectKey, IReadOnlyList<string> Themes, IReadOnlyList<string> Tags)> frames,
        TagEditorItem item,
        Action<string> onPersistFailed)
    {
        var touched = frames.Where(frame => TagEditorCatalog.FrameHas(frame.Themes, frame.Tags, item)).ToList();
        if (touched.Count == 0)
        {
            return TagWriteOutcome.Unchanged;
        }

        var writes = touched.Select(frame =>
        {
            var next = TagEditorCatalog.WithRemoved(frame.Themes, frame.Tags, item);
            return new TagFrameWrite
            {
                WorkId = frame.WorkId,
                ObjectKey = frame.ObjectKey,
                Themes = next.Themes,
                Tags = next.Tags
            };
        }).ToList();
        return Write(session, channel, writes, onPersistFailed);
    }

    /// <summary>
    /// 点下拉：尚未全部带着就加上，已经全部带着就去掉。
    /// </summary>
    public static TagWriteOutcome ToggleFrames(
        WorkspaceSession session,
        string channel,
        IReadOnlyList<(string WorkId, string ObjectKey, IReadOnlyList<string> Themes, IReadOnlyList<string> Tags)> frames,
        TagEditorItem item,
        Action<string> onPersistFailed)
    {
        if (frames.Count > 0 && frames.All(frame => TagEditorCatalog.FrameHas(frame.Themes, frame.Tags, item)))
        {
            return RemoveFromFrames(session, channel, frames, item, onPersistFailed);
        }

        if (frames.Count == 0 || frames.All(frame => TagEditorCatalog.FrameHas(frame.Themes, frame.Tags, item)))
        {
            return TagWriteOutcome.Unchanged;
        }

        var writes = frames.Select(frame =>
        {
            var next = TagEditorCatalog.WithAdded(frame.Themes, frame.Tags, item);
            return new TagFrameWrite
            {
                WorkId = frame.WorkId,
                ObjectKey = frame.ObjectKey,
                Themes = next.Themes,
                Tags = next.Tags
            };
        }).ToList();
        return Write(session, channel, writes, onPersistFailed);
    }

    /// <summary>
    /// 给作品级自由标签加上输入。不改类型。
    /// 字面被拒绝时把原因放进 <paramref name="inputNotice"/>，不弹窗。
    /// </summary>
    public static TagWriteOutcome CommitWork(
        WorkspaceSession session,
        WorkCatalogItem work,
        IReadOnlyList<TagEditorItem> suggestions,
        string text,
        out string? inputNotice,
        Action<string> onPersistFailed)
    {
        inputNotice = null;
        if (!TagEditorCatalog.TryCommitText(text, suggestions, out var existing, out var newLabel, out var error))
        {
            if (error != null)
            {
                inputNotice = error;
                return TagWriteOutcome.Failed;
            }

            return TagWriteOutcome.Cancelled;
        }

        if (existing is { Kind: TagEditorKind.Type })
        {
            inputNotice = "项目不写主题类型。类型写在单张照片上。";
            return TagWriteOutcome.Failed;
        }

        var label = existing?.Label ?? newLabel!;
        if (work.Tags.Any(tag => string.Equals(tag, label, StringComparison.Ordinal)))
        {
            return TagWriteOutcome.Unchanged;
        }

        var next = TagEditorCatalog.WithAdded(Array.Empty<string>(), work.Tags, NewFree(label));
        var outcome = Write(session, work.Channel, new[]
        {
            new TagFrameWrite { WorkId = work.Id, Tags = next.Tags }
        }, onPersistFailed);
        if (outcome == TagWriteOutcome.Written && newLabel != null)
        {
            TagVocabStore.Remember(session.Profile.ResolvedRoot, work.Channel, newLabel);
        }

        return outcome;
    }

    /// <summary>
    /// 从作品级自由标签去掉一枚。
    /// </summary>
    public static TagWriteOutcome RemoveWork(
        WorkspaceSession session,
        WorkCatalogItem work,
        TagEditorItem item,
        Action<string> onPersistFailed)
    {
        if (item.Kind == TagEditorKind.Type || !TagEditorCatalog.FrameHas(Array.Empty<string>(), work.Tags, item))
        {
            return TagWriteOutcome.Unchanged;
        }

        var next = TagEditorCatalog.WithRemoved(Array.Empty<string>(), work.Tags, item);
        return Write(session, work.Channel, new[]
        {
            new TagFrameWrite { WorkId = work.Id, Tags = next.Tags }
        }, onPersistFailed);
    }

    /// <summary>
    /// 点作品级下拉：已有则去掉，没有则加上。
    /// 点到类型时把原因放进 <paramref name="inputNotice"/>，不弹窗。
    /// </summary>
    public static TagWriteOutcome ToggleWork(
        WorkspaceSession session,
        WorkCatalogItem work,
        TagEditorItem item,
        out string? inputNotice,
        Action<string> onPersistFailed)
    {
        inputNotice = null;
        if (item.Kind == TagEditorKind.Type)
        {
            inputNotice = "项目不写主题类型。类型写在单张照片上。";
            return TagWriteOutcome.Failed;
        }

        if (TagEditorCatalog.FrameHas(Array.Empty<string>(), work.Tags, item))
        {
            return RemoveWork(session, work, item, onPersistFailed);
        }

        var next = TagEditorCatalog.WithAdded(Array.Empty<string>(), work.Tags, item);
        var outcome = Write(session, work.Channel, new[]
        {
            new TagFrameWrite { WorkId = work.Id, Tags = next.Tags }
        }, onPersistFailed);
        if (outcome == TagWriteOutcome.Written)
        {
            TagVocabStore.Remember(session.Profile.ResolvedRoot, work.Channel, item.Label);
        }

        return outcome;
    }

    /// <summary>
    /// 列出还在用的作品，确认后从内容层去掉，并删除预设。
    /// </summary>
    public static TagWriteOutcome DeletePreset(
        WorkspaceSession session,
        string channel,
        string label,
        Action<string> onPersistFailed)
    {
        var works = session.Works
            .Where(work => !work.IsUnregistered && string.Equals(work.Channel, channel, StringComparison.Ordinal))
            .ToList();
        var hits = TagEditorCatalog.FindFreeTagUsages(works, label);
        if (!ConfirmIntentDialog.Show(
                TagEditorCatalog.DescribePresetDelete(label, hits),
                requireAck: false,
                title: "删除标签",
                okLabel: "删除"))
        {
            return TagWriteOutcome.Cancelled;
        }

        var writes = TagEditorCatalog.StripFreeTag(works, label);
        var outcome = writes.Count == 0
            ? TagWriteOutcome.Written
            : Write(session, channel, writes, onPersistFailed);
        if (outcome == TagWriteOutcome.Written)
        {
            TagVocabStore.Forget(session.Profile.ResolvedRoot, channel, label);
        }

        return outcome;
    }

    /// <summary>
    /// 一条新的自由标签。
    /// </summary>
    private static TagEditorItem NewFree(string label)
    {
        return new TagEditorItem
        {
            Id = label,
            Label = label,
            Kind = TagEditorKind.Free,
            AllowPresetDelete = true
        };
    }

    /// <summary>
    /// 预演通过后先改内存里的标签，再把写盘排到后台。界面不用等整份编目重载。
    /// </summary>
    private static TagWriteOutcome Write(
        WorkspaceSession session,
        string channel,
        IReadOnlyList<TagFrameWrite> writes,
        Action<string> onPersistFailed)
    {
        var document = IntentDocumentBuilder.BuildTagFrames(session, ExecutionMode.Direct, channel, writes);
        var report = PreviewReporter.BuildFromDocument(session, document);
        if (report.HasHardError)
        {
            TextDialog.Show("无法写入", PromptRenderer.RenderPreview(report));
            return TagWriteOutcome.Failed;
        }

        ApplyToCatalog(session, channel, writes);
        EnqueuePersist(session, report, onPersistFailed);
        return TagWriteOutcome.Written;
    }

    /// <summary>
    /// 把这次标签写进当前编目，芯片可以马上画出来。
    /// </summary>
    private static void ApplyToCatalog(
        WorkspaceSession session,
        string channel,
        IReadOnlyList<TagFrameWrite> writes)
    {
        foreach (var write in writes)
        {
            var work = session.Works.FirstOrDefault(item =>
                !item.IsUnregistered
                && string.Equals(item.Channel, channel, StringComparison.Ordinal)
                && string.Equals(item.Id, write.WorkId, StringComparison.Ordinal));
            if (work == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(write.ObjectKey))
            {
                work.Tags = write.Tags.ToList();
                continue;
            }

            var rel = JsonUtil.ToRel(write.ObjectKey);
            var media = work.Media.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.Src)
                && string.Equals(JsonUtil.ToRel(item.Src!), rel, StringComparison.Ordinal));
            if (media == null)
            {
                continue;
            }

            media.Themes = write.Themes.ToList();
            media.Tags = write.Tags.ToList();
            work.Themes = PhotoFactRules.ThemesOfPublishedMedia(work);
        }
    }

    /// <summary>
    /// 按顺序写盘。失败则作废还在排队的写入，并回到界面上重载。
    /// </summary>
    private static void EnqueuePersist(WorkspaceSession session, PreviewReport report, Action<string> onPersistFailed)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        int epoch;
        lock (mPersistGate)
        {
            epoch = mPersistEpoch;
            mPersistChain = mPersistChain.ContinueWith(
                antecedent =>
                {
                    _ = antecedent.Exception;
                    Persist(session, report, epoch, dispatcher, onPersistFailed);
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// 后台写入一条标签意图。
    /// </summary>
    private static void Persist(
        WorkspaceSession session,
        PreviewReport report,
        int epoch,
        Dispatcher dispatcher,
        Action<string> onPersistFailed)
    {
        lock (mPersistGate)
        {
            if (epoch != mPersistEpoch)
            {
                return;
            }
        }

        string? failure;
        bool ok;
        try
        {
            ok = TagWindow.TryWrite(session, report, out failure);
        }
        catch (Exception ex)
        {
            ok = false;
            failure = ex.Message;
        }

        if (ok)
        {
            return;
        }

        lock (mPersistGate)
        {
            mPersistEpoch++;
        }

        var text = string.IsNullOrWhiteSpace(failure) ? "写入失败。" : failure;
        dispatcher.BeginInvoke(() => onPersistFailed(text));
    }
}
