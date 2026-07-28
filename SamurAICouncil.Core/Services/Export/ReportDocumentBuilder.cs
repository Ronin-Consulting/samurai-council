using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services.Export.Model;

namespace SamurAICouncil.Core.Services.Export;

/// <summary>
/// Maps an <see cref="AssistantMessage"/> + query + timestamp into a <see cref="ReportDocument"/> -
/// the single place this happens, so PDF/Excel/Word all render identical content structure.
/// </summary>
public class ReportDocumentBuilder
{
    private readonly IChartImageRenderer _chartRenderer;

    public ReportDocumentBuilder(IChartImageRenderer chartRenderer)
    {
        _chartRenderer = chartRenderer;
    }

    public ReportDocument Build(AssistantMessage message, string query, DateTime timestamp)
    {
        var sections = new List<ReportSection>
        {
            BuildQuerySection(query, timestamp)
        };

        if (message.Stage3 != null)
            sections.Add(BuildFinalAnswerSection(message.Stage3));

        if (message.Stage3?.UsedTools == true)
            sections.Add(BuildToolUsageSection(message.Stage3.ToolUsages));

        if (message.Stage1?.Count > 0)
            sections.Add(BuildCouncilResponsesSection(message.Stage1));

        if (message.Stage2?.Count > 0 && message.Metadata?.AggregateRankings?.Count > 0)
            sections.Add(BuildRankingsSection(message.Metadata.AggregateRankings));

        return new ReportDocument
        {
            Title = "SamurAI Council",
            Subtitle = "Multi-Model AI Advisory Board Response",
            Query = query,
            Timestamp = timestamp,
            Sections = sections,
        };
    }

    private static ReportSection BuildQuerySection(string query, DateTime timestamp)
    {
        var card = new CardBlock(
            Title: "Query",
            Badge: timestamp.ToString("MMMM dd, yyyy HH:mm:ss UTC"),
            Content: [new ParagraphBlock([new InlineRun(query)])]);

        return new ReportSection
        {
            Kind = ReportSectionKind.Query,
            Label = "Query",
            Blocks = [card],
        };
    }

    private ReportSection BuildFinalAnswerSection(Stage3Response stage3)
    {
        var blocks = new List<ReportBlock>(MarkdownToReportBlocksConverter.Convert(stage3.Response));

        var chart = stage3.StudioChart ?? stage3.Chart;
        if (chart != null)
        {
            var png = _chartRenderer.RenderToPng(chart);
            if (png != null)
                // No caption: ChartImageRenderer already draws the chart's title inside the image itself,
                // so captioning with the same text here would just duplicate it.
                blocks.Add(new ImageBlock(png, Caption: null, AspectRatio: 900.0 / 540.0));
        }

        return new ReportSection
        {
            Kind = ReportSectionKind.FinalAnswer,
            Label = "Final Answer",
            HeaderMeta = $"Chairman: {ExportTextHelpers.GetShortModelName(stage3.Model)}",
            Blocks = blocks,
        };
    }

    private static ReportSection BuildToolUsageSection(List<ToolUsage> toolUsages)
    {
        var cards = toolUsages.Select(tool => (ReportBlock)new CardBlock(
            Title: $"Tool: {tool.ToolName}",
            Badge: null,
            Content:
            [
                new ParagraphBlock([new InlineRun("Input:")]),
                new MonospaceBlock(tool.Input),
                new ParagraphBlock([new InlineRun("Output:")]),
                new MonospaceBlock(tool.Output),
            ])).ToList();

        return new ReportSection
        {
            Kind = ReportSectionKind.ToolUsage,
            Label = "Tool Usage",
            Blocks = cards,
        };
    }

    private static ReportSection BuildCouncilResponsesSection(List<Stage1Response> responses)
    {
        var cards = responses.Select(response => (ReportBlock)new CardBlock(
            Title: ExportTextHelpers.GetShortModelName(response.Model),
            Badge: response.UsedTools ? "(used tools)" : null,
            Content: MarkdownToReportBlocksConverter.Convert(response.Response))).ToList();

        return new ReportSection
        {
            Kind = ReportSectionKind.CouncilResponses,
            Label = "Council Member Responses",
            Blocks = cards,
        };
    }

    private static ReportSection BuildRankingsSection(List<AggregateRanking> rankings)
    {
        var sorted = rankings.OrderBy(r => r.AverageRank).ToList();
        var rows = sorted.Select((item, index) => (IReadOnlyList<string>)
        [
            (index + 1).ToString(),
            ExportTextHelpers.GetShortModelName(item.Model),
            item.AverageRank.ToString("F2"),
            item.RankingsCount.ToString(),
        ]).ToList();

        var table = new TableBlock(
            Headers: ["#", "Model", "Avg Rank", "Votes"],
            Rows: rows,
            HighlightRowIndex: rows.Count > 0 ? 0 : null);

        return new ReportSection
        {
            Kind = ReportSectionKind.Rankings,
            Label = "Peer Review Rankings",
            Blocks = [table],
        };
    }
}
