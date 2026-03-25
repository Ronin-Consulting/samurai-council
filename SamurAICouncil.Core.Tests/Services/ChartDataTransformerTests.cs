using Microsoft.Extensions.Logging;
using Moq;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

[TestClass]
public class ChartDataTransformerTests
{
    private ChartDataTransformer _transformer = null!;
    private Mock<ILogger<ChartDataTransformer>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<ChartDataTransformer>>();
        _transformer = new ChartDataTransformer(_loggerMock.Object);
    }

    #region ParseChartRecommendation Tests

    [TestMethod]
    public void ParseChartRecommendation_WithNull_ReturnsNull()
    {
        var result = _transformer.ParseChartRecommendation(null!);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithEmptyString_ReturnsNull()
    {
        var result = _transformer.ParseChartRecommendation("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithWhitespace_ReturnsNull()
    {
        var result = _transformer.ParseChartRecommendation("   \n\t   ");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithValidDirectJson_ReturnsChart()
    {
        var json = """
            {
                "type": "Bar",
                "title": "Sales by Region",
                "labels": ["North", "South", "East", "West"],
                "series": [
                    { "name": "2024 Sales", "values": [1200, 980, 1500, 1100] }
                ],
                "xAxisLabel": "Region",
                "yAxisLabel": "Sales ($)"
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(ChartType.Bar, result.Type);
        Assert.AreEqual("Sales by Region", result.Title);
        Assert.AreEqual(4, result.Labels.Length);
        Assert.AreEqual("North", result.Labels[0]);
        Assert.AreEqual(1, result.Series.Length);
        Assert.AreEqual("2024 Sales", result.Series[0].Name);
        Assert.AreEqual(4, result.Series[0].Values.Length);
        Assert.AreEqual(1200, result.Series[0].Values[0]);
        Assert.AreEqual("Region", result.XAxisLabel);
        Assert.AreEqual("Sales ($)", result.YAxisLabel);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithWrappedJson_ReturnsChart()
    {
        var json = """
            {
                "chart": {
                    "type": "Line",
                    "title": "Monthly Trends",
                    "labels": ["Jan", "Feb", "Mar"],
                    "series": [
                        { "name": "Revenue", "values": [100, 150, 200] }
                    ]
                }
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(ChartType.Line, result.Type);
        Assert.AreEqual("Monthly Trends", result.Title);
        Assert.AreEqual(3, result.Labels.Length);
        Assert.AreEqual(1, result.Series.Length);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithMarkdownCodeBlock_ExtractsJson()
    {
        var json = """
            Here is the chart recommendation:

            ```json
            {
                "type": "Pie",
                "title": "Market Share",
                "labels": ["Product A", "Product B", "Product C"],
                "series": [
                    { "name": "Share", "values": [45, 35, 20] }
                ]
            }
            ```

            This shows the market distribution.
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(ChartType.Pie, result.Type);
        Assert.AreEqual("Market Share", result.Title);
        Assert.AreEqual(3, result.Labels.Length);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithCodeBlockNoLanguage_ExtractsJson()
    {
        var json = """
            ```
            {
                "type": "Donut",
                "title": "Budget Allocation",
                "labels": ["Marketing", "R&D"],
                "series": [{ "name": "Budget", "values": [60, 40] }]
            }
            ```
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(ChartType.Donut, result.Type);
        Assert.AreEqual("Budget Allocation", result.Title);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithJsonInText_ExtractsJson()
    {
        var json = """
            Based on my analysis, here is the recommended chart:
            {"type": "Bar", "title": "Test", "labels": ["A"], "series": [{"name": "S1", "values": [10]}]}
            Hope this helps!
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(ChartType.Bar, result.Type);
        Assert.AreEqual("Test", result.Title);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithInvalidJson_ReturnsNull()
    {
        var json = "{ this is not valid json }";

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithMalformedJson_ReturnsNull()
    {
        var json = """
            {
                "type": "Bar",
                "title": "Missing closing brace"
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithNoneType_ReturnsChartWithNoneType()
    {
        var json = """
            {
                "type": "None",
                "title": "",
                "labels": [],
                "series": []
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(ChartType.None, result.Type);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithCaseInsensitiveType_ParsesCorrectly()
    {
        var json = """
            {
                "type": "bar",
                "title": "Lowercase Type",
                "labels": ["X"],
                "series": [{ "name": "S", "values": [1] }]
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(ChartType.Bar, result.Type);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithMultipleSeries_ParsesAll()
    {
        var json = """
            {
                "type": "Bar",
                "title": "Comparison",
                "labels": ["Q1", "Q2", "Q3", "Q4"],
                "series": [
                    { "name": "2023", "values": [100, 120, 130, 150] },
                    { "name": "2024", "values": [110, 140, 160, 180] }
                ]
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Series.Length);
        Assert.AreEqual("2023", result.Series[0].Name);
        Assert.AreEqual("2024", result.Series[1].Name);
        Assert.AreEqual(4, result.Series[0].Values.Length);
        Assert.AreEqual(4, result.Series[1].Values.Length);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithTooManyDataPoints_TruncatesToMax()
    {
        // Create data with 25 points (max for Line charts is 20)
        var labels = string.Join(", ", Enumerable.Range(1, 25).Select(i => $"\"Item{i}\""));
        var values = string.Join(", ", Enumerable.Range(1, 25).Select(i => i * 10));

        var json = $@"{{
            ""type"": ""Line"",
            ""title"": ""Large Dataset"",
            ""labels"": [{labels}],
            ""series"": [{{ ""name"": ""Data"", ""values"": [{values}] }}]
        }}";

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(20, result.Labels.Length);
        Assert.AreEqual(20, result.Series[0].Values.Length);
        Assert.AreEqual("Item1", result.Labels[0]);
        Assert.AreEqual("Item20", result.Labels[19]);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithExactlyMaxDataPoints_DoesNotTruncate()
    {
        // Create data with exactly 20 points (max for Line charts)
        var labels = string.Join(", ", Enumerable.Range(1, 20).Select(i => $"\"Item{i}\""));
        var values = string.Join(", ", Enumerable.Range(1, 20).Select(i => i * 10));

        var json = $@"{{
            ""type"": ""Line"",
            ""title"": ""Max Dataset"",
            ""labels"": [{labels}],
            ""series"": [{{ ""name"": ""Data"", ""values"": [{values}] }}]
        }}";

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(20, result.Labels.Length);
        Assert.AreEqual(20, result.Series[0].Values.Length);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithMissingOptionalFields_UsesDefaults()
    {
        var json = """
            {
                "type": "Pie",
                "title": "Simple Chart",
                "labels": ["A", "B"],
                "series": [{ "name": "Data", "values": [50, 50] }]
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.IsNull(result.XAxisLabel);
        Assert.IsNull(result.YAxisLabel);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithDecimalValues_ParsesCorrectly()
    {
        var json = """
            {
                "type": "Line",
                "title": "Decimal Data",
                "labels": ["A", "B", "C"],
                "series": [{ "name": "Values", "values": [10.5, 20.75, 30.125] }]
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(10.5, result.Series[0].Values[0], 0.001);
        Assert.AreEqual(20.75, result.Series[0].Values[1], 0.001);
        Assert.AreEqual(30.125, result.Series[0].Values[2], 0.001);
    }

    #endregion

    #region IsValidChart Tests

    [TestMethod]
    public void IsValidChart_WithNull_ReturnsFalse()
    {
        var result = _transformer.IsValidChart(null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithNoneType_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.None,
            Title = "Test",
            Labels = ["A"],
            Series = [new ChartSeriesData { Name = "S", Values = [1] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithEmptySeries_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A", "B"],
            Series = []
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithSeriesWithNoValues_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A", "B"],
            Series = [new ChartSeriesData { Name = "Empty", Values = [] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithValidBarChart_ReturnsTrue()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Valid Bar Chart",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Data", Values = [10, 20, 30] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithValidLineChart_ReturnsTrue()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Line,
            Title = "Valid Line Chart",
            Labels = ["Jan", "Feb", "Mar"],
            Series = [new ChartSeriesData { Name = "Revenue", Values = [100, 150, 200] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithValidPieChart_ReturnsTrue()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Pie,
            Title = "Valid Pie Chart",
            Labels = ["Segment A", "Segment B"],
            Series = [new ChartSeriesData { Name = "Share", Values = [60, 40] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithValidDonutChart_ReturnsTrue()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Donut,
            Title = "Valid Donut Chart",
            Labels = ["Part 1", "Part 2", "Part 3"],
            Series = [new ChartSeriesData { Name = "Distribution", Values = [33, 33, 34] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithMismatchedLabelsAndValues_StillReturnsTrue()
    {
        // Labels don't match value count, but chart is still valid (just might look odd)
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Mismatched Chart",
            Labels = ["A", "B"],  // 2 labels
            Series = [new ChartSeriesData { Name = "Data", Values = [10, 20, 30, 40] }]  // 4 values
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithEmptyLabels_ReturnsTrue()
    {
        // Empty labels are allowed (might use indices instead)
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "No Labels Chart",
            Labels = [],
            Series = [new ChartSeriesData { Name = "Data", Values = [10, 20, 30] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithMultipleSeries_ReturnsTrue()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Multi-Series Chart",
            Labels = ["Q1", "Q2", "Q3"],
            Series =
            [
                new ChartSeriesData { Name = "2023", Values = [100, 120, 140] },
                new ChartSeriesData { Name = "2024", Values = [110, 130, 160] }
            ]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithOneEmptySeriesAmongMultiple_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Mixed Series Chart",
            Labels = ["Q1", "Q2"],
            Series =
            [
                new ChartSeriesData { Name = "Valid", Values = [100, 120] },
                new ChartSeriesData { Name = "Empty", Values = [] }
            ]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithSingleDataPoint_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Single Point Chart",
            Labels = ["Only One"],
            Series = [new ChartSeriesData { Name = "Data", Values = [100] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithAllZeroValues_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Zero Data Chart",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Data", Values = [0, 0, 0] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithAllZeroValuesMultipleSeries_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "All Zero Multi-Series",
            Labels = ["A", "B"],
            Series =
            [
                new ChartSeriesData { Name = "S1", Values = [0, 0] },
                new ChartSeriesData { Name = "S2", Values = [0, 0] }
            ]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithSomeZeroValues_ReturnsTrue()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Some Zero Values",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Data", Values = [0, 50, 100] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithPieChartNegativeValues_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Pie,
            Title = "Negative Pie Chart",
            Labels = ["Positive", "Negative"],
            Series = [new ChartSeriesData { Name = "Values", Values = [50, -10] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithDonutChartNegativeValues_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Donut,
            Title = "Negative Donut Chart",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Values", Values = [30, -20, 50] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidChart_WithBarChartNegativeValues_ReturnsTrue()
    {
        // Bar charts CAN have negative values (for showing losses, etc.)
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Profit/Loss Chart",
            Labels = ["Q1", "Q2", "Q3"],
            Series = [new ChartSeriesData { Name = "Profit", Values = [100, -50, 75] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidChart_WithLineChartNegativeValues_ReturnsTrue()
    {
        // Line charts CAN have negative values
        var chart = new ChartRecommendation
        {
            Type = ChartType.Line,
            Title = "Temperature Trend",
            Labels = ["Mon", "Tue", "Wed"],
            Series = [new ChartSeriesData { Name = "Temp", Values = [-5, 0, 10] }]
        };

        var result = _transformer.IsValidChart(chart);

        Assert.IsTrue(result);
    }

    #endregion

    #region Missing Label Generation Tests

    [TestMethod]
    public void ParseChartRecommendation_WithMissingLabels_GeneratesNumberedLabels()
    {
        var json = """
            {
                "type": "Bar",
                "title": "No Labels",
                "labels": [],
                "series": [{ "name": "Data", "values": [10, 20, 30] }]
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Labels.Length);
        Assert.AreEqual("Item 1", result.Labels[0]);
        Assert.AreEqual("Item 2", result.Labels[1]);
        Assert.AreEqual("Item 3", result.Labels[2]);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithLabelsPresent_DoesNotGenerateLabels()
    {
        var json = """
            {
                "type": "Bar",
                "title": "Has Labels",
                "labels": ["A", "B", "C"],
                "series": [{ "name": "Data", "values": [10, 20, 30] }]
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Labels.Length);
        Assert.AreEqual("A", result.Labels[0]);
        Assert.AreEqual("B", result.Labels[1]);
        Assert.AreEqual("C", result.Labels[2]);
    }

    #endregion

    #region TruncateLabel Tests

    [TestMethod]
    public void TruncateLabel_WithShortLabel_ReturnsUnchanged()
    {
        var result = ChartDataTransformer.TruncateLabel("Short");

        Assert.AreEqual("Short", result);
    }

    [TestMethod]
    public void TruncateLabel_WithExactMaxLength_ReturnsUnchanged()
    {
        var label = new string('X', 15);  // MaxLabelLength is 15

        var result = ChartDataTransformer.TruncateLabel(label);

        Assert.AreEqual(15, result.Length);
        Assert.IsFalse(result.Contains('…'));
    }

    [TestMethod]
    public void TruncateLabel_WithLongLabel_TruncatesWithEllipsis()
    {
        var label = "This is a very long label that should be truncated";

        var result = ChartDataTransformer.TruncateLabel(label);

        Assert.IsTrue(result.Length <= 15);
        Assert.IsTrue(result.EndsWith("…"));
    }

    [TestMethod]
    public void TruncateLabel_TruncatesAtWordBoundary()
    {
        var label = "Contoso North Store Number 1234";

        var result = ChartDataTransformer.TruncateLabel(label);

        // Should truncate at word boundary "Contoso North…" rather than mid-word
        Assert.IsTrue(result.Length <= 15);
        Assert.IsTrue(result.EndsWith("…"));
        Assert.IsFalse(result.Contains("Stor")); // Should not have partial word
    }

    [TestMethod]
    public void TruncateLabel_WithNull_ReturnsNull()
    {
        var result = ChartDataTransformer.TruncateLabel(null!);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TruncateLabel_WithEmpty_ReturnsEmpty()
    {
        var result = ChartDataTransformer.TruncateLabel("");

        Assert.AreEqual("", result);
    }

    #endregion

    #region ShouldUseHorizontalBar Tests

    [TestMethod]
    public void ShouldUseHorizontalBar_WithNull_ReturnsFalse()
    {
        var result = _transformer.ShouldUseHorizontalBar(null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldUseHorizontalBar_WithLineChart_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Line,
            Labels = ["Very Long Label That Should Trigger Horizontal"],
            Series = [new ChartSeriesData { Name = "Data", Values = [1] }]
        };

        var result = _transformer.ShouldUseHorizontalBar(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldUseHorizontalBar_WithShortLabels_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Labels = ["USA", "UK", "DE", "FR"],
            Series = [new ChartSeriesData { Name = "Sales", Values = [100, 90, 80, 70] }]
        };

        var result = _transformer.ShouldUseHorizontalBar(chart);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldUseHorizontalBar_WithLongLabels_ReturnsTrue()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Labels = ["North America Region", "South America Region", "Western Europe", "Eastern Europe"],
            Series = [new ChartSeriesData { Name = "Sales", Values = [100, 90, 80, 70] }]
        };

        var result = _transformer.ShouldUseHorizontalBar(chart);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldUseHorizontalBar_WithEmptyLabels_ReturnsFalse()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Labels = [],
            Series = [new ChartSeriesData { Name = "Data", Values = [1, 2, 3] }]
        };

        var result = _transformer.ShouldUseHorizontalBar(chart);

        Assert.IsFalse(result);
    }

    #endregion

    #region Chart-Type Specific Truncation Tests

    [TestMethod]
    public void ParseChartRecommendation_WithTooManyPieSlices_TruncatesToSix()
    {
        var labels = string.Join(", ", Enumerable.Range(1, 10).Select(i => $"\"Slice{i}\""));
        var values = string.Join(", ", Enumerable.Range(1, 10).Select(i => i * 10));

        var json = $@"{{
            ""type"": ""Pie"",
            ""title"": ""Large Pie"",
            ""labels"": [{labels}],
            ""series"": [{{ ""name"": ""Data"", ""values"": [{values}] }}]
        }}";

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(6, result.Labels.Length);
        Assert.AreEqual(6, result.Series[0].Values.Length);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithTooManyBarCategories_TruncatesToFifteen()
    {
        var labels = string.Join(", ", Enumerable.Range(1, 20).Select(i => $"\"Cat{i}\""));
        var values = string.Join(", ", Enumerable.Range(1, 20).Select(i => i * 10));

        var json = $@"{{
            ""type"": ""Bar"",
            ""title"": ""Many Categories"",
            ""labels"": [{labels}],
            ""series"": [{{ ""name"": ""Data"", ""values"": [{values}] }}]
        }}";

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(15, result.Labels.Length);
        Assert.AreEqual(15, result.Series[0].Values.Length);
    }

    [TestMethod]
    public void ParseChartRecommendation_WithLongLabels_TruncatesLabels()
    {
        var json = """
            {
                "type": "Bar",
                "title": "Stores",
                "labels": ["Contoso North America Store #1234 Main Street", "Adventure Works Seattle Downtown Location"],
                "series": [{ "name": "Revenue", "values": [1000, 900] }]
            }
            """;

        var result = _transformer.ParseChartRecommendation(json);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Labels.All(l => l.Length <= 15));
        Assert.IsTrue(result.Labels.All(l => l.EndsWith("…")));
    }

    #endregion
}
