using PersonalWorks.SiteMediaStudio.Core;
using System.Linq;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class FfmpegFrameExtractorTests
{
    [Fact]
    public void BuildExtractArgumentList_Accurate_SeeksThenDecodesToTime()
    {
        var args = FfmpegFrameExtractor.BuildExtractArgumentList(
            @"D:\clip.mp4",
            12.54,
            @"C:\tmp\a.jpg",
            accurate: true);
        Assert.Contains("-accurate_seek", args);
        Assert.Contains("-an", args);
        var ss = args.ToList().IndexOf("12.54");
        var input = args.ToList().IndexOf("-i");
        Assert.True(ss > 0);
        Assert.Equal("-ss", args[ss - 1]);
        Assert.True(args.ToList().IndexOf("-ss") < input);
        Assert.Equal(@"D:\clip.mp4", args[input + 1]);
    }

    [Fact]
    public void BuildExtractArgumentList_Thumbnail_OmitsAccurateFlags()
    {
        var args = FfmpegFrameExtractor.BuildExtractArgumentList(
            "clip.mp4",
            1,
            "out.jpg",
            accurate: false);
        Assert.DoesNotContain("-accurate_seek", args);
        Assert.Contains("-ss", args);
        Assert.Contains("1", args);
    }
}
