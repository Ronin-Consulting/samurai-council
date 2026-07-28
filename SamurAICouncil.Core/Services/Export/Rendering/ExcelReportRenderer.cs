using ClosedXML.Excel;
using SamurAICouncil.Core.Services.Export.Model;

namespace SamurAICouncil.Core.Services.Export.Rendering;

/// <summary>
/// Renders a <see cref="ReportDocument"/> to Excel bytes via ClosedXML. Ported from the previous
/// PdfExportService/MarkdownToExcelRenderer's hand-rolled markdown scanner - now driven by the
/// shared, already-parsed <see cref="ReportBlock"/> tree instead of re-scanning raw markdown.
/// </summary>
public static class ExcelReportRenderer
{
    private const int CouncilResponseCharLimit = 5000;
    private const int ToolInputCharLimit = 2000;
    private const int ToolOutputCharLimit = 10000;

    public static byte[] Render(ReportDocument document)
    {
        using var workbook = new XLWorkbook();

        var summarySheet = workbook.Worksheets.Add("Summary");
        ComposeSummarySheet(summarySheet, document);

        var councilSection = document.Sections.FirstOrDefault(s => s.Kind == ReportSectionKind.CouncilResponses);
        if (councilSection != null)
        {
            var responsesSheet = workbook.Worksheets.Add("Council Responses");
            ComposeResponsesSheet(responsesSheet, councilSection);
        }

        var rankingsSection = document.Sections.FirstOrDefault(s => s.Kind == ReportSectionKind.Rankings);
        if (rankingsSection != null)
        {
            var rankingsSheet = workbook.Worksheets.Add("Rankings");
            ComposeRankingsSheet(rankingsSheet, (TableBlock)rankingsSection.Blocks[0]);
        }

        var toolSection = document.Sections.FirstOrDefault(s => s.Kind == ReportSectionKind.ToolUsage);
        if (toolSection != null)
        {
            var toolsSheet = workbook.Worksheets.Add("Tool Usage");
            ComposeToolUsageSheet(toolsSheet, toolSection);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void ComposeSummarySheet(IXLWorksheet sheet, ReportDocument document)
    {
        sheet.Cell(1, 1).Value = document.Title + " Response";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.DarkRed;
        sheet.Range(1, 1, 1, 2).Merge();

        var finalAnswerSection = document.Sections.FirstOrDefault(s => s.Kind == ReportSectionKind.FinalAnswer);

        sheet.Cell(3, 1).Value = "Query";
        sheet.Cell(3, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(3, 2).Value = document.Query;
        sheet.Cell(3, 2).Style.Alignment.WrapText = true;

        sheet.Cell(4, 1).Value = "Timestamp";
        sheet.Cell(4, 1).Style.Font.Bold = true;
        sheet.Cell(4, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(4, 2).Value = document.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC");

        sheet.Cell(5, 1).Value = "Chairman";
        sheet.Cell(5, 1).Style.Font.Bold = true;
        sheet.Cell(5, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(5, 2).Value = finalAnswerSection?.HeaderMeta?.Replace("Chairman: ", "") ?? "N/A";

        sheet.Cell(7, 1).Value = "Final Answer";
        sheet.Cell(7, 1).Style.Font.Bold = true;
        sheet.Cell(7, 1).Style.Font.FontSize = 14;
        sheet.Cell(7, 1).Style.Font.FontColor = XLColor.DarkRed;
        sheet.Range(7, 1, 7, 5).Merge();

        sheet.Column(1).Width = 25;
        sheet.Column(2).Width = 20;
        sheet.Column(3).Width = 15;
        sheet.Column(4).Width = 15;
        sheet.Column(5).Width = 15;

        if (finalAnswerSection != null)
            RenderBlocksToExcel(sheet, finalAnswerSection.Blocks, startRow: 8, startCol: 1, colSpan: 5);
        else
            sheet.Cell(8, 1).Value = "No response available";

        var dataRange = sheet.Range(3, 1, 5, 2);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void ComposeResponsesSheet(IXLWorksheet sheet, ReportSection councilSection)
    {
        sheet.Cell(1, 1).Value = "Model";
        sheet.Cell(1, 2).Value = "Response";
        sheet.Cell(1, 3).Value = "Used Tools";

        var headerRange = sheet.Range(1, 1, 1, 3);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.DarkRed;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = 2;
        foreach (var block in councilSection.Blocks)
        {
            if (block is not CardBlock card)
                continue;

            sheet.Cell(row, 1).Value = card.Title;
            sheet.Cell(row, 2).Value = ExportTextHelpers.TruncateText(PlainText(card.Content), CouncilResponseCharLimit);
            sheet.Cell(row, 2).Style.Alignment.WrapText = true;
            sheet.Cell(row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.Cell(row, 3).Value = card.Badge != null ? "Yes" : "No";
            sheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (row % 2 == 0)
                sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.WhiteSmoke;

            row++;
        }

        var dataRange = sheet.Range(1, 1, row - 1, 3);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Column(1).Width = 20;
        sheet.Column(2).Width = 80;
        sheet.Column(3).Width = 12;
        sheet.Column(1).AdjustToContents(1, row, 15, 25);
    }

    private static void ComposeRankingsSheet(IXLWorksheet sheet, TableBlock table)
    {
        sheet.Cell(1, 1).Value = "Rank";
        sheet.Cell(1, 2).Value = "Model";
        sheet.Cell(1, 3).Value = "Average Rank";
        sheet.Cell(1, 4).Value = "Votes";

        var headerRange = sheet.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.DarkRed;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = 2;
        for (var i = 0; i < table.Rows.Count; i++)
        {
            var dataRow = table.Rows[i];
            sheet.Cell(row, 1).Value = int.Parse(dataRow[0]);
            sheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, 2).Value = dataRow[1];
            sheet.Cell(row, 3).Value = double.Parse(dataRow[2]);
            sheet.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, 4).Value = int.Parse(dataRow[3]);
            sheet.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (table.HighlightRowIndex == i)
            {
                sheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGoldenrodYellow;
                sheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            }
            else if (row % 2 == 0)
            {
                sheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.WhiteSmoke;
            }

            row++;
        }

        var dataRange = sheet.Range(1, 1, row - 1, 4);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Columns().AdjustToContents();
    }

    private static void ComposeToolUsageSheet(IXLWorksheet sheet, ReportSection toolSection)
    {
        sheet.Cell(1, 1).Value = "Tool Name";
        sheet.Cell(1, 2).Value = "Input (Query)";
        sheet.Cell(1, 3).Value = "Output (Result)";

        var headerRange = sheet.Range(1, 1, 1, 3);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = 2;
        foreach (var block in toolSection.Blocks)
        {
            if (block is not CardBlock card)
                continue;

            var input = card.Content.OfType<MonospaceBlock>().ElementAtOrDefault(0)?.Text ?? "";
            var output = card.Content.OfType<MonospaceBlock>().ElementAtOrDefault(1)?.Text ?? "";

            sheet.Cell(row, 1).Value = card.Title?.Replace("Tool: ", "");
            sheet.Cell(row, 2).Value = ExportTextHelpers.TruncateText(input, ToolInputCharLimit);
            sheet.Cell(row, 2).Style.Alignment.WrapText = true;
            sheet.Cell(row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.Cell(row, 3).Value = ExportTextHelpers.TruncateText(output, ToolOutputCharLimit);
            sheet.Cell(row, 3).Style.Alignment.WrapText = true;
            sheet.Cell(row, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            if (row % 2 == 0)
                sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.AliceBlue;

            row++;
        }

        var dataRange = sheet.Range(1, 1, row - 1, 3);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Column(1).Width = 20;
        sheet.Column(2).Width = 50;
        sheet.Column(3).Width = 80;
    }

    private static string PlainText(IReadOnlyList<ReportBlock> blocks) =>
        string.Join("\n", blocks.Select(PlainTextForBlock).Where(t => !string.IsNullOrEmpty(t)));

    private static string PlainTextForBlock(ReportBlock block) => block switch
    {
        HeadingBlock h => string.Concat(h.Runs.Select(r => r.Text)),
        ParagraphBlock p => string.Concat(p.Runs.Select(r => r.Text)),
        BulletListBlock b => string.Join("\n", b.Items.Select(i => "• " + string.Concat(i.Select(r => r.Text)))),
        NumberedListBlock n => string.Join("\n", n.Items.Select((i, idx) => $"{idx + 1}. " + string.Concat(i.Select(r => r.Text)))),
        TableBlock t => string.Join("\n", t.Rows.Select(r => string.Join(" | ", r))),
        MonospaceBlock m => m.Text,
        CardBlock c => PlainText(c.Content),
        _ => string.Empty,
    };

    private static void RenderBlocksToExcel(IXLWorksheet sheet, IReadOnlyList<ReportBlock> blocks, int startRow, int startCol, int colSpan)
    {
        var row = startRow;
        foreach (var block in blocks)
            row = RenderBlockToExcel(sheet, block, row, startCol, colSpan);
    }

    private static int RenderBlockToExcel(IXLWorksheet sheet, ReportBlock block, int row, int col, int colSpan)
    {
        switch (block)
        {
            case HeadingBlock heading:
                return RenderRichTextCell(sheet, heading.Runs, row, col, colSpan, fontSize: 14, bold: true, headingColor: true);

            case ParagraphBlock paragraph:
                return RenderRichTextCell(sheet, paragraph.Runs, row, col, colSpan, fontSize: 11, bold: false, headingColor: false);

            case BulletListBlock bulletList:
                return RenderListCell(sheet, bulletList.Items, row, col, colSpan, bullet: true);

            case NumberedListBlock numberedList:
                return RenderListCell(sheet, numberedList.Items, row, col, colSpan, bullet: false);

            case TableBlock table:
                var next = RenderTableToExcel(sheet, table, row, col);
                return next + 1;

            case MonospaceBlock mono:
                sheet.Cell(row, col).Value = mono.Text;
                sheet.Cell(row, col).Style.Font.FontName = "Consolas";
                return row + 1;

            case ImageBlock image:
                using (var stream = new MemoryStream(image.PngBytes))
                {
                    sheet.AddPicture(stream).MoveTo(sheet.Cell(row, col));
                }
                return row + 20; // rough vertical space for the inserted picture

            case CardBlock card:
                RenderBlocksToExcel(sheet, card.Content, row, col, colSpan);
                return row + Math.Max(1, card.Content.Count);

            default:
                return row;
        }
    }

    private static int RenderRichTextCell(
        IXLWorksheet sheet, IReadOnlyList<InlineRun> runs, int row, int col, int colSpan, int fontSize, bool bold, bool headingColor)
    {
        var cell = sheet.Cell(row, col);
        if (colSpan > 1)
            sheet.Range(row, col, row, col + colSpan - 1).Merge();

        var richText = cell.CreateRichText();
        foreach (var run in runs)
        {
            var rt = richText.AddText(run.Text);
            rt.SetFontSize(fontSize);
            if (bold || run.Bold)
                rt.SetBold(true);
            if (run.Italic)
                rt.SetItalic(true);
            if (run.Code)
                rt.SetFontName("Consolas");
        }

        if (headingColor)
            cell.Style.Font.FontColor = XLColor.DarkRed;
        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        return row + 1;
    }

    private static int RenderListCell(
        IXLWorksheet sheet, IReadOnlyList<IReadOnlyList<InlineRun>> items, int row, int col, int colSpan, bool bullet)
    {
        var cell = sheet.Cell(row, col);
        if (colSpan > 1)
            sheet.Range(row, col, row, col + colSpan - 1).Merge();

        var richText = cell.CreateRichText();
        for (var i = 0; i < items.Count; i++)
        {
            var prefix = bullet ? "• " : $"{i + 1}. ";
            richText.AddText(prefix).SetFontSize(11);
            foreach (var run in items[i])
            {
                var rt = richText.AddText(run.Text);
                rt.SetFontSize(11);
                if (run.Bold) rt.SetBold(true);
                if (run.Italic) rt.SetItalic(true);
            }
            if (i < items.Count - 1)
                richText.AddText("\n");
        }

        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.Row(row).Height = Math.Max(20, items.Count * 18);

        return row + 1;
    }

    private static int RenderTableToExcel(IXLWorksheet sheet, TableBlock table, int startRow, int startCol)
    {
        var currentRow = startRow;

        for (var c = 0; c < table.Headers.Count; c++)
        {
            var cell = sheet.Cell(currentRow, startCol + c);
            cell.Value = table.Headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        currentRow++;

        foreach (var dataRow in table.Rows)
        {
            for (var c = 0; c < Math.Min(dataRow.Count, table.Headers.Count); c++)
            {
                var cell = sheet.Cell(currentRow, startCol + c);
                cell.Value = dataRow[c];
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.WrapText = true;
            }
            currentRow++;
        }

        for (var c = 0; c < table.Headers.Count; c++)
            sheet.Column(startCol + c).AdjustToContents(startRow, currentRow - 1, 10, 30);

        return currentRow;
    }
}
