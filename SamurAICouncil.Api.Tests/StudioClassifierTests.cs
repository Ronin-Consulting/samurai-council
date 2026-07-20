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
        Assert.AreEqual(ChartType.Bar, rec.Type);
    }

    [TestMethod]
    public void MultiYearMonthlyTrend_CollapsesToLineWithCompositeLabel()
    {
        // A real 3-year monthly trend: Year + Month + MonthLabel are each redundant on their
        // own (no single column is unique per row — only the combination is), and at 36 rows
        // this also exceeds the old MaxLinePoints=24 cap. Must still collapse to a Line with a
        // composite "Jan 2007"-style label, not fall back to a 3-dimension Table.
        var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var data = Rows(Enumerable.Range(2007, 3)
            .SelectMany(year => Enumerable.Range(1, 12).Select(month => new (string, object?)[]
            {
                ("CalendarYear", year),
                ("CalendarMonth", month),
                ("CalendarMonthLabel", months[month - 1]),
                ("TotalSales", (double)(year * 12 + month)),
            }))
            .ToArray());
        var rec = Classifier.Classify("show monthly sales trends", data);
        Assert.AreEqual(ChartType.Line, rec.Type);
        Assert.AreEqual(36, rec.Labels.Length);
        Assert.AreEqual("Jan 2007", rec.Labels[0]);
        Assert.AreEqual("Dec 2009", rec.Labels[^1]);
    }

    [TestMethod]
    public void LongCategoryListWithLongLabels_StaysHorizontalBar()
    {
        // 25 categories with long labels exceeds MaxBarCategories (15) but fits within the
        // wider MaxHorizontalBarCategories (30) — a horizontal bar scales vertically instead of
        // squeezing x-axis labels, so it shouldn't bail to Table just because a vertical bar
        // would have.
        var data = Rows(Enumerable.Range(1, 25)
            .Select(i => new (string, object?)[] { ("ProductName", $"Contoso Premium Widget Series {i}"), ("Revenue", (double)(1000 - i)) })
            .ToArray());
        var rec = Classifier.Classify("show product revenue", data);
        Assert.AreEqual(ChartType.HorizontalBar, rec.Type);
        Assert.AreEqual(25, rec.Labels.Length);
    }

    [TestMethod]
    public void YyyymmEncodedMonthColumn_PrefersReadableCompositeLabelOverRawCode()
    {
        // ContosoRetailDW's real DimDate.CalendarMonth is YYYYMM-encoded (e.g. 200701), which is
        // technically unique per row across years on its own — but "Jan 2007" (built from the
        // non-unique Year + MonthLabel columns) is far more readable than the raw "200701" code,
        // so the composite label must be preferred even though a single unique column exists.
        var months = new[] { "Jan", "Feb", "Mar" };
        var data = Rows(Enumerable.Range(2007, 2)
            .SelectMany(year => Enumerable.Range(1, 3).Select(month => new (string, object?)[]
            {
                ("CalendarYear", year),
                ("CalendarMonth", year * 100 + month), // e.g. 200701 -- unique per row alone
                ("CalendarMonthLabel", months[month - 1]),
                ("TotalSales", (double)(year * 3 + month)),
            }))
            .ToArray());
        var rec = Classifier.Classify("show monthly sales trends", data);
        Assert.AreEqual(ChartType.Line, rec.Type);
        Assert.AreEqual("Jan 2007", rec.Labels[0]);
        Assert.AreEqual("Mar 2008", rec.Labels[^1]);
    }

    [TestMethod]
    public void AmbiguousTemporalColumns_FallBackToTable()
    {
        // Duplicate Year+Month rows mean not even the FULL COMBINATION of temporal columns is
        // unique per row, so there's no reliable way to build a composite axis — must still
        // safely fall back to Table rather than mislabeling or crashing.
        var rec = Classifier.Classify("monthly sales", Rows(
            [("CalendarYear", (object?)2009), ("CalendarMonth", 1), ("CalendarMonthLabel", "Jan"), ("TotalSales", 100.0)],
            [("CalendarYear", 2009), ("CalendarMonth", 1), ("CalendarMonthLabel", "Jan"), ("TotalSales", 120.0)],
            [("CalendarYear", 2009), ("CalendarMonth", 2), ("CalendarMonthLabel", "Feb"), ("TotalSales", 90.0)]));
        Assert.AreEqual(ChartType.Table, rec.Type);
    }

    // ---- scoring-mechanism regression tests: prove the winning candidate is chosen by score,
    // not by which branch happened to run first (the bug class the Candidate/PickBest refactor
    // is meant to eliminate) ----

    [TestMethod]
    public void OneDimOneMeasure_TooManyCategoriesForEitherBar_FallsBackToTable()
    {
        var data = Rows(Enumerable.Range(1, 35)
            .Select(i => new (string, object?)[] { ("Category", $"C{i}"), ("Sales", (double)i) })
            .ToArray());
        var rec = Classifier.Classify("sales by category", data);
        Assert.AreEqual(ChartType.Table, rec.Type);
    }

    [TestMethod]
    public void OneDimOneMeasure_PieBeatsBarEvenWithLongLabels()
    {
        // Composition wording + <=6 rows makes Pie eligible; long labels ALSO make a
        // HorizontalBar (cap 30) eligible. Pie must still win — proves score order, not
        // "whichever candidate happens to be checked last."
        var data = Rows(Enumerable.Range(1, 5)
            .Select(i => new (string, object?)[] { ("Region", $"Contoso Enterprise Region {i}"), ("SalesPercentage", 20.0) })
            .ToArray());
        var rec = Classifier.Classify("percentage of sales by region", data);
        Assert.AreEqual(ChartType.Pie, rec.Type);
    }

    [TestMethod]
    public void OneDimTwoMeasures_PercentagePair_TemporalNoComposition_IsLine()
    {
        // Duplicate "Jan" rows mean the top-level Line candidate's ChooseTimeAxis fails (a
        // single temporal column can't be its own unique axis with only 1 temporal dim), so
        // this family's own duplicate-tolerant Line branch is what's actually reached.
        var rec = Classifier.Classify("monthly sales with percentage", Rows(
            [("Month", "Jan"), ("TotalSales", 100.0), ("SalesPercentage", 50.0)],
            [("Month", "Jan"), ("TotalSales", 90.0), ("SalesPercentage", 45.0)]));
        Assert.AreEqual(ChartType.Line, rec.Type);
    }

    [TestMethod]
    public void OneDimTwoMeasures_PercentagePair_TemporalWithComposition_StillLine()
    {
        // Same shape as above, but with composition wording in the query too. Pie's hard
        // constraint requires the dimension to be non-temporal, so Pie is never actually
        // eligible here regardless of the query's wording — Line must still win.
        var rec = Classifier.Classify("percentage of monthly sales", Rows(
            [("Month", "Jan"), ("TotalSales", 100.0), ("SalesPercentage", 50.0)],
            [("Month", "Jan"), ("TotalSales", 90.0), ("SalesPercentage", 45.0)]));
        Assert.AreEqual(ChartType.Line, rec.Type);
    }

    [TestMethod]
    public void OneDimTwoMeasures_General_Temporal_IsLine()
    {
        var rec = Classifier.Classify("monthly online vs store sales", Rows(
            [("Month", "Jan"), ("Online", 10.0), ("Store", 20.0)],
            [("Month", "Jan"), ("Online", 15.0), ("Store", 22.0)]));
        Assert.AreEqual(ChartType.Line, rec.Type);
    }

    [TestMethod]
    public void OneDimTwoMeasures_General_TooManyRowsForGroupedBar_FallsBackToTable()
    {
        var data = Rows(Enumerable.Range(1, 20)
            .Select(i => new (string, object?)[] { ("Region", $"R{i}"), ("Metric1", (double)i), ("Metric2", (double)(i * 2)) })
            .ToArray());
        var rec = Classifier.Classify("region metrics", data);
        Assert.AreEqual(ChartType.Table, rec.Type);
    }

    [TestMethod]
    public void SingleRowTwoNumericColumns_PrefersStatOverScatter()
    {
        // rowCount==1, 0 dims, exactly 2 numeric columns satisfies both Stat's and Scatter's
        // hard constraints — Stat must win (a 1-point scatter plot is meaningless).
        var rec = Classifier.Classify("headline price and quantity", Rows(
            [("Price", 10.0), ("Qty", 5.0)]));
        Assert.AreEqual(ChartType.Stat, rec.Type);
    }

    [TestMethod]
    public void CompositionIntentQuery_WithTotalWording_IsDonut()
    {
        // "total" alongside composition wording -> Donut, so its center label can surface the
        // total; the documented example query ("what percentage...") has no "total" and stays Pie.
        var rec = Classifier.Classify("what percentage of total sales came from each channel", Rows(
            [("Channel", "Store"), ("Sales", 100.0)],
            [("Channel", "Online"), ("Sales", 50.0)]));
        Assert.AreEqual(ChartType.Donut, rec.Type);
    }

    [TestMethod]
    public void TemporalSingleMeasure_WithVolumeWording_IsArea()
    {
        var rec = Classifier.Classify("cumulative monthly sales", Rows(
            [("Month", "Jan"), ("Sales", 100.0)],
            [("Month", "Feb"), ("Sales", 150.0)]));
        Assert.AreEqual(ChartType.Area, rec.Type);
    }

    [TestMethod]
    public void TemporalMultiMeasure_WithVolumeWording_StaysLine()
    {
        // Volume wording alone isn't enough -- Area is only offered for a single series, since
        // overlapping area fills read worse than overlapping lines for multi-series data.
        var rec = Classifier.Classify("cumulative monthly online vs store sales", Rows(
            [("Month", "Jan"), ("Online", 10.0), ("Store", 20.0)],
            [("Month", "Feb"), ("Online", 15.0), ("Store", 22.0)]));
        Assert.AreEqual(ChartType.Line, rec.Type);
    }
}
