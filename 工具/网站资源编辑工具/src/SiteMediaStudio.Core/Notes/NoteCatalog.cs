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

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
