using SamurAICouncil.Core.Models;
using SamurAICouncil.Web.Components.Shared;

namespace SamurAICouncil.Web.Tests.Components;

[TestClass]
public class Stage2PanelTests : BunitTestBase
{
    [TestMethod]
    public void Stage2Panel_WithNoRankings_ShowsEmptyState()
    {
        var cut = Render<Stage2Panel>(parameters => parameters
            .Add(p => p.Rankings, null));

        Assert.IsTrue(cut.Markup.Contains("No rankings available"));
    }

    [TestMethod]
    public void Stage2Panel_WithRankings_ShowsHeader()
    {
        var rankings = new List<Stage2Ranking>
        {
            new() { Model = "openai/gpt-4", Ranking = "Response A is best", ParsedRanking = new List<string> { "Response A" } },
            new() { Model = "anthropic/claude-3", Ranking = "Response B is best", ParsedRanking = new List<string> { "Response B" } }
        };

        var cut = Render<Stage2Panel>(parameters => parameters
            .Add(p => p.Rankings, rankings));

        Assert.IsTrue(cut.Markup.Contains("Stage 2: Peer Review"));
        Assert.IsTrue(cut.Markup.Contains("2 models ranked"));
    }

    [TestMethod]
    public void Stage2Panel_WithRankings_ShowsTabs()
    {
        var rankings = new List<Stage2Ranking>
        {
            new() { Model = "openai/gpt-4", Ranking = "Test ranking", ParsedRanking = new List<string>() },
            new() { Model = "google/gemini-pro", Ranking = "Test ranking", ParsedRanking = new List<string>() }
        };

        var cut = Render<Stage2Panel>(parameters => parameters
            .Add(p => p.Rankings, rankings));

        Assert.IsTrue(cut.Markup.Contains("GPT-4"));
        Assert.IsTrue(cut.Markup.Contains("Gemini Pro"));
    }

    [TestMethod]
    public void Stage2Panel_WithParsedRanking_ShowsExtractedRanking()
    {
        var rankings = new List<Stage2Ranking>
        {
            new()
            {
                Model = "openai/gpt-4",
                Ranking = "My ranking is...",
                ParsedRanking = new List<string> { "Response A", "Response B", "Response C" }
            }
        };

        var cut = Render<Stage2Panel>(parameters => parameters
            .Add(p => p.Rankings, rankings));

        Assert.IsTrue(cut.Markup.Contains("Extracted Ranking"));
        Assert.IsTrue(cut.Markup.Contains("Response A"));
        Assert.IsTrue(cut.Markup.Contains("Response B"));
        Assert.IsTrue(cut.Markup.Contains("Response C"));
    }

    [TestMethod]
    public void Stage2Panel_WithAggregateRankings_ShowsAggregateSection()
    {
        var rankings = new List<Stage2Ranking>
        {
            new() { Model = "openai/gpt-4", Ranking = "Test", ParsedRanking = new List<string>() }
        };

        var aggregateRankings = new List<AggregateRanking>
        {
            new() { Model = "anthropic/claude-3", AverageRank = 1.5, RankingsCount = 2 },
            new() { Model = "openai/gpt-4", AverageRank = 2.0, RankingsCount = 2 }
        };

        var cut = Render<Stage2Panel>(parameters => parameters
            .Add(p => p.Rankings, rankings)
            .Add(p => p.AggregateRankings, aggregateRankings));

        Assert.IsTrue(cut.Markup.Contains("Aggregate Rankings"));
        Assert.IsTrue(cut.Markup.Contains("#1"));
        Assert.IsTrue(cut.Markup.Contains("#2"));
        Assert.IsTrue(cut.Markup.Contains("1.50"));
        Assert.IsTrue(cut.Markup.Contains("2.00"));
    }

    [TestMethod]
    public void Stage2Panel_WithLabelToModel_DeAnonymizesLabels()
    {
        var rankings = new List<Stage2Ranking>
        {
            new()
            {
                Model = "openai/gpt-4",
                Ranking = "Response A is best",
                ParsedRanking = new List<string> { "Response A" }
            }
        };

        var labelToModel = new Dictionary<string, string>
        {
            { "Response A", "anthropic/claude-3-sonnet" }
        };

        var cut = Render<Stage2Panel>(parameters => parameters
            .Add(p => p.Rankings, rankings)
            .Add(p => p.LabelToModel, labelToModel));

        // Should show de-anonymized label in the parsed ranking list
        Assert.IsTrue(cut.Markup.Contains("Response A (Claude Sonnet)"));
    }
}
