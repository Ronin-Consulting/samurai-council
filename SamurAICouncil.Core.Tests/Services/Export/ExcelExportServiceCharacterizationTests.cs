using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Moq;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Core.Services.Export;

namespace SamurAICouncil.Core.Tests.Services.Export;

// Characterizes the export output shape. Originally written against the pre-refactor
// PdfExportService and confirmed passing there; now exercises the refactored ExportService +
// ReportDocumentBuilder + ExcelReportRenderer pipeline as the regression gate.
[TestClass]
public class ExcelExportServiceCharacterizationTests
{
    private static ExportService CreateService() =>
        new(Mock.Of<ILogger<ExportService>>(), new ReportDocumentBuilder(Mock.Of<IChartImageRenderer>()));

    private static AssistantMessage FullHappyPathMessage() => new()
    {
        Stage1 =
        [
            new Stage1Response { Model = "openai/gpt-4o", Response = "First model's take on the question." },
            new Stage1Response
            {
                Model = "anthropic/claude-3-opus",
                Response = "Second model's take, using a tool.",
                ToolUsages = [new ToolUsage { ToolName = "query_company_data", Input = "SELECT 1", Output = "[{\"x\":1}]" }],
            },
        ],
        Stage2 = [new Stage2Ranking { Model = "openai/gpt-4o", Ranking = "A > B", ParsedRanking = ["A", "B"] }],
        Stage3 = new Stage3Response
        {
            Model = "anthropic/claude-3-opus",
            Response = "## Summary\n\nThis is the **final** answer.",
            ToolUsages = [new ToolUsage { ToolName = "query_company_data", Input = "SELECT 2", Output = "[{\"y\":2}]" }],
        },
        Metadata = new CouncilMetadata
        {
            AggregateRankings =
            [
                new AggregateRanking { Model = "anthropic/claude-3-opus", AverageRank = 1.2, RankingsCount = 3 },
                new AggregateRanking { Model = "openai/gpt-4o", AverageRank = 1.8, RankingsCount = 3 },
            ],
        },
    };

    private static AssistantMessage NoStage1Message() => new()
    {
        Stage3 = new Stage3Response { Model = "openai/gpt-4o", Response = "Only a final answer." },
    };

    private static AssistantMessage EmptyMessage() => new();

    private static XLWorkbook OpenWorkbook(byte[] bytes) => new(new MemoryStream(bytes));

    [TestMethod]
    public async Task FullHappyPath_CreatesAllFourSheets()
    {
        var bytes = await CreateService().ExportToExcelAsync(FullHappyPathMessage(), "What should we do?", DateTime.UtcNow);
        using var wb = OpenWorkbook(bytes);

        var sheetNames = wb.Worksheets.Select(s => s.Name).ToList();
        CollectionAssert.AreEquivalent(
            new[] { "Summary", "Council Responses", "Rankings", "Tool Usage" }, sheetNames);
    }

    [TestMethod]
    public async Task FullHappyPath_SummarySheetHasQueryAndChairman()
    {
        var bytes = await CreateService().ExportToExcelAsync(FullHappyPathMessage(), "What should we do?", DateTime.UtcNow);
        using var wb = OpenWorkbook(bytes);
        var summary = wb.Worksheet("Summary");

        Assert.AreEqual("Query", summary.Cell(3, 1).GetString());
        Assert.AreEqual("What should we do?", summary.Cell(3, 2).GetString());
        Assert.AreEqual("Chairman", summary.Cell(5, 1).GetString());
        Assert.AreEqual("Claude Opus", summary.Cell(5, 2).GetString());
    }

    [TestMethod]
    public async Task FullHappyPath_RankingsSheetSortedWithRankOneFirst()
    {
        var bytes = await CreateService().ExportToExcelAsync(FullHappyPathMessage(), "q", DateTime.UtcNow);
        using var wb = OpenWorkbook(bytes);
        var rankings = wb.Worksheet("Rankings");

        Assert.AreEqual("Claude Opus", rankings.Cell(2, 2).GetString());
        Assert.AreEqual(1, rankings.Cell(2, 1).GetValue<int>());
        Assert.AreEqual(XLColor.LightGoldenrodYellow, rankings.Cell(2, 1).Style.Fill.BackgroundColor);
    }

    [TestMethod]
    public async Task NoStage1_OmitsCouncilResponsesSheet()
    {
        var bytes = await CreateService().ExportToExcelAsync(NoStage1Message(), "q", DateTime.UtcNow);
        using var wb = OpenWorkbook(bytes);

        CollectionAssert.DoesNotContain(wb.Worksheets.Select(s => s.Name).ToList(), "Council Responses");
        CollectionAssert.DoesNotContain(wb.Worksheets.Select(s => s.Name).ToList(), "Rankings");
    }

    [TestMethod]
    public async Task EmptyMessage_OnlyProducesSummarySheet()
    {
        var bytes = await CreateService().ExportToExcelAsync(EmptyMessage(), "just a query", DateTime.UtcNow);
        using var wb = OpenWorkbook(bytes);

        CollectionAssert.AreEquivalent(new[] { "Summary" }, wb.Worksheets.Select(s => s.Name).ToList());
    }
}
