using SamurAICouncil.Core.Models;
using SamurAICouncil.Web.Components.Shared;

namespace SamurAICouncil.Web.Tests.Components;

[TestClass]
public class AssistantMessagePanelTests : BunitTestBase
{
    [TestMethod]
    public void AssistantMessagePanel_WithNullMessage_ShowsEmptyState()
    {
        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, null));

        Assert.IsTrue(cut.Markup.Contains("No message data"));
    }

    [TestMethod]
    public void AssistantMessagePanel_WithStage1Loading_ShowsLoadingProgress()
    {
        var message = new AssistantMessage
        {
            Loading = new LoadingState { Stage1 = true }
        };

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        Assert.IsTrue(cut.Markup.Contains("Collecting responses from AI models..."));
        // Step label is "Responses" in the new UI
        Assert.IsTrue(cut.Markup.Contains("Responses"));
    }

    [TestMethod]
    public void AssistantMessagePanel_WithStage2Loading_ShowsLoadingProgress()
    {
        var message = new AssistantMessage
        {
            Loading = new LoadingState { Stage2 = true }
        };

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        Assert.IsTrue(cut.Markup.Contains("Models are peer-reviewing responses..."));
        // Step label is "Review" in the new UI
        Assert.IsTrue(cut.Markup.Contains("Review"));
    }

    [TestMethod]
    public void AssistantMessagePanel_WithStage3Loading_ShowsLoadingProgress()
    {
        var message = new AssistantMessage
        {
            Loading = new LoadingState { Stage3 = true }
        };

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        Assert.IsTrue(cut.Markup.Contains("Chairman is synthesizing the final answer..."));
        // Step label is "Synthesis" in the new UI
        Assert.IsTrue(cut.Markup.Contains("Synthesis"));
    }

    [TestMethod]
    public void AssistantMessagePanel_WithCompleteData_ShowsStageTabs()
    {
        var message = CreateCompleteMessage();

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        Assert.IsTrue(cut.Markup.Contains("Final Answer"));
        Assert.IsTrue(cut.Markup.Contains("Responses"));
        Assert.IsTrue(cut.Markup.Contains("Rankings"));
    }

    [TestMethod]
    public void AssistantMessagePanel_WithCompleteData_DefaultsToFinalAnswer()
    {
        var message = CreateCompleteMessage();

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        // The Final Answer tab content should be visible by default
        // Stage3Panel shows "Stage 3: Final Synthesis"
        Assert.IsTrue(cut.Markup.Contains("Stage 3: Final Synthesis"));
    }

    [TestMethod]
    public void AssistantMessagePanel_WithCompleteData_ShowsStage3Content()
    {
        var message = CreateCompleteMessage();

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        // Should show the chairman and response
        Assert.IsTrue(cut.Markup.Contains("Chairman"));
        Assert.IsTrue(cut.Markup.Contains("This is the final synthesized answer"));
    }

    [TestMethod]
    public void AssistantMessagePanel_ShowsResponseCount()
    {
        var message = CreateCompleteMessage();

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        // Should show the count chip with "2"
        Assert.IsTrue(cut.Markup.Contains(">2<"));
    }

    [TestMethod]
    public void AssistantMessagePanel_DisablesTabsWithNoData()
    {
        var message = new AssistantMessage
        {
            Stage3 = new Stage3Response { Model = "test", Response = "Final answer only" }
            // No Stage1 or Stage2 data
        };

        var cut = Render<AssistantMessagePanel>(parameters => parameters
            .Add(p => p.Message, message));

        // Should have tabs - MudTabs uses mud-tab-panel
        Assert.IsTrue(cut.Markup.Contains("mud-tabs"));
        // Final Answer should be visible
        Assert.IsTrue(cut.Markup.Contains("Final Answer"));
    }

    private static AssistantMessage CreateCompleteMessage()
    {
        return new AssistantMessage
        {
            Stage1 = new List<Stage1Response>
            {
                new() { Model = "openai/gpt-4", Response = "GPT-4 response" },
                new() { Model = "anthropic/claude-3", Response = "Claude response" }
            },
            Stage2 = new List<Stage2Ranking>
            {
                new() { Model = "openai/gpt-4", Ranking = "Ranking 1", ParsedRanking = new List<string> { "Response A" } },
                new() { Model = "anthropic/claude-3", Ranking = "Ranking 2", ParsedRanking = new List<string> { "Response B" } }
            },
            Stage3 = new Stage3Response
            {
                Model = "anthropic/claude-3-opus",
                Response = "This is the final synthesized answer."
            },
            Metadata = new CouncilMetadata
            {
                LabelToModel = new Dictionary<string, string>
                {
                    { "Response A", "openai/gpt-4" },
                    { "Response B", "anthropic/claude-3" }
                },
                AggregateRankings = new List<AggregateRanking>
                {
                    new() { Model = "openai/gpt-4", AverageRank = 1.5, RankingsCount = 2 }
                }
            }
        };
    }
}
