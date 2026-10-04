using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio.Tests;

public sealed class PromptRendererTests
{
    [Fact]
    public void RenderPrompt_ContainsIntentJson()
    {
        var profilePath = ToolPaths.FindFixtureProfile();
        Assert.NotNull(profilePath);
        var session = WorkspaceSession.Load(profilePath!);
        var park = session.SiteItems.First(item => item.ObjectKey.EndsWith("demo-park/01.png", StringComparison.OrdinalIgnoreCase));
        var markedList = new List<(MediaIntent Intent, StageItem? Stage, SiteItem? Site)>
        {
            (MediaIntent.SiteHide, null, park)
        };
        var document = IntentDocumentBuilder.Build(session, ExecutionMode.Prompt, markedList);
        var resolvedList = new List<(IntentItem Item, StageItem? Stage, SiteItem? Site)>
        {
            (document.Items[0], null, park)
        };
        var report = PreviewReporter.Build(session, document, resolvedList, park.WorkId);
        var prompt = PromptRenderer.RenderPrompt(report);
        Assert.Contains("\"intent\": \"site.hide\"", prompt, StringComparison.Ordinal);
        Assert.Contains("demo-render/demo-park/01.png", prompt, StringComparison.Ordinal);
        Assert.Contains("禁止 deploy --prune-assets", prompt, StringComparison.Ordinal);
        Assert.Contains("stars.update 只改对应 media.stars", prompt, StringComparison.Ordinal);
    }
}
