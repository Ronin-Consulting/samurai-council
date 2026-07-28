using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SamurAICouncil.Core.Services.Export.Model;
using MdHeadingBlock = Markdig.Syntax.HeadingBlock;
using MdParagraphBlock = Markdig.Syntax.ParagraphBlock;

namespace SamurAICouncil.Core.Services.Export;

/// <summary>
/// Converts markdown text into <see cref="ReportBlock"/>s by walking Markdig's AST, so all three
/// export renderers (PDF/Excel/Word) see identical parsed structure for the same markdown - replacing
/// the two independent hand-rolled regex/line-scanners that previously existed per format.
/// </summary>
public static class MarkdownToReportBlocksConverter
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UsePipeTables().UseEmphasisExtras().Build();

    public static IReadOnlyList<ReportBlock> Convert(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        var blocks = new List<ReportBlock>();

        foreach (var block in document)
        {
            var converted = ConvertBlock(block);
            if (converted != null)
                blocks.Add(converted);
        }

        return blocks;
    }

    private static ReportBlock? ConvertBlock(Block block)
    {
        switch (block)
        {
            case MdHeadingBlock heading:
                return new Model.HeadingBlock(heading.Level, ConvertInlines(heading.Inline));

            case MdParagraphBlock paragraph:
                return new Model.ParagraphBlock(ConvertInlines(paragraph.Inline));

            case ListBlock list:
                return ConvertList(list);

            case Table table:
                return ConvertTable(table);

            case QuoteBlock quote:
                // No dedicated blockquote block type yet - render as a plain paragraph per item,
                // flattened; visually distinct blockquote styling can be added later if needed.
                var runs = new List<InlineRun>();
                foreach (var child in quote)
                {
                    if (child is MdParagraphBlock qp)
                        runs.AddRange(ConvertInlines(qp.Inline));
                }
                return new Model.ParagraphBlock(runs);

            case CodeBlock code:
                return new MonospaceBlock(code.Lines.ToString());

            default:
                return null;
        }
    }

    private static ReportBlock ConvertList(ListBlock list)
    {
        var items = new List<IReadOnlyList<InlineRun>>();
        foreach (var itemBlock in list)
        {
            if (itemBlock is not ListItemBlock listItem)
                continue;

            var runs = new List<InlineRun>();
            foreach (var child in listItem)
            {
                if (child is MdParagraphBlock p)
                    runs.AddRange(ConvertInlines(p.Inline));
            }
            items.Add(runs);
        }

        return list.IsOrdered ? new NumberedListBlock(items) : new BulletListBlock(items);
    }

    private static ReportBlock ConvertTable(Table table)
    {
        var headers = new List<string>();
        var rows = new List<IReadOnlyList<string>>();

        foreach (var rowBlock in table)
        {
            if (rowBlock is not TableRow row)
                continue;

            var cells = new List<string>();
            foreach (var cellBlock in row)
            {
                if (cellBlock is not TableCell cell)
                    continue;

                cells.Add(ExtractPlainText(cell));
            }

            if (row.IsHeader)
                headers.AddRange(cells);
            else
                rows.Add(cells);
        }

        return new TableBlock(headers, rows);
    }

    private static string ExtractPlainText(ContainerBlock container)
    {
        var runs = new List<InlineRun>();
        foreach (var child in container)
        {
            if (child is MdParagraphBlock p)
                runs.AddRange(ConvertInlines(p.Inline));
        }
        return string.Concat(runs.Select(r => r.Text));
    }

    private static IReadOnlyList<InlineRun> ConvertInlines(ContainerInline? container)
    {
        var runs = new List<InlineRun>();
        if (container == null)
            return runs;

        WalkInlines(container, runs, bold: false, italic: false, code: false);
        return runs;
    }

    private static void WalkInlines(ContainerInline container, List<InlineRun> runs, bool bold, bool italic, bool code)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    runs.Add(new InlineRun(literal.Content.ToString(), bold, italic, code));
                    break;

                case CodeInline codeInline:
                    runs.Add(new InlineRun(codeInline.Content, bold, italic, Code: true));
                    break;

                case EmphasisInline emphasis:
                    var isBold = bold || emphasis.DelimiterCount >= 2;
                    var isItalic = italic || emphasis.DelimiterCount == 1;
                    WalkInlines(emphasis, runs, isBold, isItalic, code);
                    break;

                case LineBreakInline:
                    runs.Add(new InlineRun("\n", bold, italic, code));
                    break;

                case ContainerInline nested:
                    WalkInlines(nested, runs, bold, italic, code);
                    break;
            }
        }
    }
}
