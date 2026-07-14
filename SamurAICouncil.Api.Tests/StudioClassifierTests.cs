using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Api.Tests;

[TestClass]
public class StudioClassifierTests
{
    private static readonly StudioClassifier Classifier = new();

    private static List<Dictionary<string, object?>> Rows(params (string, object?)[][] rows)
        => rows.Select(r => r.ToDictionary(kv => kv.Item1, kv => kv.Item2)).ToList();

    [TestMethod]
    public void SingleNumericRow_IsStat()
    {
        var rec = Classifier.Classify("total revenue in 2008", Rows(
            [("TotalRevenue", (object?)4111233535.0)]));
        Assert.AreEqual(ChartType.Stat, rec.Type);
        Assert.AreEqual(1, rec.Stats!.Length);
        Assert.AreEqual("$", rec.Stats![0].Unit);
    }

    [TestMethod]
    public void CategoryPlusMeasure_IsBar()
    {
        var rec = Classifier.Classify("sales by channel", Rows(
            [("Channel", "Store"), ("Sales", 100.0)],
            [("Channel", "Online"), ("Sales", 50.0)]));
        Assert.AreEqual(ChartType.Bar, rec.Type);
        CollectionAssert.AreEqual(new[] { "Store", "Online" }, rec.Labels);
        CollectionAssert.AreEqual(new[] { 100.0, 50.0 }, rec.Series[0].Values);
    }

    [TestMethod]
    public void TemporalDimension_IsLine()
    {
        var rec = Classifier.Classify("monthly sales", Rows(
            [("Month", "Jan"), ("Sales", 1.0)],
            [("Month", "Feb"), ("Sales", 2.0)]));
        Assert.AreEqual(ChartType.Line, rec.Type);
    }

    [TestMethod]
    public void DimensionPlusTwoMeasures_IsGroupedBar()
    {
        var rec = Classifier.Classify("sales by channel 2007 vs 2008", Rows(
            [("Channel", "Store"), ("Sales2007", 1.0), ("Sales2008", 2.0)],
            [("Channel", "Online"), ("Sales2007", 3.0), ("Sales2008", 4.0)]));
        Assert.AreEqual(ChartType.GroupedBar, rec.Type);
        Assert.AreEqual(2, rec.Series.Length);
    }

    [TestMethod]
    public void TwoMeasures_IsScatter()
    {
        var rec = Classifier.Classify("price vs quantity", Rows(
            [("Price", 10.0), ("Qty", 3.0)],
            [("Price", 20.0), ("Qty", 1.0)]));
        Assert.AreEqual(ChartType.Scatter, rec.Type);
        Assert.AreEqual(2, rec.Series[0].Points!.Length);
    }

    [TestMethod]
    public void TwoDimensionsOneMeasure_PivotsToGroupedBar_OrStackedForComposition()
    {
        var data = Rows(
            [("Channel", "Store"), ("Year", "2007"), ("Sales", 1.0)],
            [("Channel", "Store"), ("Year", "2008"), ("Sales", 2.0)],
            [("Channel", "Online"), ("Year", "2007"), ("Sales", 3.0)],
            [("Channel", "Online"), ("Year", "2008"), ("Sales", 4.0)]);

        var grouped = Classifier.Classify("sales by channel and year", data);
        Assert.AreEqual(ChartType.GroupedBar, grouped.Type);

        var stacked = Classifier.Classify("revenue composition by channel and year", data);
        Assert.AreEqual(ChartType.StackedBar, stacked.Type);
    }

    [TestMethod]
    public void NoMeasure_IsTable()
    {
        var rec = Classifier.Classify("list products", Rows(
            [("Product", "A"), ("Category", "X"), ("Brand", "Contoso")]));
        Assert.AreEqual(ChartType.Table, rec.Type);
        Assert.AreEqual(3, rec.Table!.Columns.Length);
    }
}
