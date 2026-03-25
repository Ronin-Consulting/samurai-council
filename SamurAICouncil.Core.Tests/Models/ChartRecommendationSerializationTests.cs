using System.Text.Json;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Tests.Models;

[TestClass]
public class ChartRecommendationSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #region ChartRecommendation Serialization

    [TestMethod]
    public void ChartRecommendation_SerializesToJson_WithCorrectPropertyNames()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test Chart",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Data", Values = [1, 2, 3] }],
            XAxisLabel = "X Axis",
            YAxisLabel = "Y Axis"
        };

        var json = JsonSerializer.Serialize(chart);

        Assert.IsTrue(json.Contains("\"type\""));
        Assert.IsTrue(json.Contains("\"title\""));
        Assert.IsTrue(json.Contains("\"labels\""));
        Assert.IsTrue(json.Contains("\"series\""));
        Assert.IsTrue(json.Contains("\"xAxisLabel\""));
        Assert.IsTrue(json.Contains("\"yAxisLabel\""));
    }

    [TestMethod]
    public void ChartRecommendation_SerializesChartType_AsString()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = [],
            Series = []
        };

        var json = JsonSerializer.Serialize(chart);

        Assert.IsTrue(json.Contains("\"Bar\""));
        Assert.IsFalse(json.Contains(":0")); // Should not be numeric
    }

    [TestMethod]
    public void ChartRecommendation_RoundTrips_AllChartTypes()
    {
        var chartTypes = new[] { ChartType.None, ChartType.Bar, ChartType.Line, ChartType.Pie, ChartType.Donut };

        foreach (var chartType in chartTypes)
        {
            var original = new ChartRecommendation
            {
                Type = chartType,
                Title = $"Test {chartType}",
                Labels = ["Label"],
                Series = [new ChartSeriesData { Name = "Series", Values = [1.0] }]
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<ChartRecommendation>(json, Options);

            Assert.IsNotNull(deserialized, $"Failed to deserialize {chartType}");
            Assert.AreEqual(chartType, deserialized.Type, $"ChartType mismatch for {chartType}");
        }
    }

    [TestMethod]
    public void ChartRecommendation_RoundTrips_CompleteObject()
    {
        var original = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Sales by Region",
            Labels = ["North", "South", "East", "West"],
            Series =
            [
                new ChartSeriesData { Name = "2023", Values = [100, 150, 200, 120] },
                new ChartSeriesData { Name = "2024", Values = [110, 160, 220, 140] }
            ],
            XAxisLabel = "Region",
            YAxisLabel = "Sales ($)"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ChartRecommendation>(json, Options);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Type, deserialized.Type);
        Assert.AreEqual(original.Title, deserialized.Title);
        Assert.AreEqual(original.XAxisLabel, deserialized.XAxisLabel);
        Assert.AreEqual(original.YAxisLabel, deserialized.YAxisLabel);
        Assert.AreEqual(original.Labels.Length, deserialized.Labels.Length);
        Assert.AreEqual(original.Series.Length, deserialized.Series.Length);

        for (int i = 0; i < original.Labels.Length; i++)
        {
            Assert.AreEqual(original.Labels[i], deserialized.Labels[i]);
        }

        for (int i = 0; i < original.Series.Length; i++)
        {
            Assert.AreEqual(original.Series[i].Name, deserialized.Series[i].Name);
            Assert.AreEqual(original.Series[i].Values.Length, deserialized.Series[i].Values.Length);
        }
    }

    [TestMethod]
    public void ChartRecommendation_DeserializesFromLlmJson()
    {
        // Simulates JSON that an LLM might produce
        var llmJson = """
            {
                "type": "Bar",
                "title": "Top 5 Products",
                "labels": ["Product A", "Product B", "Product C", "Product D", "Product E"],
                "series": [
                    { "name": "Revenue", "values": [50000, 42000, 38000, 35000, 30000] }
                ],
                "xAxisLabel": "Product",
                "yAxisLabel": "Revenue ($)"
            }
            """;

        var chart = JsonSerializer.Deserialize<ChartRecommendation>(llmJson, Options);

        Assert.IsNotNull(chart);
        Assert.AreEqual(ChartType.Bar, chart.Type);
        Assert.AreEqual("Top 5 Products", chart.Title);
        Assert.AreEqual(5, chart.Labels.Length);
        Assert.AreEqual(1, chart.Series.Length);
        Assert.AreEqual(50000, chart.Series[0].Values[0]);
    }

    [TestMethod]
    public void ChartRecommendation_DeserializesWithCaseInsensitiveType()
    {
        var json = """{ "type": "bar", "title": "Test", "labels": [], "series": [] }""";

        var chart = JsonSerializer.Deserialize<ChartRecommendation>(json, Options);

        Assert.IsNotNull(chart);
        Assert.AreEqual(ChartType.Bar, chart.Type);
    }

    [TestMethod]
    public void ChartRecommendation_DeserializesWithMixedCaseType()
    {
        var json = """{ "type": "LINE", "title": "Test", "labels": [], "series": [] }""";

        var chart = JsonSerializer.Deserialize<ChartRecommendation>(json, Options);

        Assert.IsNotNull(chart);
        Assert.AreEqual(ChartType.Line, chart.Type);
    }

    [TestMethod]
    public void ChartRecommendation_DeserializesNullOptionalFields()
    {
        var json = """{ "type": "Pie", "title": "Test", "labels": ["A"], "series": [{"name": "S", "values": [1]}] }""";

        var chart = JsonSerializer.Deserialize<ChartRecommendation>(json, Options);

        Assert.IsNotNull(chart);
        Assert.IsNull(chart.XAxisLabel);
        Assert.IsNull(chart.YAxisLabel);
    }

    #endregion

    #region ChartSeriesData Serialization

    [TestMethod]
    public void ChartSeriesData_SerializesToJson_WithCorrectPropertyNames()
    {
        var series = new ChartSeriesData
        {
            Name = "Test Series",
            Values = [10.5, 20.0, 30.75]
        };

        var json = JsonSerializer.Serialize(series);

        Assert.IsTrue(json.Contains("\"name\""));
        Assert.IsTrue(json.Contains("\"values\""));
        Assert.IsTrue(json.Contains("\"Test Series\""));
    }

    [TestMethod]
    public void ChartSeriesData_RoundTrips_DecimalValues()
    {
        var original = new ChartSeriesData
        {
            Name = "Decimal Series",
            Values = [10.123, 20.456, 30.789]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ChartSeriesData>(json, Options);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.Name, deserialized.Name);
        Assert.AreEqual(original.Values.Length, deserialized.Values.Length);

        for (int i = 0; i < original.Values.Length; i++)
        {
            Assert.AreEqual(original.Values[i], deserialized.Values[i], 0.001);
        }
    }

    [TestMethod]
    public void ChartSeriesData_RoundTrips_EmptyValues()
    {
        var original = new ChartSeriesData
        {
            Name = "Empty Series",
            Values = []
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ChartSeriesData>(json, Options);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(0, deserialized.Values.Length);
    }

    [TestMethod]
    public void ChartSeriesData_RoundTrips_LargeValues()
    {
        var original = new ChartSeriesData
        {
            Name = "Large Values",
            Values = [1000000000.0, 2500000000.0, -500000000.0]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ChartSeriesData>(json, Options);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(1000000000.0, deserialized.Values[0], 0.001);
        Assert.AreEqual(2500000000.0, deserialized.Values[1], 0.001);
        Assert.AreEqual(-500000000.0, deserialized.Values[2], 0.001);
    }

    #endregion

    #region ChartType Enum Serialization

    [TestMethod]
    public void ChartType_SerializesAsString_NotInteger()
    {
        var json = JsonSerializer.Serialize(ChartType.Bar);

        Assert.AreEqual("\"Bar\"", json);
    }

    [TestMethod]
    [DataRow(ChartType.None, "None")]
    [DataRow(ChartType.Bar, "Bar")]
    [DataRow(ChartType.Line, "Line")]
    [DataRow(ChartType.Pie, "Pie")]
    [DataRow(ChartType.Donut, "Donut")]
    public void ChartType_SerializesCorrectString(ChartType type, string expected)
    {
        var json = JsonSerializer.Serialize(type);

        Assert.AreEqual($"\"{expected}\"", json);
    }

    [TestMethod]
    [DataRow("\"None\"", ChartType.None)]
    [DataRow("\"Bar\"", ChartType.Bar)]
    [DataRow("\"Line\"", ChartType.Line)]
    [DataRow("\"Pie\"", ChartType.Pie)]
    [DataRow("\"Donut\"", ChartType.Donut)]
    public void ChartType_DeserializesFromString(string json, ChartType expected)
    {
        var result = JsonSerializer.Deserialize<ChartType>(json);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ChartType_DeserializesCaseInsensitive()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        Assert.AreEqual(ChartType.Bar, JsonSerializer.Deserialize<ChartType>("\"bar\"", options));
        Assert.AreEqual(ChartType.Bar, JsonSerializer.Deserialize<ChartType>("\"BAR\"", options));
        Assert.AreEqual(ChartType.Bar, JsonSerializer.Deserialize<ChartType>("\"Bar\"", options));
    }

    #endregion

    #region ToolUsage Chart Property Serialization

    [TestMethod]
    public void ToolUsage_SerializesWithChart()
    {
        var toolUsage = new ToolUsage
        {
            ToolName = "query_company_data",
            Input = "SELECT * FROM Products",
            Output = "[{\"name\": \"Product A\"}]",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Bar,
                Title = "Products",
                Labels = ["A", "B"],
                Series = [new ChartSeriesData { Name = "Count", Values = [10, 20] }]
            }
        };

        var json = JsonSerializer.Serialize(toolUsage);

        // ToolUsage uses PascalCase, ChartRecommendation uses camelCase via JsonPropertyName
        Assert.IsTrue(json.Contains("\"Chart\""));
        Assert.IsTrue(json.Contains("\"type\":\"Bar\""));
        Assert.IsTrue(json.Contains("\"Products\""));
    }

    [TestMethod]
    public void ToolUsage_SerializesWithNullChart()
    {
        var toolUsage = new ToolUsage
        {
            ToolName = "query_company_data",
            Input = "SELECT 1",
            Output = "1",
            Chart = null
        };

        var json = JsonSerializer.Serialize(toolUsage);
        var deserialized = JsonSerializer.Deserialize<ToolUsage>(json, Options);

        Assert.IsNotNull(deserialized);
        Assert.IsNull(deserialized.Chart);
    }

    [TestMethod]
    public void ToolUsage_RoundTripsWithChart()
    {
        var original = new ToolUsage
        {
            ToolName = "query_company_data",
            Input = "SELECT product, revenue FROM sales",
            Output = "[{\"product\": \"A\", \"revenue\": 100}]",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Pie,
                Title = "Revenue Share",
                Labels = ["Product A", "Product B"],
                Series = [new ChartSeriesData { Name = "Revenue", Values = [60, 40] }]
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ToolUsage>(json, Options);

        Assert.IsNotNull(deserialized);
        Assert.IsNotNull(deserialized.Chart);
        Assert.AreEqual(ChartType.Pie, deserialized.Chart.Type);
        Assert.AreEqual("Revenue Share", deserialized.Chart.Title);
        Assert.AreEqual(2, deserialized.Chart.Labels.Length);
    }

    #endregion

    #region Stage3Response Chart Property Serialization

    [TestMethod]
    public void Stage3Response_SerializesWithChart()
    {
        var response = new Stage3Response
        {
            Model = "test-model",
            Response = "Here is the analysis...",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Line,
                Title = "Monthly Trends",
                Labels = ["Jan", "Feb", "Mar"],
                Series = [new ChartSeriesData { Name = "Revenue", Values = [100, 150, 200] }]
            }
        };

        var json = JsonSerializer.Serialize(response);

        Assert.IsTrue(json.Contains("\"chart\""));
        Assert.IsTrue(json.Contains("\"Line\""));
        Assert.IsTrue(json.Contains("\"Monthly Trends\""));
    }

    [TestMethod]
    public void Stage3Response_RoundTripsWithChart()
    {
        var original = new Stage3Response
        {
            Model = "anthropic/claude-3",
            Response = "Based on the data analysis...",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Bar,
                Title = "Top Products",
                Labels = ["P1", "P2", "P3", "P4", "P5"],
                Series = [new ChartSeriesData { Name = "Sales", Values = [500, 400, 350, 300, 250] }],
                XAxisLabel = "Product",
                YAxisLabel = "Units Sold"
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Stage3Response>(json, Options);

        Assert.IsNotNull(deserialized);
        Assert.IsNotNull(deserialized.Chart);
        Assert.AreEqual(ChartType.Bar, deserialized.Chart.Type);
        Assert.AreEqual("Top Products", deserialized.Chart.Title);
        Assert.AreEqual(5, deserialized.Chart.Labels.Length);
        Assert.AreEqual("Product", deserialized.Chart.XAxisLabel);
        Assert.AreEqual("Units Sold", deserialized.Chart.YAxisLabel);
    }

    #endregion
}
