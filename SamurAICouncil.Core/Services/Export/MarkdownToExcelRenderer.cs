using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Renders markdown content to Excel cells with rich text formatting.
/// Supports headings, bold, italic, lists, and extracts tables to Excel rows.
/// </summary>
public static partial class MarkdownToExcelRenderer
{
    /// <summary>
    /// Result of rendering markdown to Excel, containing the text content and any extracted tables.
    /// </summary>
    public record RenderResult(int NextRow, List<ExtractedTable> Tables);

    /// <summary>
    /// A table extracted from markdown content.
    /// </summary>
    public record ExtractedTable(string[] Headers, List<string[]> Rows);

    /// <summary>
    /// Renders markdown content to an Excel worksheet starting at the specified row.
    /// Returns the next available row after rendering.
    /// </summary>
    public static RenderResult RenderMarkdown(IXLWorksheet sheet, string markdown, int startRow, int startCol = 1, int colSpan = 2)
    {
        if (string.IsNullOrEmpty(markdown))
            return new RenderResult(startRow, new List<ExtractedTable>());

        var lines = markdown.Split('\n');
        var currentRow = startRow;
        var extractedTables = new List<ExtractedTable>();
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r');

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Check for table (starts with |)
            if (line.TrimStart().StartsWith('|'))
            {
                var (tableEndIndex, table) = ExtractTable(lines, i);
                if (table != null)
                {
                    // Render table directly in Excel
                    currentRow = RenderTableToExcel(sheet, table, currentRow, startCol);
                    currentRow++; // Add spacing after table
                    extractedTables.Add(table);
                }
                i = tableEndIndex;
                continue;
            }

            // Check for headings
            if (line.StartsWith("### "))
            {
                currentRow = RenderHeading(sheet, line[4..], currentRow, startCol, colSpan, 12, true);
                i++;
                continue;
            }
            if (line.StartsWith("## "))
            {
                currentRow = RenderHeading(sheet, line[3..], currentRow, startCol, colSpan, 14, true);
                i++;
                continue;
            }
            if (line.StartsWith("# "))
            {
                currentRow = RenderHeading(sheet, line[2..], currentRow, startCol, colSpan, 16, true);
                i++;
                continue;
            }

            // Check for unordered list
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                var (listEndIndex, listItems) = ExtractUnorderedList(lines, i);
                currentRow = RenderList(sheet, listItems, currentRow, startCol, colSpan, bullet: true);
                i = listEndIndex;
                continue;
            }

            // Check for ordered list
            if (OrderedListRegex().IsMatch(line.TrimStart()))
            {
                var (listEndIndex, listItems) = ExtractOrderedList(lines, i);
                currentRow = RenderList(sheet, listItems, currentRow, startCol, colSpan, bullet: false);
                i = listEndIndex;
                continue;
            }

            // Regular paragraph - render with rich text
            currentRow = RenderParagraph(sheet, line, currentRow, startCol, colSpan);
            i++;
        }

        return new RenderResult(currentRow, extractedTables);
    }

    private static int RenderHeading(IXLWorksheet sheet, string text, int row, int col, int colSpan, int fontSize, bool bold)
    {
        var cell = sheet.Cell(row, col);
        if (colSpan > 1)
            sheet.Range(row, col, row, col + colSpan - 1).Merge();

        var richText = cell.CreateRichText();
        RenderRichText(richText, text, fontSize, bold, false);

        cell.Style.Font.FontColor = XLColor.DarkRed;
        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        return row + 1;
    }

    private static int RenderParagraph(IXLWorksheet sheet, string text, int row, int col, int colSpan)
    {
        var cell = sheet.Cell(row, col);
        if (colSpan > 1)
            sheet.Range(row, col, row, col + colSpan - 1).Merge();

        var richText = cell.CreateRichText();
        RenderRichText(richText, text, 11, false, false);

        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        return row + 1;
    }

    private static int RenderList(IXLWorksheet sheet, List<string> items, int row, int col, int colSpan, bool bullet)
    {
        var cell = sheet.Cell(row, col);
        if (colSpan > 1)
            sheet.Range(row, col, row, col + colSpan - 1).Merge();

        var richText = cell.CreateRichText();

        for (var i = 0; i < items.Count; i++)
        {
            var prefix = bullet ? "\u2022 " : $"{i + 1}. ";
            richText.AddText(prefix).SetFontSize(11);
            RenderRichText(richText, items[i], 11, false, false);
            if (i < items.Count - 1)
                richText.AddText("\n");
        }

        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        // Set row height based on number of items
        var rowHeight = Math.Max(20, items.Count * 18);
        sheet.Row(row).Height = rowHeight;

        return row + 1;
    }

    private static int RenderTableToExcel(IXLWorksheet sheet, ExtractedTable table, int startRow, int startCol)
    {
        var currentRow = startRow;

        // Header row
        for (var c = 0; c < table.Headers.Length; c++)
        {
            var cell = sheet.Cell(currentRow, startCol + c);
            var headerText = StripMarkdownFormatting(table.Headers[c].Trim());
            cell.Value = headerText;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        currentRow++;

        // Data rows
        foreach (var dataRow in table.Rows)
        {
            for (var c = 0; c < Math.Min(dataRow.Length, table.Headers.Length); c++)
            {
                var cell = sheet.Cell(currentRow, startCol + c);
                var cellText = dataRow[c].Trim();

                // Use rich text to properly render bold/italic formatting
                var richText = cell.CreateRichText();
                RenderRichText(richText, cellText, 11, false, false);

                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.WrapText = true;
            }
            currentRow++;
        }

        // Auto-fit table columns
        for (var c = 0; c < table.Headers.Length; c++)
        {
            var col = sheet.Column(startCol + c);
            col.AdjustToContents(startRow, currentRow - 1, 10, 30);
        }

        return currentRow;
    }

    /// <summary>
    /// Renders text with inline formatting (bold, italic) to Excel rich text.
    /// </summary>
    private static void RenderRichText(IXLRichText richText, string content, int baseFontSize, bool baseIsBold, bool baseIsItalic)
    {
        if (string.IsNullOrEmpty(content))
            return;

        // Parse the content for bold and italic markers
        var parts = ParseInlineFormatting(content);

        foreach (var part in parts)
        {
            var rt = richText.AddText(part.Text);
            rt.SetFontSize(baseFontSize);

            if (baseIsBold || part.IsBold)
                rt.SetBold(true);
            if (baseIsItalic || part.IsItalic)
                rt.SetItalic(true);
        }
    }

    private record TextPart(string Text, bool IsBold, bool IsItalic);

    private static List<TextPart> ParseInlineFormatting(string content)
    {
        var parts = new List<TextPart>();
        var remaining = content;

        while (!string.IsNullOrEmpty(remaining))
        {
            // Look for bold first (**text**)
            var boldMatch = BoldRegex().Match(remaining);
            var italicMatch = ItalicRegex().Match(remaining);

            Match? firstMatch = null;
            bool isBold = false;

            if (boldMatch.Success && (!italicMatch.Success || boldMatch.Index <= italicMatch.Index))
            {
                firstMatch = boldMatch;
                isBold = true;
            }
            else if (italicMatch.Success)
            {
                firstMatch = italicMatch;
                isBold = false;
            }

            if (firstMatch == null)
            {
                // No more formatting - add remaining as plain text
                if (!string.IsNullOrEmpty(remaining))
                    parts.Add(new TextPart(remaining, false, false));
                break;
            }

            // Add text before the match
            if (firstMatch.Index > 0)
            {
                parts.Add(new TextPart(remaining[..firstMatch.Index], false, false));
            }

            // Add the formatted text
            var innerText = isBold
                ? firstMatch.Groups[1].Value  // Bold: **text** -> text
                : firstMatch.Groups[1].Value; // Italic: *text* -> text

            parts.Add(new TextPart(innerText, isBold, !isBold));

            // Continue with remaining text
            remaining = remaining[(firstMatch.Index + firstMatch.Length)..];
        }

        return parts;
    }

    private static (int endIndex, ExtractedTable? table) ExtractTable(string[] lines, int startIndex)
    {
        var tableLines = new List<string>();
        var i = startIndex;

        // Collect all table lines
        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r');
            if (!line.TrimStart().StartsWith('|'))
                break;
            tableLines.Add(line);
            i++;
        }

        if (tableLines.Count < 2)
            return (i, null);

        // Parse header row
        var headers = ParseTableRow(tableLines[0]);
        if (headers.Length == 0)
            return (i, null);

        // Skip separator row (|---|---|)
        var dataStartIndex = 1;
        if (tableLines.Count > 1 && tableLines[1].Contains("---"))
            dataStartIndex = 2;

        // Parse data rows
        var rows = new List<string[]>();
        for (var r = dataStartIndex; r < tableLines.Count; r++)
        {
            rows.Add(ParseTableRow(tableLines[r]));
        }

        return (i, new ExtractedTable(headers, rows));
    }

    private static string[] ParseTableRow(string row)
    {
        row = row.Trim();
        if (row.StartsWith('|'))
            row = row[1..];
        if (row.EndsWith('|'))
            row = row[..^1];
        return row.Split('|');
    }

    private static (int endIndex, List<string> items) ExtractUnorderedList(string[] lines, int startIndex)
    {
        var items = new List<string>();
        var i = startIndex;

        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r').TrimStart();
            if (!line.StartsWith("- ") && !line.StartsWith("* "))
                break;
            items.Add(line[2..]);
            i++;
        }

        return (i, items);
    }

    private static (int endIndex, List<string> items) ExtractOrderedList(string[] lines, int startIndex)
    {
        var items = new List<string>();
        var i = startIndex;

        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r').TrimStart();
            var match = OrderedListRegex().Match(line);
            if (!match.Success)
                break;
            items.Add(line[match.Length..]);
            i++;
        }

        return (i, items);
    }

    [GeneratedRegex(@"^\d+\.\s")]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")]
    private static partial Regex ItalicRegex();

    /// <summary>
    /// Strips markdown formatting markers from text (bold, italic, headings).
    /// </summary>
    private static string StripMarkdownFormatting(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove bold markers
        text = BoldRegex().Replace(text, "$1");
        // Remove italic markers
        text = ItalicRegex().Replace(text, "$1");
        // Remove heading markers
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^#{1,6}\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        return text;
    }
}
