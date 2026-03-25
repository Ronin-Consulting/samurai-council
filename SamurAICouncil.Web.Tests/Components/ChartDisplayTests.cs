using SamurAICouncil.Core.Models;
using SamurAICouncil.Web.Components.Shared;

namespace SamurAICouncil.Web.Tests.Components;

[TestClass]
public class ChartDisplayTests : BunitTestBase
{
    #region Null/Empty State Tests

    [TestMethod]
    public void ChartDisplay_WithNullChart_RendersNothing()
    {
        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, null));

        Assert.AreEqual(string.Empty, cut.Markup.Trim());
    }

    [TestMethod]
    public void ChartDisplay_WithNoneType_RendersNothing()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.None,
            Title = "Test",
            Labels = ["A", "B"],
            Series = [new ChartSeriesData { Name = "Data", Values = [10, 20] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.AreEqual(string.Empty, cut.Markup.Trim());
    }

    [TestMethod]
    public void ChartDisplay_WithEmptySeries_RendersNothing()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A", "B"],
            Series = []
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.AreEqual(string.Empty, cut.Markup.Trim());
    }

    [TestMethod]
    public void ChartDisplay_WithEmptyValues_RendersNothing()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A", "B"],
            Series = [new ChartSeriesData { Name = "Empty", Values = [] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.AreEqual(string.Empty, cut.Markup.Trim());
    }

    #endregion

    #region Bar Chart Tests

    [TestMethod]
    public void ChartDisplay_WithBarChart_RendersMudChart()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Sales by Region",
            Labels = ["North", "South", "East", "West"],
            Series = [new ChartSeriesData { Name = "Sales", Values = [100, 150, 200, 120] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("mud-paper"));
    }

    [TestMethod]
    public void ChartDisplay_WithBarChart_ShowsTitle()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Top Products by Revenue",
            Labels = ["A", "B", "C"],
            Series = [new ChartSeriesData { Name = "Revenue", Values = [500, 400, 300] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("Top Products by Revenue"));
    }

    [TestMethod]
    public void ChartDisplay_WithBarChart_ShowsChartIcon()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test Chart",
            Labels = ["A"],
            Series = [new ChartSeriesData { Name = "Data", Values = [1] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("mud-icon"));
    }

    [TestMethod]
    public void ChartDisplay_WithBarChart_ShowsAxisLabels()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A", "B"],
            Series = [new ChartSeriesData { Name = "Data", Values = [10, 20] }],
            XAxisLabel = "Category",
            YAxisLabel = "Value"
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("X: Category"));
        Assert.IsTrue(cut.Markup.Contains("Y: Value"));
    }

    [TestMethod]
    public void ChartDisplay_WithBarChart_MultipleSeries_Renders()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Year Comparison",
            Labels = ["Q1", "Q2", "Q3", "Q4"],
            Series =
            [
                new ChartSeriesData { Name = "2023", Values = [100, 120, 140, 160] },
                new ChartSeriesData { Name = "2024", Values = [110, 130, 150, 180] }
            ]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Year Comparison"));
    }

    #endregion

    #region Line Chart Tests

    [TestMethod]
    public void ChartDisplay_WithLineChart_RendersMudChart()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Line,
            Title = "Monthly Trends",
            Labels = ["Jan", "Feb", "Mar", "Apr", "May"],
            Series = [new ChartSeriesData { Name = "Revenue", Values = [100, 120, 115, 140, 160] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Monthly Trends"));
    }

    [TestMethod]
    public void ChartDisplay_WithLineChart_ShowsAxisLabels()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Line,
            Title = "Trend",
            Labels = ["Week 1", "Week 2"],
            Series = [new ChartSeriesData { Name = "Data", Values = [50, 75] }],
            XAxisLabel = "Time",
            YAxisLabel = "Count"
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("X: Time"));
        Assert.IsTrue(cut.Markup.Contains("Y: Count"));
    }

    #endregion

    #region Pie Chart Tests

    [TestMethod]
    public void ChartDisplay_WithPieChart_RendersMudChart()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Pie,
            Title = "Market Share",
            Labels = ["Product A", "Product B", "Product C"],
            Series = [new ChartSeriesData { Name = "Share", Values = [45, 35, 20] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Market Share"));
    }

    [TestMethod]
    public void ChartDisplay_WithPieChart_NoLabels_RendersNothing()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Pie,
            Title = "Test",
            Labels = [],
            Series = [new ChartSeriesData { Name = "Data", Values = [50, 50] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.AreEqual(string.Empty, cut.Markup.Trim());
    }

    #endregion

    #region Donut Chart Tests

    [TestMethod]
    public void ChartDisplay_WithDonutChart_RendersMudChart()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Donut,
            Title = "Budget Allocation",
            Labels = ["R&D", "Marketing", "Operations", "Other"],
            Series = [new ChartSeriesData { Name = "Budget", Values = [30, 25, 35, 10] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Budget Allocation"));
    }

    #endregion

    #region Title/Label Edge Cases

    [TestMethod]
    public void ChartDisplay_WithEmptyTitle_RendersWithoutTitle()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "",
            Labels = ["A", "B"],
            Series = [new ChartSeriesData { Name = "Data", Values = [10, 20] }]
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsFalse(cut.Markup.Contains("mud-typography-subtitle1"));
    }

    [TestMethod]
    public void ChartDisplay_WithOnlyXAxisLabel_ShowsOnlyX()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A"],
            Series = [new ChartSeriesData { Name = "Data", Values = [1] }],
            XAxisLabel = "Category",
            YAxisLabel = null
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsTrue(cut.Markup.Contains("X: Category"));
        Assert.IsFalse(cut.Markup.Contains("Y:"));
    }

    [TestMethod]
    public void ChartDisplay_WithOnlyYAxisLabel_ShowsOnlyY()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A"],
            Series = [new ChartSeriesData { Name = "Data", Values = [1] }],
            XAxisLabel = null,
            YAxisLabel = "Value"
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsFalse(cut.Markup.Contains("X:"));
        Assert.IsTrue(cut.Markup.Contains("Y: Value"));
    }

    [TestMethod]
    public void ChartDisplay_WithNoAxisLabels_HidesAxisSection()
    {
        var chart = new ChartRecommendation
        {
            Type = ChartType.Bar,
            Title = "Test",
            Labels = ["A"],
            Series = [new ChartSeriesData { Name = "Data", Values = [1] }],
            XAxisLabel = null,
            YAxisLabel = null
        };

        var cut = Render<ChartDisplay>(parameters => parameters
            .Add(p => p.Chart, chart));

        Assert.IsFalse(cut.Markup.Contains("X:"));
        Assert.IsFalse(cut.Markup.Contains("Y:"));
    }

    #endregion
}
