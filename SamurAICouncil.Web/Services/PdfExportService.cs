using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Web.Services;

/// <summary>
/// Service for exporting council responses to PDF and Excel formats.
/// </summary>
public class PdfExportService : IExportService
{
    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(ILogger<PdfExportService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> ExportToPdfAsync(AssistantMessage message, string query, DateTime timestamp)
    {
        _logger.LogInformation("Generating PDF export for query: {Query}", query.Length > 50 ? query[..50] + "..." : query);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken4));

                // Header
                page.Header().Element(ComposeHeader);

                // Content
                page.Content().Element(c => ComposeContent(c, message, query, timestamp));

                // Footer
                page.Footer().Element(ComposeFooter);
            });
        });

        var pdfBytes = document.GeneratePdf();
        _logger.LogInformation("PDF generated successfully, size: {Size} bytes", pdfBytes.Length);

        return Task.FromResult(pdfBytes);
    }

    public Task<byte[]> ExportToExcelAsync(AssistantMessage message, string query, DateTime timestamp)
    {
        _logger.LogInformation("Generating Excel export for query: {Query}", query.Length > 50 ? query[..50] + "..." : query);

        using var workbook = new XLWorkbook();

        // Sheet 1: Summary
        var summarySheet = workbook.Worksheets.Add("Summary");
        ComposeSummarySheet(summarySheet, message, query, timestamp);

        // Sheet 2: Council Responses (if available)
        if (message.Stage1?.Count > 0)
        {
            var responsesSheet = workbook.Worksheets.Add("Council Responses");
            ComposeResponsesSheet(responsesSheet, message.Stage1);
        }

        // Sheet 3: Rankings (if available)
        if (message.Stage2?.Count > 0 && message.Metadata?.AggregateRankings?.Count > 0)
        {
            var rankingsSheet = workbook.Worksheets.Add("Rankings");
            ComposeRankingsSheet(rankingsSheet, message.Metadata.AggregateRankings);
        }

        // Sheet 4: Tool Usage (if available)
        if (message.Stage3?.UsedTools == true && message.Stage3.ToolUsages?.Count > 0)
        {
            var toolsSheet = workbook.Worksheets.Add("Tool Usage");
            ComposeToolUsageSheet(toolsSheet, message.Stage3.ToolUsages);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var excelBytes = stream.ToArray();

        _logger.LogInformation("Excel generated successfully, size: {Size} bytes", excelBytes.Length);
        return Task.FromResult(excelBytes);
    }

    public bool HasTabularData(AssistantMessage message)
    {
        if (message.Stage3?.UsedTools == true)
            return true;

        if (message.Stage1?.Any(s => s.UsedTools) == true)
            return true;

        // Check for markdown tables in the response
        if (message.Stage3?.Response != null && ContainsMarkdownTable(message.Stage3.Response))
            return true;

        return false;
    }

    public string GenerateFilename(string extension, DateTime timestamp)
    {
        var formattedTime = timestamp.ToString("yyyy-MM-dd_HHmmss");
        return $"SamurAI_Council_{formattedTime}.{extension}";
    }

    private static bool ContainsMarkdownTable(string text)
    {
        // Simple heuristic: look for pipe characters with dashes (table header separator)
        return text.Contains("|") && text.Contains("---");
    }

    private void ComposeHeader(IContainer container)
    {
        container.PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item()
                    .Text("SamurAI Council")
                    .Bold()
                    .FontSize(24)
                    .FontColor(Colors.Red.Darken2);

                column.Item()
                    .Text("Multi-Model AI Advisory Board Response")
                    .FontSize(10)
                    .FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeContent(IContainer container, AssistantMessage message, string query, DateTime timestamp)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(15);

            // Query Section
            column.Item().Element(c => ComposeQuerySection(c, query, timestamp));

            // Final Answer Section (Stage 3)
            if (message.Stage3 != null)
            {
                column.Item().Element(c => ComposeFinalAnswerSection(c, message.Stage3));
            }

            // Tool Usage Section (if any)
            if (message.Stage3?.UsedTools == true)
            {
                column.Item().Element(c => ComposeToolUsageSection(c, message.Stage3.ToolUsages));
            }

            // Council Responses Section (Stage 1)
            if (message.Stage1?.Count > 0)
            {
                column.Item().Element(c => ComposeCouncilResponsesSection(c, message.Stage1, message.Metadata));
            }

            // Rankings Section (Stage 2)
            if (message.Stage2?.Count > 0 && message.Metadata?.AggregateRankings?.Count > 0)
            {
                column.Item().Element(c => ComposeRankingsSection(c, message.Metadata.AggregateRankings));
            }
        });
    }

    private void ComposeQuerySection(IContainer container, string query, DateTime timestamp)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(12).Column(column =>
        {
            column.Spacing(5);

            column.Item().Row(row =>
            {
                row.AutoItem().Text("Query").Bold().FontSize(12);
                row.RelativeItem();
                row.AutoItem().Text(timestamp.ToString("MMMM dd, yyyy HH:mm:ss UTC")).FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            column.Item().PaddingTop(5).Text(query).FontSize(11);
        });
    }

    private void ComposeFinalAnswerSection(IContainer container, Stage3Response stage3)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item().Row(row =>
            {
                row.AutoItem()
                    .Background(Colors.Red.Darken2)
                    .PaddingVertical(4)
                    .PaddingHorizontal(8)
                    .Text("Final Answer")
                    .Bold()
                    .FontSize(12)
                    .FontColor(Colors.White);

                row.RelativeItem();

                row.AutoItem()
                    .AlignMiddle()
                    .Text($"Chairman: {GetShortModelName(stage3.Model)}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });

            column.Item()
                .Border(1)
                .BorderColor(Colors.Red.Darken2)
                .Padding(12)
                .Column(contentColumn =>
                {
                    MarkdownToPdfRenderer.RenderMarkdown(contentColumn, stage3.Response);
                });
        });
    }

    private void ComposeToolUsageSection(IContainer container, List<Core.Interfaces.ToolUsage> toolUsages)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item()
                .Text("Tool Usage")
                .Bold()
                .FontSize(12)
                .FontColor(Colors.Blue.Darken2);

            foreach (var tool in toolUsages)
            {
                column.Item().Border(1).BorderColor(Colors.Blue.Lighten3).Background(Colors.Blue.Lighten5).Padding(10).Column(toolColumn =>
                {
                    toolColumn.Spacing(5);

                    toolColumn.Item().Text($"Tool: {tool.ToolName}").Bold().FontSize(10);

                    toolColumn.Item().Text("Input:").FontSize(9).FontColor(Colors.Grey.Darken1);
                    toolColumn.Item().Background(Colors.Grey.Lighten4).Padding(5).Text(TruncateText(tool.Input, 500)).FontSize(8).FontFamily(Fonts.Courier);

                    toolColumn.Item().PaddingTop(5).Text("Output:").FontSize(9).FontColor(Colors.Grey.Darken1);
                    toolColumn.Item().Background(Colors.Grey.Lighten4).Padding(5).Text(TruncateText(tool.Output, 1000)).FontSize(8).FontFamily(Fonts.Courier);
                });
            }
        });
    }

    private void ComposeCouncilResponsesSection(IContainer container, List<Stage1Response> stage1Responses, CouncilMetadata? metadata)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item()
                .Text("Council Member Responses")
                .Bold()
                .FontSize(12)
                .FontColor(Colors.Grey.Darken2);

            foreach (var response in stage1Responses)
            {
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(respColumn =>
                {
                    respColumn.Spacing(5);

                    respColumn.Item().Row(row =>
                    {
                        row.AutoItem().Text(GetShortModelName(response.Model)).Bold().FontSize(10);
                        if (response.UsedTools)
                        {
                            row.AutoItem().PaddingLeft(8).Text("(used tools)").FontSize(8).FontColor(Colors.Blue.Darken1);
                        }
                    });

                    respColumn.Item().PaddingTop(3).Column(contentCol =>
                    {
                        MarkdownToPdfRenderer.RenderMarkdown(contentCol, TruncateText(response.Response, 1500));
                    });
                });
            }
        });
    }

    private void ComposeRankingsSection(IContainer container, List<AggregateRanking> rankings)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item()
                .Text("Peer Review Rankings")
                .Bold()
                .FontSize(12)
                .FontColor(Colors.Grey.Darken2);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold().FontSize(10);
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Model").Bold().FontSize(10);
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Avg Rank").Bold().FontSize(10);
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Votes").Bold().FontSize(10);
                });

                // Data rows
                var rank = 1;
                foreach (var item in rankings.OrderBy(r => r.AverageRank))
                {
                    var bgColor = rank == 1 ? Colors.Yellow.Lighten4 : Colors.White;

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(rank.ToString()).FontSize(10);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(GetShortModelName(item.Model)).FontSize(10);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.AverageRank.ToString("F2")).FontSize(10);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.RankingsCount.ToString()).FontSize(10);

                    rank++;
                }
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated by SamurAI Council • Page ").FontSize(9).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
            text.Span(" of ").FontSize(9).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    private static string GetShortModelName(string model)
    {
        if (string.IsNullOrEmpty(model))
            return "Unknown";

        var parts = model.Split('/');
        var modelPart = parts.Length > 1 ? parts[^1] : model;

        return modelPart switch
        {
            var m when m.StartsWith("gpt-4o") => "GPT-4o",
            var m when m.StartsWith("gpt-4") => "GPT-4",
            var m when m.StartsWith("gpt-3.5") => "GPT-3.5",
            var m when m.StartsWith("claude-3-opus") => "Claude Opus",
            var m when m.StartsWith("claude-3-sonnet") => "Claude Sonnet",
            var m when m.StartsWith("claude-3.5-sonnet") => "Claude 3.5",
            var m when m.StartsWith("claude-sonnet-4") => "Claude Sonnet 4",
            var m when m.StartsWith("claude-3-haiku") => "Claude Haiku",
            var m when m.StartsWith("gemini-2") => "Gemini 2",
            var m when m.StartsWith("gemini-1.5-pro") => "Gemini 1.5 Pro",
            var m when m.StartsWith("gemini-1.5-flash") => "Gemini 1.5 Flash",
            var m when m.StartsWith("gemini-pro") => "Gemini Pro",
            var m when m.StartsWith("gemini-1.5") => "Gemini 1.5",
            _ => modelPart.Length > 25 ? modelPart[..25] + "..." : modelPart
        };
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    #region Excel Sheet Composition

    private void ComposeSummarySheet(IXLWorksheet sheet, AssistantMessage message, string query, DateTime timestamp)
    {
        // Title
        sheet.Cell(1, 1).Value = "SamurAI Council Response";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.DarkRed;
        sheet.Range(1, 1, 1, 2).Merge();

        // Query Section
        sheet.Cell(3, 1).Value = "Query";
        sheet.Cell(3, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(3, 2).Value = query;
        sheet.Cell(3, 2).Style.Alignment.WrapText = true;

        // Timestamp
        sheet.Cell(4, 1).Value = "Timestamp";
        sheet.Cell(4, 1).Style.Font.Bold = true;
        sheet.Cell(4, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(4, 2).Value = timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC");

        // Chairman
        sheet.Cell(5, 1).Value = "Chairman";
        sheet.Cell(5, 1).Style.Font.Bold = true;
        sheet.Cell(5, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(5, 2).Value = message.Stage3 != null ? GetShortModelName(message.Stage3.Model) : "N/A";

        // Final Answer Section Header
        sheet.Cell(7, 1).Value = "Final Answer";
        sheet.Cell(7, 1).Style.Font.Bold = true;
        sheet.Cell(7, 1).Style.Font.FontSize = 14;
        sheet.Cell(7, 1).Style.Font.FontColor = XLColor.DarkRed;
        sheet.Range(7, 1, 7, 5).Merge();

        // Set column widths for proper rendering
        sheet.Column(1).Width = 25;
        sheet.Column(2).Width = 20;
        sheet.Column(3).Width = 15;
        sheet.Column(4).Width = 15;
        sheet.Column(5).Width = 15;

        // Render Final Answer with markdown formatting
        var finalAnswer = message.Stage3?.Response ?? "No response available";
        var result = MarkdownToExcelRenderer.RenderMarkdown(sheet, finalAnswer, startRow: 8, startCol: 1, colSpan: 5);

        // Add borders to metadata cells
        var dataRange = sheet.Range(3, 1, 5, 2);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private void ComposeResponsesSheet(IXLWorksheet sheet, List<Stage1Response> responses)
    {
        // Header row
        sheet.Cell(1, 1).Value = "Model";
        sheet.Cell(1, 2).Value = "Response";
        sheet.Cell(1, 3).Value = "Used Tools";

        var headerRange = sheet.Range(1, 1, 1, 3);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.DarkRed;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Data rows
        var row = 2;
        foreach (var response in responses)
        {
            sheet.Cell(row, 1).Value = GetShortModelName(response.Model);
            sheet.Cell(row, 2).Value = StripMarkdownFormatting(TruncateText(response.Response, 5000));
            sheet.Cell(row, 2).Style.Alignment.WrapText = true;
            sheet.Cell(row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.Cell(row, 3).Value = response.UsedTools ? "Yes" : "No";
            sheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Alternating row colors
            if (row % 2 == 0)
            {
                sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.WhiteSmoke;
            }

            row++;
        }

        // Borders
        var dataRange = sheet.Range(1, 1, row - 1, 3);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Auto-fit columns
        sheet.Column(1).Width = 20;
        sheet.Column(2).Width = 80;
        sheet.Column(3).Width = 12;
        sheet.Column(1).AdjustToContents(1, row, 15, 25);
    }

    private void ComposeRankingsSheet(IXLWorksheet sheet, List<AggregateRanking> rankings)
    {
        // Header row
        sheet.Cell(1, 1).Value = "Rank";
        sheet.Cell(1, 2).Value = "Model";
        sheet.Cell(1, 3).Value = "Average Rank";
        sheet.Cell(1, 4).Value = "Votes";

        var headerRange = sheet.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.DarkRed;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Data rows
        var row = 2;
        var rank = 1;
        foreach (var item in rankings.OrderBy(r => r.AverageRank))
        {
            sheet.Cell(row, 1).Value = rank;
            sheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, 2).Value = GetShortModelName(item.Model);
            sheet.Cell(row, 3).Value = item.AverageRank;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, 4).Value = item.RankingsCount;
            sheet.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Highlight top rank
            if (rank == 1)
            {
                sheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGoldenrodYellow;
                sheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            }
            else if (row % 2 == 0)
            {
                sheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.WhiteSmoke;
            }

            rank++;
            row++;
        }

        // Borders
        var dataRange = sheet.Range(1, 1, row - 1, 4);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Auto-fit columns
        sheet.Columns().AdjustToContents();
    }

    private void ComposeToolUsageSheet(IXLWorksheet sheet, List<Core.Interfaces.ToolUsage> toolUsages)
    {
        // Header row
        sheet.Cell(1, 1).Value = "Tool Name";
        sheet.Cell(1, 2).Value = "Input (Query)";
        sheet.Cell(1, 3).Value = "Output (Result)";

        var headerRange = sheet.Range(1, 1, 1, 3);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Data rows
        var row = 2;
        foreach (var tool in toolUsages)
        {
            sheet.Cell(row, 1).Value = tool.ToolName;
            sheet.Cell(row, 2).Value = TruncateText(tool.Input, 2000);
            sheet.Cell(row, 2).Style.Alignment.WrapText = true;
            sheet.Cell(row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.Cell(row, 3).Value = TruncateText(tool.Output, 10000);
            sheet.Cell(row, 3).Style.Alignment.WrapText = true;
            sheet.Cell(row, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            if (row % 2 == 0)
            {
                sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.AliceBlue;
            }

            row++;
        }

        // Borders
        var dataRange = sheet.Range(1, 1, row - 1, 3);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Set column widths
        sheet.Column(1).Width = 20;
        sheet.Column(2).Width = 50;
        sheet.Column(3).Width = 80;
    }

    private static string StripMarkdownFormatting(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove bold/italic markers
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*([^*]+)\*", "$1");

        // Remove heading markers
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^#{1,6}\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Remove bullet point markers (keep the text)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^[\-\*]\s+", "• ", System.Text.RegularExpressions.RegexOptions.Multiline);

        return text;
    }

    #endregion
}
