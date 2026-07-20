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

    // ---- regression tests: shape edge cases ----

    [TestMethod]
    public void RedundantTemporalColumns_CollapseToLine()
    {
        // A monthly trend whose SQL returns Year + Month + MonthLabel + Sales — three temporal-
        // looking columns describing one time axis, which must collapse to a Line, not fall back
        // to a ≥3-dims Table.
        var rec = Classifier.Classify("monthly sales trend 2009", Rows(
            [("CalendarYear", (object?)2009), ("CalendarMonth", 1), ("CalendarMonthLabel", "Jan"), ("TotalSales", 100.0)],
            [("CalendarYear", 2009), ("CalendarMonth", 2), ("CalendarMonthLabel", "Feb"), ("TotalSales", 120.0)],
            [("CalendarYear", 2009), ("CalendarMonth", 3), ("CalendarMonthLabel", "Mar"), ("TotalSales", 90.0)]));
        Assert.AreEqual(ChartType.Line, rec.Type);
        CollectionAssert.AreEqual(new[] { "Jan", "Feb", "Mar" }, rec.Labels); // readable label axis chosen
    }

    [TestMethod]
    public void KeyColumnWithName_ListsAsTable()
    {
        // "list the categories" → a Key (identifier) + a Name; the Key must not be treated as a measure.
        var rec = Classifier.Classify("list all product categories", Rows(
            [("ProductCategoryKey", (object?)1), ("ProductCategoryName", "Audio")],
            [("ProductCategoryKey", 2), ("ProductCategoryName", "Cameras")]));
        Assert.AreEqual(ChartType.Table, rec.Type);
    }

    [TestMethod]
    public void CompositionIntentQuery_MagnitudePlusPercent_IsPie()
    {
        // The query explicitly asks for a percentage/share -> honor it as a Pie (the documented
        // example: "What percentage of sales come from each channel?" -> Pie chart), even though the
        // SQL also returned a pre-computed percentage column alongside the amount.
        var rec = Classifier.Classify("what percentage of sales came from each channel", Rows(
            [("Channel", "Store"), ("TotalSales", 100.0), ("SalesPercentage", 50.0)],
            [("Channel", "Online"), ("TotalSales", 60.0), ("SalesPercentage", 30.0)],
            [("Channel", "Reseller"), ("TotalSales", 40.0), ("SalesPercentage", 20.0)]));
        Assert.AreEqual(ChartType.Pie, rec.Type);
        Assert.AreEqual(1, rec.Series.Length);
        CollectionAssert.AreEqual(new[] { 100.0, 60.0, 40.0 }, rec.Series[0].Values); // raw magnitude, not the % column
    }

    [TestMethod]
    public void PlainQuery_MagnitudePlusIncidentalPercent_IsSingleMeasureBar()
    {
        // No share/percentage/breakdown wording in the query -> the percentage column is incidental;
        // plot the magnitude alone as a single-measure bar rather than grouping amount vs. percent,
        // and don't force an unrequested Pie.
        var rec = Classifier.Classify("sales by channel", Rows(
            [("Channel", "Store"), ("TotalSales", 100.0), ("SalesPercentage", 50.0)],
            [("Channel", "Online"), ("TotalSales", 60.0), ("SalesPercentage", 30.0)],
            [("Channel", "Reseller"), ("TotalSales", 40.0), ("SalesPercentage", 20.0)]));
        Assert.AreEqual(ChartType.Bar, rec.Type);
        Assert.AreEqual(1, rec.Series.Length);
    }

    [TestMethod]
    public void CompositionIntentQuery_SingleMeasure_IsPie()
    {
        // Composition wording + a single measure (no separate amount column) -> still Pie.
        var rec = Classifier.Classify("share of sales by channel", Rows(
            [("Channel", "Store"), ("SalesPercentage", 50.0)],
            [("Channel", "Online"), ("SalesPercentage", 30.0)],
            [("Channel", "Reseller"), ("SalesPercentage", 20.0)]));
        Assert.AreEqual(ChartType.Pie, rec.Type);
    }

    [TestMethod]
    public void CompositionIntentQuery_TooManySlices_FallsBackToBar()
    {
        // Composition wording, but more categories than a pie can readably show (> MaxPieSlices) ->
        // fall back to the bar/table path instead of forcing an unreadable pie.
        var data = Rows(Enumerable.Range(1, 8)
            .Select(i => new (string, object?)[] { ("Channel", $"Channel{i}"), ("SalesPercentage", 12.5) })
            .ToArray());
        var rec = Classifier.Classify("percentage of sales by channel", data);
        Assert.AreNotEqual(ChartType.Pie, rec.Type);
    }
}
