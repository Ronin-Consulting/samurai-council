using Moq;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;
using SamurAICouncil.Core.Services.Export;
using SamurAICouncil.Core.Services.Export.Model;

namespace SamurAICouncil.Core.Tests.Services.Export;

[TestClass]
public class ReportDocumentBuilderTests
{
    private Mock<IChartImageRenderer> _chartRenderer = null!;
    private ReportDocumentBuilder _builder = null!;

    [TestInitialize]
    public void Setup()
    {
        _chartRenderer = new Mock<IChartImageRenderer>();
        _builder = new ReportDocumentBuilder(_chartRenderer.Object);
    }

    [TestMethod]
    public void EmptyStage1_OmitsCouncilResponsesSection()
    {
        var message = new AssistantMessage { Stage3 = new Stage3Response { Model = "gpt-4o", Response = "answer" } };

        var doc = _builder.Build(message, "q", DateTime.UtcNow);

        Assert.IsFalse(doc.Sections.Any(s => s.Kind == ReportSectionKind.CouncilResponses));
    }

    [TestMethod]
    public void MissingStage3_OmitsFinalAnswerAndToolUsageSections()
    {
        var message = new AssistantMessage { Stage1 = [new Stage1Response { Model = "gpt-4o", Response = "r" }] };

        var doc = _builder.Build(message, "q", DateTime.UtcNow);

        Assert.IsFalse(doc.Sections.Any(s => s.Kind == ReportSectionKind.FinalAnswer));
        Assert.IsFalse(doc.Sections.Any(s => s.Kind == ReportSectionKind.ToolUsage));
        _chartRenderer.Verify(r => r.RenderToPng(It.IsAny<ChartRecommendation>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void ToolUsagePresent_ProducesToolUsageSectionWithOneCardPerTool()
    {
        var message = new AssistantMessage
        {
            Stage3 = new Stage3Response
            {
                Model = "gpt-4o",
                Response = "answer",
                ToolUsages = [new ToolUsage { ToolName = "query_company_data", Input = "SELECT 1", Output = "[]" }],
            },
        };

        var doc = _builder.Build(message, "q", DateTime.UtcNow);

        var toolSection = doc.Sections.Single(s => s.Kind == ReportSectionKind.ToolUsage);
        Assert.AreEqual(1, toolSection.Blocks.Count);
        var card = (CardBlock)toolSection.Blocks[0];
        Assert.AreEqual("Tool: query_company_data", card.Title);
    }

    [TestMethod]
    public void ChartPresent_RendersImageAndAppendsImageBlockToFinalAnswer()
    {
        var chart = new ChartRecommendation { Type = ChartType.Bar, Title = "Sales" };
        var fakePng = new byte[] { 1, 2, 3 };
        _chartRenderer.Setup(r => r.RenderToPng(chart, It.IsAny<int>(), It.IsAny<int>())).Returns(fakePng);

        var message = new AssistantMessage
        {
            Stage3 = new Stage3Response { Model = "gpt-4o", Response = "answer", StudioChart = chart },
        };

        var doc = _builder.Build(message, "q", DateTime.UtcNow);

        var finalAnswer = doc.Sections.Single(s => s.Kind == ReportSectionKind.FinalAnswer);
        var imageBlock = finalAnswer.Blocks.OfType<ImageBlock>().SingleOrDefault();
        Assert.IsNotNull(imageBlock);
        CollectionAssert.AreEqual(fakePng, imageBlock!.PngBytes);
    }

    [TestMethod]
    public void ChartAbsent_NeverCallsChartRenderer()
    {
        var message = new AssistantMessage { Stage3 = new Stage3Response { Model = "gpt-4o", Response = "answer" } };

        _builder.Build(message, "q", DateTime.UtcNow);

        _chartRenderer.Verify(r => r.RenderToPng(It.IsAny<ChartRecommendation>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void StudioChartPreferredOverClassicChart()
    {
        var classicChart = new ChartRecommendation { Type = ChartType.Bar, Title = "Classic" };
        var studioChart = new ChartRecommendation { Type = ChartType.Bar, Title = "Studio" };
        _chartRenderer.Setup(r => r.RenderToPng(studioChart, It.IsAny<int>(), It.IsAny<int>())).Returns([9]);

        var message = new AssistantMessage
        {
            Stage3 = new Stage3Response { Model = "gpt-4o", Response = "answer", Chart = classicChart, StudioChart = studioChart },
        };

        _builder.Build(message, "q", DateTime.UtcNow);

        _chartRenderer.Verify(r => r.RenderToPng(studioChart, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _chartRenderer.Verify(r => r.RenderToPng(classicChart, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void RankingsPresent_SortsByAverageRankAndHighlightsFirstRow()
    {
        var message = new AssistantMessage
        {
            Stage2 = [new Stage2Ranking { Model = "gpt-4o", Ranking = "x", ParsedRanking = [] }],
            Metadata = new CouncilMetadata
            {
                AggregateRankings =
                [
                    new AggregateRanking { Model = "gpt-4o", AverageRank = 2.0, RankingsCount = 1 },
                    new AggregateRanking { Model = "claude-3-opus", AverageRank = 1.0, RankingsCount = 1 },
                ],
            },
        };

        var doc = _builder.Build(message, "q", DateTime.UtcNow);

        var rankings = (TableBlock)doc.Sections.Single(s => s.Kind == ReportSectionKind.Rankings).Blocks[0];
        Assert.AreEqual(0, rankings.HighlightRowIndex);
        Assert.AreEqual("Claude Opus", rankings.Rows[0][1]);
        Assert.AreEqual("GPT-4o", rankings.Rows[1][1]);
    }

    [TestMethod]
    public void QuerySectionAlwaysPresent_EvenForEmptyMessage()
    {
        var doc = _builder.Build(new AssistantMessage(), "just a query", DateTime.UtcNow);

        Assert.AreEqual(1, doc.Sections.Count);
        Assert.AreEqual(ReportSectionKind.Query, doc.Sections[0].Kind);
        var card = (CardBlock)doc.Sections[0].Blocks[0];
        var paragraph = (ParagraphBlock)card.Content[0];
        Assert.AreEqual("just a query", string.Concat(paragraph.Runs.Select(r => r.Text)));
    }
}
