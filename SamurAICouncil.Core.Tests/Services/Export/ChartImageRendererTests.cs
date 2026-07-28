using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Core.Services.Export;
using SkiaSharp;

namespace SamurAICouncil.Core.Tests.Services.Export;

[TestClass]
public class ChartImageRendererTests
{
    private const int DefaultWidth = 900;
    private const int DefaultHeight = 540;

    private IChartImageRenderer _renderer = null!;

    [TestInitialize]
    public void Setup()
    {
        _renderer = new ChartImageRenderer();
    }

    // ---- None / Stat / Table are not rasterized ---------------------------------------------------

    [TestMethod]
    public void RenderToPng_WithNoneType_ReturnsNull()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.None,
            Labels = ["A", "B"],
            Series = [new ChartSeriesData { Name = "S", Values = [1, 2] }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RenderToPng_WithStatType_ReturnsNull()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Stat,
            Stats = [new StatData { Label = "Total", Value = 100 }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RenderToPng_WithTableType_ReturnsNull()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Table,
            Table = new TableData { Columns = ["A"], Rows = [["1"]] }
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNull(result);
    }

    // ---- Defensive / edge cases ---------------------------------------------------------------------

    [TestMethod]
    public void RenderToPng_WithNullChart_ReturnsNull()
    {
        var result = _renderer.RenderToPng(null!);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RenderToPng_WithEmptySeries_ReturnsNull()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Labels = ["A", "B"],
            Series = []
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RenderToPng_WithSeriesContainingNoValues_ReturnsNull()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Labels = [],
            Series = [new ChartSeriesData { Name = "Empty", Values = [] }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RenderToPng_WithEmptyLabelsButValidValues_DoesNotThrowAndRenders()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Labels = [],
            Series = [new ChartSeriesData { Name = "Data", Values = [10, 20, 30] }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNotNull(result);
        using var _ = AssertValidPng(result, DefaultWidth, DefaultHeight);
    }

    [TestMethod]
    public void RenderToPng_WithZeroWidth_ReturnsNull()
    {
        var chart = SimpleBarChart();

        var result = _renderer.RenderToPng(chart, width: 0, height: DefaultHeight);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RenderToPng_WithCustomDimensions_MatchesRequestedSize()
    {
        var chart = SimpleBarChart();

        var result = _renderer.RenderToPng(chart, width: 400, height: 260);

        Assert.IsNotNull(result);
        using var _ = AssertValidPng(result, 400, 260);
    }

    // ---- Single data point must not throw / produce NaN --------------------------------------------

    [TestMethod]
    public void RenderToPng_BarWithSingleDataPoint_DoesNotThrow()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Single Bar",
            Labels = ["Only"],
            Series = [new ChartSeriesData { Name = "Data", Values = [42] }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNotNull(result);
        using var _ = AssertValidPng(result, DefaultWidth, DefaultHeight);
    }

    [TestMethod]
    public void RenderToPng_LineWithSingleDataPoint_DoesNotThrow()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Line,
            Title = "Single Line Point",
            Labels = ["Jan"],
            Series = [new ChartSeriesData { Name = "Data", Values = [42] }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNotNull(result);
        using var _ = AssertValidPng(result, DefaultWidth, DefaultHeight);
    }

    [TestMethod]
    public void RenderToPng_PieWithSingleSegment_DoesNotThrow()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Pie,
            Title = "Single Slice",
            Labels = ["All"],
            Series = [new ChartSeriesData { Name = "Data", Values = [100] }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNotNull(result);
        using var _ = AssertValidPng(result, DefaultWidth, DefaultHeight);
    }

    [TestMethod]
    public void RenderToPng_ScatterWithSinglePoint_DoesNotThrow()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Scatter,
            Title = "Single Point",
            Series = [new ChartSeriesData { Name = "Data", Points = [new ChartPoint { X = 5, Y = 5 }] }]
        };

        var result = _renderer.RenderToPng(chart);

        Assert.IsNotNull(result);
        using var _ = AssertValidPng(result, DefaultWidth, DefaultHeight);
    }

    // ---- One rasterization test per plottable chart type --------------------------------------------

    [TestMethod]
    public void RenderToPng_Bar_ProducesValidPngWithVisibleMarks()
    {
        AssertRendersWithVisibleMarks(SimpleBarChart());
    }

    [TestMethod]
    public void RenderToPng_HorizontalBar_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.HorizontalBar,
            Title = "Sales by Region",
            Labels = ["North America", "Europe", "Asia Pacific"],
            Series = [new ChartSeriesData { Name = "Sales", Values = [1200, 950, 700] }]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_GroupedBar_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.GroupedBar,
            Title = "Quarterly Comparison",
            Labels = ["Q1", "Q2"],
            Series =
            [
                new ChartSeriesData { Name = "2023", Values = [10, 20] },
                new ChartSeriesData { Name = "2024", Values = [15, 25] }
            ]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_StackedBar_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.StackedBar,
            Title = "Revenue by Channel",
            Labels = ["Jan", "Feb"],
            Series =
            [
                new ChartSeriesData { Name = "Online", Values = [10, 20] },
                new ChartSeriesData { Name = "Retail", Values = [5, 15] }
            ]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_Line_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Line,
            Title = "Monthly Trend",
            Labels = ["Jan", "Feb", "Mar", "Apr"],
            Series = [new ChartSeriesData { Name = "Revenue", Values = [10, 30, 20, 40] }]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_Area_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Area,
            Title = "Monthly Trend (Area)",
            Labels = ["Jan", "Feb", "Mar", "Apr"],
            Series = [new ChartSeriesData { Name = "Revenue", Values = [10, 30, 20, 40] }]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_Pie_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Pie,
            Title = "Market Share",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Share", Values = [50, 30, 20] }]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_Donut_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Donut,
            Title = "Budget Allocation",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Budget", Values = [50, 30, 20] }]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_Scatter_ProducesValidPngWithVisibleMarks()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Scatter,
            Title = "Price vs Demand",
            Series =
            [
                new ChartSeriesData
                {
                    Name = "Series 1",
                    Points =
                    [
                        new ChartPoint { X = 1, Y = 5 },
                        new ChartPoint { X = 2, Y = 8 },
                        new ChartPoint { X = 3, Y = 3 }
                    ]
                }
            ]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_PieWithMoreThanEightSegments_FoldsExtrasIntoOtherAndDoesNotThrow()
    {
        var labels = Enumerable.Range(1, 12).Select(i => $"Segment {i}").ToArray();
        var values = Enumerable.Range(1, 12).Select(i => (double)i).ToArray();
        var chart = new ChartRecommendation
        {
            Type = ChartType.Pie,
            Title = "Many Segments",
            Labels = labels,
            Series = [new ChartSeriesData { Name = "Data", Values = values }]
        };

        AssertRendersWithVisibleMarks(chart);
    }

    [TestMethod]
    public void RenderToPng_GroupedBarWithMoreThanEightSeries_FoldsExtrasIntoOtherAndDoesNotThrow()
    {
        var series = Enumerable.Range(1, 10)
            .Select(i => new ChartSeriesData { Name = $"Series {i}", Values = [i, i * 2] })
            .ToArray();
        var chart = new ChartRecommendation
        {
            Type = ChartType.GroupedBar,
            Title = "Many Series",
            Labels = ["Q1", "Q2"],
            Series = series
        };

        AssertRendersWithVisibleMarks(chart);
    }

    // ---- Helpers -------------------------------------------------------------------------------------

    private static ChartRecommendation SimpleBarChart() => new()
    {
        Type = ChartType.Bar,
        Title = "Top Products by Revenue",
        Labels = ["Widget A", "Widget B", "Widget C"],
        Series = [new ChartSeriesData { Name = "Revenue", Values = [30, 45, 20] }]
    };

    private void AssertRendersWithVisibleMarks(ChartRecommendation chart)
    {
        var result = _renderer.RenderToPng(chart);

        Assert.IsNotNull(result, "Expected a non-null PNG for a plottable chart.");
        Assert.IsTrue(result.Length > 0, "Expected non-empty PNG bytes.");

        using var bitmap = AssertValidPng(result, DefaultWidth, DefaultHeight);

        // The generous inner region below sits safely inside the plot area (title/bottom/left
        // margins per the class's documented layout), regardless of the chart type's exact mark
        // geometry — proving *something* was drawn without pinning pixel-exact coordinates.
        var innerRegion = new SKRectI(80, 70, DefaultWidth - 50, DefaultHeight - 60);
        Assert.IsTrue(
            RegionHasNonBackgroundPixel(bitmap, innerRegion),
            $"Expected at least one non-background pixel in the plot area for chart type {chart.Type}.");
    }

    private static SKBitmap AssertValidPng(byte[] pngBytes, int expectedWidth, int expectedHeight)
    {
        using var bitmap = SKBitmap.Decode(pngBytes);

        Assert.IsNotNull(bitmap, "PNG bytes should decode to a valid bitmap.");
        Assert.AreEqual(expectedWidth, bitmap.Width);
        Assert.AreEqual(expectedHeight, bitmap.Height);

        // Return a copy so the caller can inspect pixels after the `using` disposes this instance.
        return bitmap.Copy();
    }

    private static readonly SKColor SurfaceColor = SKColor.Parse("#" + ReportPalette.ChartSurfaceHex);

    private static bool RegionHasNonBackgroundPixel(SKBitmap bitmap, SKRectI region)
    {
        var right = Math.Min(region.Right, bitmap.Width);
        var bottom = Math.Min(region.Bottom, bitmap.Height);

        for (var y = Math.Max(region.Top, 0); y < bottom; y += 2)
        {
            for (var x = Math.Max(region.Left, 0); x < right; x += 2)
            {
                if (bitmap.GetPixel(x, y) != SurfaceColor)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
