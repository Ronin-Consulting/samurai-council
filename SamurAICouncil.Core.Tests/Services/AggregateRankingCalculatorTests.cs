using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

[TestClass]
public class AggregateRankingCalculatorTests
{
    [TestMethod]
    public void Calculate_WithEmptyStage2Results_ReturnsEmptyList()
    {
        var stage2Results = new List<Stage2Ranking>();
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "openai/gpt-4"
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Calculate_WithEmptyLabelToModel_ReturnsEmptyList()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new() { Model = "reviewer", ParsedRanking = ["Response A"] }
        };
        var labelToModel = new Dictionary<string, string>();

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Calculate_WithSingleRanking_ReturnsCorrectAverages()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new()
            {
                Model = "reviewer1",
                ParsedRanking = ["Response A", "Response B", "Response C"]
            }
        };
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "openai/gpt-4",
            ["Response B"] = "anthropic/claude-3",
            ["Response C"] = "google/gemini-pro"
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        Assert.AreEqual(3, result.Count);

        // First place should have average rank 1
        Assert.AreEqual("openai/gpt-4", result[0].Model);
        Assert.AreEqual(1.0, result[0].AverageRank);

        // Second place should have average rank 2
        Assert.AreEqual("anthropic/claude-3", result[1].Model);
        Assert.AreEqual(2.0, result[1].AverageRank);

        // Third place should have average rank 3
        Assert.AreEqual("google/gemini-pro", result[2].Model);
        Assert.AreEqual(3.0, result[2].AverageRank);
    }

    [TestMethod]
    public void Calculate_WithMultipleRankings_CalculatesCorrectAverages()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new()
            {
                Model = "reviewer1",
                ParsedRanking = ["Response A", "Response B", "Response C"] // A=1, B=2, C=3
            },
            new()
            {
                Model = "reviewer2",
                ParsedRanking = ["Response B", "Response A", "Response C"] // A=2, B=1, C=3
            },
            new()
            {
                Model = "reviewer3",
                ParsedRanking = ["Response A", "Response C", "Response B"] // A=1, B=3, C=2
            }
        };
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "model-a",
            ["Response B"] = "model-b",
            ["Response C"] = "model-c"
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        Assert.AreEqual(3, result.Count);

        // Model A: (1 + 2 + 1) / 3 = 1.33
        var modelA = result.First(r => r.Model == "model-a");
        Assert.AreEqual(1.33, modelA.AverageRank, 0.01);
        Assert.AreEqual(3, modelA.RankingsCount);

        // Model B: (2 + 1 + 3) / 3 = 2.0
        var modelB = result.First(r => r.Model == "model-b");
        Assert.AreEqual(2.0, modelB.AverageRank, 0.01);

        // Model C: (3 + 3 + 2) / 3 = 2.67
        var modelC = result.First(r => r.Model == "model-c");
        Assert.AreEqual(2.67, modelC.AverageRank, 0.01);
    }

    [TestMethod]
    public void Calculate_SortsByAverageRankAscending()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new()
            {
                Model = "reviewer1",
                ParsedRanking = ["Response C", "Response A", "Response B"]
            }
        };
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "model-a",
            ["Response B"] = "model-b",
            ["Response C"] = "model-c"
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        // Should be sorted: C (1st), A (2nd), B (3rd)
        Assert.AreEqual("model-c", result[0].Model);
        Assert.AreEqual("model-a", result[1].Model);
        Assert.AreEqual("model-b", result[2].Model);
    }

    [TestMethod]
    public void Calculate_WithPartialRankings_HandlesGracefully()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new()
            {
                Model = "reviewer1",
                ParsedRanking = ["Response A", "Response B"] // Only 2 responses ranked
            }
        };
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "model-a",
            ["Response B"] = "model-b",
            ["Response C"] = "model-c" // Not ranked
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        // Only ranked models should appear
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(r => r.Model == "model-a"));
        Assert.IsTrue(result.Any(r => r.Model == "model-b"));
        Assert.IsFalse(result.Any(r => r.Model == "model-c"));
    }

    [TestMethod]
    public void Calculate_WithEmptyParsedRankings_SkipsRanking()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new()
            {
                Model = "reviewer1",
                ParsedRanking = [] // Empty ranking
            },
            new()
            {
                Model = "reviewer2",
                ParsedRanking = ["Response A", "Response B"]
            }
        };
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "model-a",
            ["Response B"] = "model-b"
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        // Should only count the valid ranking
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, result[0].RankingsCount);
    }

    [TestMethod]
    public void Calculate_WithUnknownLabels_IgnoresThem()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new()
            {
                Model = "reviewer1",
                ParsedRanking = ["Response A", "Response X", "Response B"] // X is unknown
            }
        };
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "model-a",
            ["Response B"] = "model-b"
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        // Should only include known models
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1.0, result.First(r => r.Model == "model-a").AverageRank);
        Assert.AreEqual(3.0, result.First(r => r.Model == "model-b").AverageRank); // Position 3, not 2
    }

    [TestMethod]
    public void Calculate_RoundsToTwoDecimalPlaces()
    {
        var stage2Results = new List<Stage2Ranking>
        {
            new() { Model = "r1", ParsedRanking = ["Response A", "Response B", "Response C"] },
            new() { Model = "r2", ParsedRanking = ["Response B", "Response A", "Response C"] },
            new() { Model = "r3", ParsedRanking = ["Response C", "Response A", "Response B"] }
        };
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "model-a",
            ["Response B"] = "model-b",
            ["Response C"] = "model-c"
        };

        var result = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        // All averages should be rounded to 2 decimal places
        foreach (var ranking in result)
        {
            var decimalPlaces = BitConverter.GetBytes(decimal.GetBits((decimal)ranking.AverageRank)[3])[2];
            Assert.IsTrue(decimalPlaces <= 2, $"Expected <= 2 decimal places for {ranking.AverageRank}");
        }
    }
}
