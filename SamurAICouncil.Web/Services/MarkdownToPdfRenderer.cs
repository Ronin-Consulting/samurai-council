using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SamurAICouncil.Web.Services;

/// <summary>
/// Renders markdown content to QuestPDF elements.
/// Supports headings, bold, italic, lists, and tables.
/// </summary>
public static partial class MarkdownToPdfRenderer
{
    /// <summary>
    /// Renders markdown content to a QuestPDF column container.
    /// </summary>
    public static void RenderMarkdown(ColumnDescriptor column, string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return;

        var lines = markdown.Split('\n');
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
                i = RenderTable(column, lines, i);
                continue;
            }

            // Check for headings
            if (line.StartsWith("### "))
            {
                RenderHeading(column, line[4..], 14, Colors.Grey.Darken3);
                i++;
                continue;
            }
            if (line.StartsWith("## "))
            {
                RenderHeading(column, line[3..], 16, Colors.Grey.Darken3);
                i++;
                continue;
            }
            if (line.StartsWith("# "))
            {
                RenderHeading(column, line[2..], 18, Colors.Grey.Darken4);
                i++;
                continue;
            }

            // Check for unordered list
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                i = RenderUnorderedList(column, lines, i);
                continue;
            }

            // Check for ordered list
            if (OrderedListRegex().IsMatch(line.TrimStart()))
            {
                i = RenderOrderedList(column, lines, i);
                continue;
            }

            // Regular paragraph
            RenderParagraph(column, line);
            i++;
        }
    }

    private static void RenderHeading(ColumnDescriptor column, string headingText, float fontSize, string color)
    {
        column.Item().PaddingTop(8).PaddingBottom(4).Text(t =>
        {
            t.DefaultTextStyle(s => s.Bold().FontSize(fontSize).FontColor(color));
            RenderInlineText(t, headingText);
        });
    }

    private static void RenderParagraph(ColumnDescriptor column, string text)
    {
        column.Item().PaddingBottom(4).Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.4f));
            RenderInlineText(t, text);
        });
    }

    private static int RenderUnorderedList(ColumnDescriptor column, string[] lines, int startIndex)
    {
        column.Item().PaddingLeft(15).PaddingBottom(4).Column(listColumn =>
        {
            var i = startIndex;
            while (i < lines.Length)
            {
                var line = lines[i].TrimEnd('\r').TrimStart();
                if (!line.StartsWith("- ") && !line.StartsWith("* "))
                    break;

                var itemText = line.StartsWith("- ") ? line[2..] : line[2..];
                listColumn.Item().Row(row =>
                {
                    row.ConstantItem(15).Text("\u2022").FontSize(11); // Bullet character
                    row.RelativeItem().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.3f));
                        RenderInlineText(t, itemText);
                    });
                });
                i++;
            }
            return;
        });

        // Return how many lines we consumed
        var consumed = startIndex;
        while (consumed < lines.Length)
        {
            var line = lines[consumed].TrimEnd('\r').TrimStart();
            if (!line.StartsWith("- ") && !line.StartsWith("* "))
                break;
            consumed++;
        }
        return consumed;
    }

    private static int RenderOrderedList(ColumnDescriptor column, string[] lines, int startIndex)
    {
        column.Item().PaddingLeft(15).PaddingBottom(4).Column(listColumn =>
        {
            var i = startIndex;
            var number = 1;
            while (i < lines.Length)
            {
                var line = lines[i].TrimEnd('\r').TrimStart();
                var match = OrderedListRegex().Match(line);
                if (!match.Success)
                    break;

                var itemText = line[(match.Length)..];
                listColumn.Item().Row(row =>
                {
                    row.ConstantItem(20).Text($"{number}.").FontSize(11);
                    row.RelativeItem().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.3f));
                        RenderInlineText(t, itemText);
                    });
                });
                number++;
                i++;
            }
        });

        // Return how many lines we consumed
        var consumed = startIndex;
        while (consumed < lines.Length)
        {
            var line = lines[consumed].TrimEnd('\r').TrimStart();
            if (!OrderedListRegex().IsMatch(line))
                break;
            consumed++;
        }
        return consumed;
    }

    private static int RenderTable(ColumnDescriptor column, string[] lines, int startIndex)
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
            return i;

        // Parse header row
        var headerCells = ParseTableRow(tableLines[0]);
        if (headerCells.Length == 0)
            return i;

        // Skip separator row (|---|---|)
        var dataStartIndex = 1;
        if (tableLines.Count > 1 && tableLines[1].Contains("---"))
            dataStartIndex = 2;

        // Render table
        column.Item().PaddingVertical(8).Table(table =>
        {
            // Define columns
            table.ColumnsDefinition(cols =>
            {
                foreach (var _ in headerCells)
                    cols.RelativeColumn();
            });

            // Header row
            foreach (var cell in headerCells)
            {
                table.Cell()
                    .Background(Colors.Grey.Lighten3)
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten1)
                    .Padding(5)
                    .Text(t =>
                    {
                        t.DefaultTextStyle(s => s.Bold().FontSize(10));
                        RenderInlineText(t, cell.Trim());
                    });
            }

            // Data rows
            for (var rowIndex = dataStartIndex; rowIndex < tableLines.Count; rowIndex++)
            {
                var cells = ParseTableRow(tableLines[rowIndex]);
                for (var colIndex = 0; colIndex < headerCells.Length; colIndex++)
                {
                    var cellText = colIndex < cells.Length ? cells[colIndex].Trim() : "";
                    table.Cell()
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(5)
                        .Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(10));
                            RenderInlineText(t, cellText);
                        });
                }
            }
        });

        return i;
    }

    private static string[] ParseTableRow(string row)
    {
        // Remove leading/trailing pipes and split
        row = row.Trim();
        if (row.StartsWith('|'))
            row = row[1..];
        if (row.EndsWith('|'))
            row = row[..^1];

        return row.Split('|');
    }

    /// <summary>
    /// Renders text with inline formatting (bold, italic).
    /// </summary>
    private static void RenderInlineText(TextDescriptor text, string content)
    {
        if (string.IsNullOrEmpty(content))
            return;

        var parts = BoldItalicRegex().Split(content);
        var matches = BoldItalicRegex().Matches(content);

        var partIndex = 0;
        var matchIndex = 0;

        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part))
            {
                // Check if this part is a match
                if (matchIndex < matches.Count && matches[matchIndex].Index <= GetPosition(parts, partIndex))
                {
                    var match = matches[matchIndex].Value;
                    if (match.StartsWith("**") && match.EndsWith("**"))
                    {
                        // Bold
                        text.Span(match[2..^2]).Bold();
                    }
                    else if (match.StartsWith('*') && match.EndsWith('*'))
                    {
                        // Italic
                        text.Span(match[1..^1]).Italic();
                    }
                    matchIndex++;
                }
                else
                {
                    text.Span(part);
                }
            }
            partIndex++;
        }
    }

    private static int GetPosition(string[] parts, int index)
    {
        var pos = 0;
        for (var i = 0; i < index && i < parts.Length; i++)
            pos += parts[i].Length;
        return pos;
    }

    private static void RenderInlineFormatting(TextDescriptor text, string content)
    {
        // Simplified version - just render spans
        RenderInlineText(text, content);
    }

    private static string StripMarkdownFormatting(string text)
    {
        return BoldItalicRegex().Replace(text, m =>
        {
            if (m.Value.StartsWith("**"))
                return m.Value[2..^2];
            if (m.Value.StartsWith('*'))
                return m.Value[1..^1];
            return m.Value;
        });
    }

    private static string GetCleanText(string text)
    {
        return StripMarkdownFormatting(text);
    }

    [GeneratedRegex(@"^\d+\.\s")]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"(\*\*[^*]+\*\*|\*[^*]+\*)")]
    private static partial Regex BoldItalicRegex();
}
