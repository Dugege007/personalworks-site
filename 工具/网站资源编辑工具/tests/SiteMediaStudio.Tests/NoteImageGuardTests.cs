using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class NoteImageGuardTests
{
    private const string ChannelFolder = "心得（notes）";
    private const string Folder = "20261005_021341";

    [Fact]
    public void ListedVisibleImage_IsBlocked()
    {
        var shot = Image("shot.jpg");
        Apply(new[] { shot }, Entry("shot.jpg"));

        Assert.True(shot.IsNoteListed);
        Assert.False(shot.IsNoteHidden);
        Assert.Equal(NoteImageGuard.PublishedBlockText, NoteImageGuard.PublishedReason(shot));
    }

    [Fact]
    public void ListedHiddenNote_IsStillBlocked()
    {
        var shot = Image("shot.jpg");
        Apply(new[] { shot }, Entry("shot.jpg", hidden: true));

        Assert.True(shot.IsNoteListed);
        Assert.True(shot.IsNoteHidden);
        Assert.Equal(NoteImageGuard.PublishedBlockText, NoteImageGuard.PublishedReason(shot));
    }

    [Fact]
    public void LedgerPublishedWithoutListing_IsBlocked()
    {
        var shot = Image("shot.jpg", "published");
        Apply(new[] { shot }, new NoteIndexFile());

        Assert.False(shot.IsNoteListed);
        Assert.Equal(NoteImageGuard.PublishedBlockText, NoteImageGuard.PublishedReason(shot));
    }

    [Fact]
    public void DraftEntry_AllowsCompression()
    {
        var shot = Image("shot.jpg");
        Apply(new[] { shot }, Entry("shot.jpg", draft: true));

        Assert.False(shot.IsNoteListed);
        Assert.Equal("", NoteImageGuard.PublishedReason(shot));
    }

    [Fact]
    public void EmptySlug_AllowsCompression()
    {
        var shot = Image("shot.jpg");
        Apply(new[] { shot }, Entry("shot.jpg", slug: ""));

        Assert.False(shot.IsNoteListed);
        Assert.Equal("", NoteImageGuard.PublishedReason(shot));
    }

    [Fact]
    public void ImageAddedAfterPublish_AllowsCompression()
    {
        var listed = Image("shot.jpg");
        var added = Image("extra.jpg");
        Apply(new[] { listed, added }, Entry("shot.jpg"));

        Assert.True(listed.IsNoteListed);
        Assert.False(added.IsNoteListed);
        Assert.Equal(NoteImageGuard.PublishedBlockText, NoteImageGuard.PublishedReason(listed));
        Assert.Equal("", NoteImageGuard.PublishedReason(added));
    }

    [Fact]
    public void DraftImageWithPublishedLedger_StaysBlockedByLedger()
    {
        var shot = Image("shot.jpg", "published");
        Apply(new[] { shot }, Entry("shot.jpg", draft: true));

        Assert.False(shot.IsNoteListed);
        Assert.Equal(NoteImageGuard.PublishedBlockText, NoteImageGuard.PublishedReason(shot));
    }

    [Fact]
    public void WithdrawnLedgerWithoutListing_AllowsCompression()
    {
        var shot = Image("shot.jpg", "withdrawn");
        Apply(new[] { shot }, new NoteIndexFile());

        Assert.False(shot.IsNoteListed);
        Assert.Equal("", NoteImageGuard.PublishedReason(shot));
    }

    private static void Apply(IReadOnlyList<StageItem> items, NoteIndexFile index)
    {
        NoteCatalog.ApplyPublishState(items, index, ChannelFolder);
    }

    private static void Apply(IReadOnlyList<StageItem> items, NoteEntry entry)
    {
        Apply(items, new NoteIndexFile { Notes = { entry } });
    }

    private static NoteEntry Entry(
        string imageName,
        bool draft = false,
        bool hidden = false,
        string slug = "n-20261005-021341")
    {
        var entry = new NoteEntry
        {
            Folder = Folder,
            Slug = slug,
            Draft = draft,
            Hidden = hidden
        };
        entry.Images.Add(new NoteImageMeta
        {
            StageRel = ChannelFolder + "/" + Folder + "/" + imageName
        });

        return entry;
    }

    private static StageItem Image(string name, string? ledgerStatus = null)
    {
        return new StageItem
        {
            ChannelKey = NoteRules.ChannelKey,
            StageRel = ChannelFolder + "/" + Folder + "/" + name,
            FullPath = name,
            LedgerStatus = ledgerStatus
        };
    }
}
