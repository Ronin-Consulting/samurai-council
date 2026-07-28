using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Infrastructure;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Core.Services.Export;
using UglyToad.PdfPig;

namespace SamurAICouncil.Core.Tests.Services.Export;

// Characterizes the export output shape. Originally written against the pre-refactor
// PdfExportService and confirmed passing there; now exercises the refactored ExportService +
// ReportDocumentBuilder + PdfReportRenderer pipeline as the regression gate.
[TestClass]
public class PdfExportServiceCharacterizationTests
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static ExportService CreateService() =>
        new(Mock.Of<ILogger<ExportService>>(), new ReportDocumentBuilder(Mock.Of<IChartImageRenderer>()));

    private static string ExtractText(byte[] pdfBytes)
    {
        using var doc = PdfDocument.Open(pdfBytes);
        return string.Join("\n", doc.GetPages().Select(p => p.Text));
    }

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
            Response = "## Summary\n\nThis is the **final** answer with some *emphasis*.\n\n- point one\n- point two",
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

    private static AssistantMessage NoStage3Message() => new()
    {
        Stage1 = [new Stage1Response { Model = "openai/gpt-4o", Response = "Only a Stage1 response exists." }],
    };

    private static AssistantMessage NoToolUsageMessage() => new()
    {
        Stage3 = new Stage3Response { Model = "openai/gpt-4o", Response = "A plain final answer, no tools." },
    };

    private static AssistantMessage MarkdownTableMessage() => new()
    {
        Stage3 = new Stage3Response
        {
            Model = "openai/gpt-4o",
            Response = "Results:\n\n| Name | Value |\n| --- | --- |\n| Alpha | 1 |\n| Beta | 2 |",
        },
    };

    private static AssistantMessage EmptyMessage() => new();

    [TestMethod]
    public async Task FullHappyPath_ContainsAllSectionsInOrder()
    {
        var bytes = await CreateService().ExportToPdfAsync(FullHappyPathMessage(), "What should we do?", DateTime.UtcNow);
        var text = ExtractText(bytes);

        var queryIdx = text.IndexOf("Query", StringComparison.Ordinal);
        var finalAnswerIdx = text.IndexOf("Final Answer", StringComparison.Ordinal);
        var toolUsageIdx = text.IndexOf("Tool Usage", StringComparison.Ordinal);
        var councilIdx = text.IndexOf("Council Member Responses", StringComparison.Ordinal);
        var rankingsIdx = text.IndexOf("Peer Review Rankings", StringComparison.Ordinal);

        Assert.IsTrue(queryIdx >= 0, "Query section missing");
        Assert.IsTrue(finalAnswerIdx > queryIdx, "Final Answer should follow Query");
        Assert.IsTrue(toolUsageIdx > finalAnswerIdx, "Tool Usage should follow Final Answer");
        Assert.IsTrue(councilIdx > toolUsageIdx, "Council Responses should follow Tool Usage");
        Assert.IsTrue(rankingsIdx > councilIdx, "Rankings should follow Council Responses");
        Assert.IsTrue(text.Contains("What should we do?"), "Query text missing");
        Assert.IsTrue(text.Contains("Claude Opus"), "Chairman short name missing");
    }

    [TestMethod]
    public async Task NoStage3_OmitsFinalAnswerAndToolUsageSections()
    {
        var bytes = await CreateService().ExportToPdfAsync(NoStage3Message(), "q", DateTime.UtcNow);
        var text = ExtractText(bytes);

        Assert.IsFalse(text.Contains("Final Answer"));
        Assert.IsFalse(text.Contains("Tool Usage"));
        Assert.IsTrue(text.Contains("Council Member Responses"));
    }

    [TestMethod]
    public async Task NoToolUsage_OmitsToolUsageSection()
    {
        var bytes = await CreateService().ExportToPdfAsync(NoToolUsageMessage(), "q", DateTime.UtcNow);
        var text = ExtractText(bytes);

        Assert.IsTrue(text.Contains("Final Answer"));
        Assert.IsFalse(text.Contains("Tool Usage"));
    }

    [TestMethod]
    public async Task MarkdownTable_RendersCellValues()
    {
        var bytes = await CreateService().ExportToPdfAsync(MarkdownTableMessage(), "q", DateTime.UtcNow);
        var text = ExtractText(bytes);

        Assert.IsTrue(text.Contains("Alpha"));
        Assert.IsTrue(text.Contains("Beta"));
    }

    [TestMethod]
    public async Task EmptyMessage_StillProducesAValidPdfWithJustTheQuery()
    {
        var bytes = await CreateService().ExportToPdfAsync(EmptyMessage(), "just a query", DateTime.UtcNow);
        var text = ExtractText(bytes);

        Assert.IsTrue(bytes.Length > 0);
        Assert.IsTrue(text.Contains("just a query"));
        Assert.IsFalse(text.Contains("Final Answer"));
    }

    [TestMethod]
    public void GenerateFilename_MatchesExpectedPattern()
    {
        var filename = CreateService().GenerateFilename("pdf", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        Assert.AreEqual("SamurAI_Council_2026-01-02_030405.pdf", filename);
    }
}
