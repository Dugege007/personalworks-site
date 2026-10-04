using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class ChannelCopyReaderTests
{
    [Fact]
    public void LoadFromSiteTs_UnescapesNewlinesInLead()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sms-lead-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var lexiconPath = Path.Combine(dir, "lexicon.ts");
            var sitePath = Path.Combine(dir, "site.ts");
            File.WriteAllText(lexiconPath, """
                export const lexicon = {
                  digitalTwin: { key: "digital-twin" },
                };
                """);
            File.WriteAllText(sitePath, """
                export const categories = [
                  {
                    id: lexicon.digitalTwin.key,
                    lead: "- 使用 Blender 建模。\r\n- 下一行",
                  },
                ];
                """);

            var leadDict = ChannelCopyReader.LoadFromSiteTs(lexiconPath, sitePath);

            Assert.True(leadDict.TryGetValue("digital-twin", out var lead));
            Assert.Equal("- 使用 Blender 建模。\r\n- 下一行", lead);
            Assert.DoesNotContain("\\r\\n", lead, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
