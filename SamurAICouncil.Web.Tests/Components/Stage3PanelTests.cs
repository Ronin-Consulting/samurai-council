using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Web.Components.Shared;

namespace SamurAICouncil.Web.Tests.Components;

[TestClass]
public class Stage3PanelTests : BunitTestBase
{
    [TestMethod]
    public void Stage3Panel_WithNoResponse_ShowsEmptyState()
    {
        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, null));

        Assert.IsTrue(cut.Markup.Contains("No final response available"));
    }

    [TestMethod]
    public void Stage3Panel_WithEmptyResponse_ShowsEmptyState()
    {
        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, new Stage3Response { Model = "test", Response = "" }));

        Assert.IsTrue(cut.Markup.Contains("No final response available"));
    }

    [TestMethod]
    public void Stage3Panel_WithResponse_ShowsHeader()
    {
        var response = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "This is the final synthesized answer."
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        Assert.IsTrue(cut.Markup.Contains("Stage 3: Final Synthesis"));
    }

    [TestMethod]
    public void Stage3Panel_WithResponse_ShowsChairmanBadge()
    {
        var response = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "Final answer here"
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        Assert.IsTrue(cut.Markup.Contains("Chairman:"));
        Assert.IsTrue(cut.Markup.Contains("Claude Opus"));
    }

    [TestMethod]
    public void Stage3Panel_WithResponse_ShowsResponseContent()
    {
        var response = new Stage3Response
        {
            Model = "openai/gpt-4",
            Response = "The final synthesized answer with all the wisdom."
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        Assert.IsTrue(cut.Markup.Contains("The final synthesized answer with all the wisdom."));
    }

    [TestMethod]
    public void Stage3Panel_AppliesCorrectStyling()
    {
        var response = new Stage3Response
        {
            Model = "openai/gpt-4",
            Response = "Test response"
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        // Check that MudPaper is used with success styling
        Assert.IsTrue(cut.Markup.Contains("mud-paper"));
        Assert.IsTrue(cut.Markup.Contains("border-left:"));
        Assert.IsTrue(cut.Markup.Contains("var(--mud-palette-primary)"));
    }

    #region Chart Integration Tests

    [TestMethod]
    public void Stage3Panel_WithChart_DisplaysChartComponent()
    {
        var response = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "Based on the data analysis, here are the results.",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Bar,
                Title = "Top Products by Revenue",
                Labels = ["Product A", "Product B", "Product C"],
                Series = [new ChartSeriesData { Name = "Revenue", Values = [500, 400, 300] }]
            }
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        // Verify chart is rendered
        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Top Products by Revenue"));
    }

    [TestMethod]
    public void Stage3Panel_WithNullChart_DoesNotDisplayChart()
    {
        var response = new Stage3Response
        {
            Model = "openai/gpt-4",
            Response = "Here is the answer without any chart.",
            Chart = null
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        // Verify response is shown but no chart
        Assert.IsTrue(cut.Markup.Contains("Here is the answer without any chart."));
        Assert.IsFalse(cut.Markup.Contains("mud-chart"));
    }

    [TestMethod]
    public void Stage3Panel_WithNoneChartType_DoesNotDisplayChart()
    {
        var response = new Stage3Response
        {
            Model = "openai/gpt-4",
            Response = "Single value result.",
            Chart = new ChartRecommendation
            {
                Type = ChartType.None,
                Title = "",
                Labels = [],
                Series = []
            }
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        Assert.IsTrue(cut.Markup.Contains("Single value result."));
        Assert.IsFalse(cut.Markup.Contains("mud-chart"));
    }

    [TestMethod]
    public void Stage3Panel_WithLineChart_DisplaysChartWithTitle()
    {
        var response = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "The trend shows an upward pattern.",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Line,
                Title = "Monthly Sales Trend",
                Labels = ["Jan", "Feb", "Mar", "Apr", "May"],
                Series = [new ChartSeriesData { Name = "Sales", Values = [100, 120, 115, 140, 160] }],
                XAxisLabel = "Month",
                YAxisLabel = "Sales ($)"
            }
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Monthly Sales Trend"));
        Assert.IsTrue(cut.Markup.Contains("X: Month"));
        Assert.IsTrue(cut.Markup.Contains("Y: Sales ($)"));
    }

    [TestMethod]
    public void Stage3Panel_WithPieChart_DisplaysChart()
    {
        var response = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "Here is the market share breakdown.",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Pie,
                Title = "Market Share",
                Labels = ["Company A", "Company B", "Others"],
                Series = [new ChartSeriesData { Name = "Share", Values = [45, 35, 20] }]
            }
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Market Share"));
    }

    [TestMethod]
    public void Stage3Panel_WithDonutChart_DisplaysChart()
    {
        var response = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "Budget allocation summary.",
            Chart = new ChartRecommendation
            {
                Type = ChartType.Donut,
                Title = "Budget Breakdown",
                Labels = ["R&D", "Marketing", "Operations"],
                Series = [new ChartSeriesData { Name = "Budget", Values = [40, 30, 30] }]
            }
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Budget Breakdown"));
    }

    [TestMethod]
    public void Stage3Panel_WithToolUsagesAndChart_DisplaysBoth()
    {
        var response = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "Query executed successfully.",
            ToolUsages =
            [
                new ToolUsage
                {
                    ToolName = "query_company_data",
                    Input = "SELECT product, revenue FROM sales ORDER BY revenue DESC LIMIT 5",
                    Output = "[{\"product\": \"A\", \"revenue\": 500}]"
                }
            ],
            Chart = new ChartRecommendation
            {
                Type = ChartType.Bar,
                Title = "Top 5 Products",
                Labels = ["A", "B", "C", "D", "E"],
                Series = [new ChartSeriesData { Name = "Revenue", Values = [500, 400, 350, 300, 250] }]
            }
        };

        var cut = Render<Stage3Panel>(parameters => parameters
            .Add(p => p.Response, response));

        // Both tool usage indicator and chart should be present
        Assert.IsTrue(cut.Markup.Contains("mud-chart"));
        Assert.IsTrue(cut.Markup.Contains("Top 5 Products"));
    }

    #endregion
}
