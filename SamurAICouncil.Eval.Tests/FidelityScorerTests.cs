using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Eval.Scoring;

namespace SamurAICouncil.Eval.Tests;

[TestClass]
public class FidelityScorerTests
{
    private static List<Dictionary<string, object?>> Rows(params (string, object?)[][] rows)
        => rows.Select(r => r.ToDictionary(kv => kv.Item1, kv => kv.Item2)).ToList();

    [TestMethod]
    public void Studio_recommendation_scores_full_fidelity()
    {
        var rows = Rows(
            [("Channel", "Store"), ("Sales", (object?)2228460305.60)],
            [("Channel", "Online"), ("Sales", (object?)937069110.93)],
            [("Channel", "Reseller"), ("Sales", (object?)600175898.67)]);

        // Studio binds values straight from the rows, so fidelity should be perfect.
        var rec = new StudioClassifier().Classify("sales by channel", rows);
        var result = FidelityScorer.Score(rec, rows, "sales by channel");

        Assert.AreEqual(1.0, result.NumberMatchRate, 1e-9, "Studio values must trace to the source rows.");
        Assert.AreEqual(0, result.HallucinatedCount);
    }

    [TestMethod]
    public void Planted_hallucination_is_flagged()
    {
        var rows = Rows(
            [("Channel", "Store"), ("Sales", (object?)2228460305.60)],
            [("Channel", "Online"), ("Sales", (object?)937069110.93)]);

        // A Classic-style rec where one plotted value matches no row (and isn't a percentage).
        var rec = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Sales by Channel",
            Labels = ["Store", "Online"],
            Series = [new ChartSeriesData { Name = "Sales", Values = [2228460305.60, 123456789.0] }],
        };

        var result = FidelityScorer.Score(rec, rows, "sales by channel");

        Assert.IsTrue(result.NumberMatchRate < 1.0, "An unmatched value should lower the match rate.");
        Assert.IsTrue(result.HallucinatedCount >= 1, "The fabricated value should be flagged as hallucinated.");
    }

    [TestMethod]
    public void Percentage_values_are_treated_as_derived_not_hallucinated()
    {
        var rows = Rows(
            [("Channel", "Store"), ("Sales", (object?)540.0)],
            [("Channel", "Online"), ("Sales", (object?)460.0)]);

        // Percent chart: values sum to ~100 and query asks for share -> derived, not hallucinated.
        var rec = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Labels = ["Store", "Online"],
            Series = [new ChartSeriesData { Name = "Share", Values = [54.0, 46.0] }],
        };

        var result = FidelityScorer.Score(rec, rows, "what percentage of sales by channel");

        Assert.IsTrue(result.LikelyDerived);
        Assert.AreEqual(0, result.HallucinatedCount);
    }
}
