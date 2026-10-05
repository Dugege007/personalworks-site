using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PublishStatusTests
{
    [Theory]
    [InlineData("published", "已发布")]
    [InlineData("PUBLISHED", "已发布")]
    [InlineData("withdrawn", "已撤下")]
    [InlineData("stock", "常驻")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void ToChinese_MapsKnownLedgerKeys(string? status, string expected)
    {
        Assert.Equal(expected, PublishStatus.ToChinese(status));
    }

    [Fact]
    public void LedgerLine_Empty_IsNone()
    {
        Assert.Equal("无", PublishStatus.LedgerLine(null));
        Assert.Equal("已发布", PublishStatus.LedgerLine("published"));
    }

    [Fact]
    public void ForStage_Stock_IsStockLabel()
    {
        var stage = new StageItem
        {
            ChannelKey = "demo-render",
            StageRel = "demo-render/stock/keep.png",
            FullPath = "x",
            Length = 1,
            IsStock = true
        };

        Assert.Equal("常驻", PublishStatus.ForStage(stage));
    }

    [Fact]
    public void ForStage_Published_IsChinese()
    {
        var stage = new StageItem
        {
            ChannelKey = "demo-render",
            StageRel = "demo-render/demo-park/01.png",
            FullPath = "x",
            Length = 1,
            LedgerStatus = "published",
            MatchedObject = "demo-render/demo-park/01.png"
        };

        Assert.Equal("已发布", PublishStatus.ForStage(stage));
    }

    [Fact]
    public void ForStage_PendingWithdraw_IsWithdrawnWhileLedgerPublished()
    {
        var stage = new StageItem
        {
            ChannelKey = "real-world-photo",
            StageRel = "real-world-photo/04.webp",
            FullPath = "x",
            Length = 1,
            LedgerStatus = "published",
            MatchedObject = "photo/real-world-photo/shoot/04.webp",
            IsPendingWithdraw = true
        };

        Assert.Equal("已撤下", PublishStatus.ForStage(stage));
    }

    [Fact]
    public void ForStage_NoLedger_IsUnpublished()
    {
        var stage = new StageItem
        {
            ChannelKey = "demo-render",
            StageRel = "demo-render/unused.png",
            FullPath = "x",
            Length = 1
        };

        Assert.Equal("未上页", PublishStatus.ForStage(stage));
        Assert.False(PublishStatus.IsStageRemarkLocked(stage));
    }

    [Fact]
    public void IsStageRemarkLocked_NoteListedWithoutLedger_StaysOpen()
    {
        var stage = new StageItem
        {
            ChannelKey = "notes",
            StageRel = "心得（notes）/20261005_021341/正文.md",
            FullPath = "x",
            IsNoteListed = true
        };

        Assert.Equal("已发布", PublishStatus.ForStage(stage));
        Assert.False(PublishStatus.IsStageRemarkLocked(stage));
    }

    [Fact]
    public void IsStageRemarkLocked_NoteListedWithLedger_StaysLocked()
    {
        var stage = new StageItem
        {
            ChannelKey = "notes",
            StageRel = "心得（notes）/20261005_021341/shot.jpg",
            FullPath = "x",
            LedgerStatus = "published",
            IsNoteListed = true
        };

        Assert.Equal("已发布", PublishStatus.ForStage(stage));
        Assert.True(PublishStatus.IsStageRemarkLocked(stage));
    }

    [Fact]
    public void IsStageRemarkLocked_LedgerPublished_IsLocked()
    {
        var stage = new StageItem
        {
            ChannelKey = "demo-render",
            StageRel = "demo-render/demo-park/01.png",
            FullPath = "x",
            LedgerStatus = "published"
        };

        Assert.True(PublishStatus.IsStageRemarkLocked(stage));
    }

    [Fact]
    public void IsStageRemarkLocked_PendingWithdraw_StaysLocked()
    {
        var stage = new StageItem
        {
            ChannelKey = "real-world-photo",
            StageRel = "real-world-photo/04.webp",
            FullPath = "x",
            LedgerStatus = "published",
            IsPendingWithdraw = true
        };

        Assert.Equal("已撤下", PublishStatus.ForStage(stage));
        Assert.True(PublishStatus.IsStageRemarkLocked(stage));
    }

    [Fact]
    public void IsStageRemarkLocked_NoteHiddenWithLedger_DoesNotAddLock()
    {
        var stage = new StageItem
        {
            ChannelKey = "notes",
            StageRel = "心得（notes）/20261005_021341/shot.jpg",
            FullPath = "x",
            LedgerStatus = "published",
            IsNoteListed = true,
            IsNoteHidden = true
        };

        Assert.Equal("已隐藏", PublishStatus.ForStage(stage));
        Assert.False(PublishStatus.IsStageRemarkLocked(stage));
    }

    [Fact]
    public void ForSite_MissingLedger_IsUnlisted()
    {
        var site = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "园",
            Label = "01",
            ObjectKey = "demo-render/demo-park/01.png"
        };

        Assert.Equal("未登记", PublishStatus.ForSite(site));
    }

    [Fact]
    public void Unsigned_IsChineseLabel()
    {
        Assert.Equal("未脱敏", PublishStatus.Unsigned);
    }

    [Fact]
    public void ForSite_Hidden_IsChinese()
    {
        var site = new SiteItem
        {
            WorkId = "demo-park",
            ChannelKey = "demo-render",
            WorkTitle = "园",
            Label = "02",
            ObjectKey = "demo-render/demo-park/02.png",
            LedgerStatus = "published",
            IsHidden = true
        };

        Assert.Equal("已隐藏", PublishStatus.ForSite(site));
    }
}
