namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// GIF 隔帧丢弃时如何合并延时。延时单位与图像文件里的帧延时相同，压缩器使用百分之一秒。
/// </summary>
public static class NoteGifFramePolicy
{
    /// <summary>
    /// 保留偶数序号的帧，并把被丢弃的下一帧延时加到这一帧上，使总播放时长不变。
    /// 最后一帧没有下一帧可加时，只保留它自己的延时。不足两帧时原样返回。
    /// </summary>
    public static int[] CombineDroppedFrameDelays(IReadOnlyList<int> delayList)
    {
        if (delayList == null)
        {
            throw new ArgumentNullException(nameof(delayList));
        }

        if (delayList.Count <= 1)
        {
            return delayList.Count == 0
                ? Array.Empty<int>()
                : new[] { Math.Max(0, delayList[0]) };
        }

        var keptDelayList = new List<int>();
        for (var index = 0; index < delayList.Count; index += 2)
        {
            var delay = Math.Max(0, delayList[index]);
            if (index + 1 < delayList.Count)
            {
                delay += Math.Max(0, delayList[index + 1]);
            }

            keptDelayList.Add(delay);
        }

        return keptDelayList.ToArray();
    }
}
