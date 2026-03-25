using SamurAICouncil.Core.Models;
using SamurAICouncil.Web.Components.Shared;

namespace SamurAICouncil.Web.Tests.Components;

[TestClass]
public class Stage1PanelTests : BunitTestBase
{
    [TestMethod]
    public void Stage1Panel_WithNoResponses_ShowsEmptyState()
    {
        var cut = Render<Stage1Panel>(parameters => parameters
            .Add(p => p.Responses, null));

        Assert.IsTrue(cut.Markup.Contains("No responses available"));
    }

    [TestMethod]
    public void Stage1Panel_WithEmptyList_ShowsEmptyState()
    {
        var cut = Render<Stage1Panel>(parameters => parameters
            .Add(p => p.Responses, new List<Stage1Response>()));

        Assert.IsTrue(cut.Markup.Contains("No responses available"));
    }

    [TestMethod]
    public void Stage1Panel_WithResponses_ShowsHeader()
    {
        var responses = new List<Stage1Response>
        {
            new() { Model = "openai/gpt-4", Response = "Response 1" },
            new() { Model = "anthropic/claude-3-sonnet", Response = "Response 2" }
        };

        var cut = Render<Stage1Panel>(parameters => parameters
            .Add(p => p.Responses, responses));

        Assert.IsTrue(cut.Markup.Contains("Stage 1: Individual Responses"));
        Assert.IsTrue(cut.Markup.Contains("2 models responded"));
    }

    [TestMethod]
    public void Stage1Panel_WithResponses_ShowsTabs()
    {
        var responses = new List<Stage1Response>
        {
            new() { Model = "openai/gpt-4", Response = "Response 1" },
            new() { Model = "anthropic/claude-3-sonnet", Response = "Response 2" }
        };

        var cut = Render<Stage1Panel>(parameters => parameters
            .Add(p => p.Responses, responses));

        // Check that tabs are rendered (short model names)
        Assert.IsTrue(cut.Markup.Contains("GPT-4"));
        Assert.IsTrue(cut.Markup.Contains("Claude Sonnet"));
    }

    [TestMethod]
    public void Stage1Panel_WithResponses_ShowsFirstResponseByDefault()
    {
        var responses = new List<Stage1Response>
        {
            new() { Model = "openai/gpt-4", Response = "This is the GPT-4 response content" },
            new() { Model = "anthropic/claude-3-sonnet", Response = "This is the Claude response content" }
        };

        var cut = Render<Stage1Panel>(parameters => parameters
            .Add(p => p.Responses, responses));

        Assert.IsTrue(cut.Markup.Contains("This is the GPT-4 response content"));
    }

    [TestMethod]
    public void Stage1Panel_HandlesLongModelNames()
    {
        var responses = new List<Stage1Response>
        {
            new() { Model = "openai/gpt-4-turbo-preview-super-long-name", Response = "Response" }
        };

        var cut = Render<Stage1Panel>(parameters => parameters
            .Add(p => p.Responses, responses));

        // Should render a tab - verify the component renders
        Assert.IsTrue(cut.Markup.Contains("Stage 1: Individual Responses"));
        // Now using MudTabs
        Assert.IsTrue(cut.Markup.Contains("mud-tabs"));
    }
}
