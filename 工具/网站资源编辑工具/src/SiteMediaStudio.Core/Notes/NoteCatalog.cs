namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 把心得夹编成左侧项目，不进作品登记窗。
/// </summary>
public static class NoteCatalog
{
    /// <summary>
    /// 每个含 <c>正文.md</c> 的夹一条。已上页的配图写入 <c>Media</c>。
    /// </summary>
    public static IReadOnlyList<WorkCatalogItem> Discover(WorkspaceProfile profile, IReadOnlyList<StageItem> stageItems)
    {
        var channel = profile.Channels.FirstOrDefault(item => NoteRules.IsNotesChannel(item.Key));
        if (channel == null)
        {
            return Array.Empty<WorkCatalogItem>();
        }

        var stageRoot = WorkspaceProfileLoader.ResolveUnderRoot(profile, profile.StageRoot);
        var channelDir = Path.GetFullPath(Path.Combine(
            stageRoot,
            WorkspaceProfileLoader.ChannelStageRelative(profile, channel).Replace('/', Path.DirectorySeparatorChar)));
        if (!Directory.Exists(channelDir))
        {
            return Array.Empty<WorkCatalogItem>();
        }

        var index = NoteIndexStore.Load(profile);
        var list = new List<WorkCatalogItem>();
        foreach (var dir in Directory.EnumerateDirectories(channelDir))
        {
            if (!File.Exists(Path.Combine(dir, NoteRules.BodyFileName)))
            {
                continue;
            }

            var folder = Path.GetFileName(dir);
            if (folder.StartsWith('.'))
            {
                continue;
            }

            var entry = index.Notes.FirstOrDefault(item =>
                string.Equals(item.Folder, folder, StringComparison.Ordinal));
            var config = NoteConfigStore.SyncTitle(dir, folder);
            var title = config.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = string.IsNullOrWhiteSpace(entry?.Title) ? folder : entry!.Title;
            }

            var media = new List<WorkMediaItem>();
            if (entry != null)
            {
                foreach (var image in entry.Images)
                {
                    if (string.IsNullOrWhiteSpace(image.ObjectKey))
                    {
                        continue;
                    }

                    media.Add(new WorkMediaItem
                    {
                        Kind = "image",
                        Label = Path.GetFileNameWithoutExtension(image.ObjectKey),
                        Src = image.ObjectKey,
                        DisplayName = NullIfEmpty(image.DisplayName),
                        Description = NullIfEmpty(image.Description),
                        Stars = image.Stars,
                        Tags = image.Tags
                    });
                }
            }

            list.Add(new WorkCatalogItem
            {
                Id = folder,
                Channel = NoteRules.ChannelKey,
                Title = title,
                Summary = string.IsNullOrWhiteSpace(config.Summary) ? entry?.Summary ?? "" : config.Summary,
                SourceKind = "note",
                SupportsAppend = false,
                IsUnregistered = false,
                IsHidden = entry?.Hidden == true,
                StageFolder = NoteRules.ChannelKey + "/" + folder,
                Media = media
            });
        }

        return list;
    }

    /// <summary>
    /// 按发布索引给正文和已收录配图打上已发布或已隐藏。不改媒体台账。
    /// </summary>
    public static void ApplyPublishState(WorkspaceProfile profile, IReadOnlyList<StageItem> stageItems)
    {
        var channel = profile.Channels.FirstOrDefault(item => NoteRules.IsNotesChannel(item.Key));
        if (channel == null)
        {
            return;
        }

        ApplyPublishState(
            stageItems,
            NoteIndexStore.Load(profile),
            WorkspaceProfileLoader.ChannelStageRelative(profile, channel));
    }

    /// <summary>
    /// 按给定索引给正文和已收录配图打上已发布或已隐藏。
    /// </summary>
    public static void ApplyPublishState(
        IReadOnlyList<StageItem> stageItems,
        NoteIndexFile index,
        string channelFolder)
    {
        foreach (var item in stageItems)
        {
            if (!NoteRules.IsNotesChannel(item.ChannelKey))
            {
                continue;
            }

            var folder = NoteRules.NoteFolderFromStageRel(item.StageRel, channelFolder);
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var entry = index.Notes.FirstOrDefault(note =>
                string.Equals(note.Folder, folder, StringComparison.Ordinal)
                && !note.Draft
                && !string.IsNullOrWhiteSpace(note.Slug));
            if (entry == null)
            {
                continue;
            }

            var listed = NoteRules.IsBodyStageRel(item.StageRel)
                || entry.Images.Any(image =>
                    string.Equals(JsonUtil.ToRel(image.StageRel), JsonUtil.ToRel(item.StageRel), StringComparison.Ordinal));
            if (!listed)
            {
                continue;
            }

            item.IsNoteListed = true;
            item.IsNoteHidden = entry.Hidden;
        }
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
