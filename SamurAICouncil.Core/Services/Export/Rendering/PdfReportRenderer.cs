using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SamurAICouncil.Core.Services.Export.Model;

namespace SamurAICouncil.Core.Services.Export.Rendering;

/// <summary>
/// Renders a <see cref="ReportDocument"/> to PDF bytes via QuestPDF. Ported from the previous
/// PdfExportService/MarkdownToPdfRenderer's hand-rolled markdown scanner - now driven by the
/// shared, already-parsed <see cref="ReportBlock"/> tree instead of re-scanning raw markdown.
/// </summary>
public static class PdfReportRenderer
{
    private const int CouncilResponseCharLimit = 1500;
    private const int ToolTextCharLimit500 = 500;
    private const int ToolTextCharLimit1000 = 1000;

    public static byte[] Render(ReportDocument document)
    {
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken4));

                page.Header().Element(c => ComposeHeader(c, document));
                page.Content().Element(c => ComposeContent(c, document));
                page.Footer().Element(ComposeFooter);
            });
        });

        return pdf.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ReportDocument document)
    {
        container.PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(document.Title).Bold().FontSize(24).FontColor(Colors.Red.Darken2);
                column.Item().Text(document.Subtitle).FontSize(10).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void ComposeContent(IContainer container, ReportDocument document)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(15);
            foreach (var section in document.Sections)
                column.Item().Element(c => ComposeSection(c, section));
        });
    }

    private static void ComposeSection(IContainer container, ReportSection section)
    {
        switch (section.Kind)
        {
            case ReportSectionKind.Query:
                ComposeQuerySection(container, section);
                break;
            case ReportSectionKind.FinalAnswer:
                ComposeFinalAnswerSection(container, section);
                break;
            case ReportSectionKind.ToolUsage:
                ComposeToolUsageSection(container, section);
                break;
            case ReportSectionKind.CouncilResponses:
                ComposeCouncilResponsesSection(container, section);
                break;
            case ReportSectionKind.Rankings:
                ComposeRankingsSection(container, section);
                break;
        }
    }

    private static void ComposeQuerySection(IContainer container, ReportSection section)
    {
        var card = (CardBlock)section.Blocks[0];
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten4).Padding(12).Column(column =>
        {
            column.Spacing(5);
            column.Item().Row(row =>
            {
                row.AutoItem().Text(card.Title).Bold().FontSize(12);
                row.RelativeItem();
                row.AutoItem().Text(card.Badge ?? string.Empty).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
            column.Item().PaddingTop(5).Element(c => RenderBlocks(c, card.Content));
        });
    }

    private static void ComposeFinalAnswerSection(IContainer container, ReportSection section)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Row(row =>
            {
                row.AutoItem().Background(Colors.Red.Darken2).PaddingVertical(4).PaddingHorizontal(8)
                    .Text(section.Label).Bold().FontSize(12).FontColor(Colors.White);
                row.RelativeItem();
                row.AutoItem().AlignMiddle().Text(section.HeaderMeta ?? string.Empty).FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            column.Item().Border(1).BorderColor(Colors.Red.Darken2).Padding(12)
                .Element(c => RenderBlocks(c, section.Blocks));
        });
    }

    private static void ComposeToolUsageSection(IContainer container, ReportSection section)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text(section.Label).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);

            foreach (var block in section.Blocks)
            {
                if (block is not CardBlock card)
                    continue;

                column.Item().Border(1).BorderColor(Colors.Blue.Lighten3).Background(Colors.Blue.Lighten5).Padding(10).Column(toolColumn =>
                {
                    toolColumn.Spacing(5);
                    toolColumn.Item().Text(card.Title ?? string.Empty).Bold().FontSize(10);

                    // Content is [Input label, Input monospace, Output label, Output monospace]
                    for (var i = 0; i < card.Content.Count; i++)
                    {
                        switch (card.Content[i])
                        {
                            case ParagraphBlock p:
                                toolColumn.Item().Text(string.Concat(p.Runs.Select(r => r.Text))).FontSize(9).FontColor(Colors.Grey.Darken1);
                                break;
                            case MonospaceBlock m:
                                var limit = i == 1 ? ToolTextCharLimit500 : ToolTextCharLimit1000; // Input, then Output
                                toolColumn.Item().Background(Colors.Grey.Lighten4).Padding(5)
                                    .Text(ExportTextHelpers.TruncateText(m.Text, limit)).FontSize(8).FontFamily(Fonts.Courier);
                                break;
                        }
                    }
                });
            }
        });
    }

    private static void ComposeCouncilResponsesSection(IContainer container, ReportSection section)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text(section.Label).Bold().FontSize(12).FontColor(Colors.Grey.Darken2);

            foreach (var block in section.Blocks)
            {
                if (block is not CardBlock card)
                    continue;

                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(respColumn =>
                {
                    respColumn.Spacing(5);
                    respColumn.Item().Row(row =>
                    {
                        row.AutoItem().Text(card.Title ?? string.Empty).Bold().FontSize(10);
                        if (card.Badge != null)
                            row.AutoItem().PaddingLeft(8).Text(card.Badge).FontSize(8).FontColor(Colors.Blue.Darken1);
                    });

                    var truncated = ExportTextHelpers.TruncateBlocks(card.Content, CouncilResponseCharLimit);
                    respColumn.Item().PaddingTop(3).Element(c => RenderBlocks(c, truncated));
                });
            }
        });
    }

    private static void ComposeRankingsSection(IContainer container, ReportSection section)
    {
        var table = (TableBlock)section.Blocks[0];
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text(section.Label).Bold().FontSize(12).FontColor(Colors.Grey.Darken2);
            column.Item().Element(c => RenderTableBlock(c, table));
        });
    }

    private static void RenderBlocks(IContainer container, IReadOnlyList<ReportBlock> blocks)
    {
        container.Column(column =>
        {
            foreach (var block in blocks)
                RenderBlock(column, block);
        });
    }

    private static void RenderBlock(ColumnDescriptor column, ReportBlock block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var (size, color) = heading.Level switch
                {
                    1 => (18f, Colors.Grey.Darken4),
                    2 => (16f, Colors.Grey.Darken3),
                    _ => (14f, Colors.Grey.Darken3),
                };
                column.Item().PaddingTop(8).PaddingBottom(4).Text(t =>
                {
                    t.DefaultTextStyle(s => s.Bold().FontSize(size).FontColor(color));
                    RenderInlineRuns(t, heading.Runs);
                });
                break;

            case ParagraphBlock paragraph:
                column.Item().PaddingBottom(4).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.4f));
                    RenderInlineRuns(t, paragraph.Runs);
                });
                break;

            case BulletListBlock bulletList:
                column.Item().PaddingLeft(15).PaddingBottom(4).Column(listColumn =>
                {
                    foreach (var item in bulletList.Items)
                    {
                        listColumn.Item().Row(row =>
                        {
                            row.ConstantItem(15).Text("•").FontSize(11);
                            row.RelativeItem().Text(t =>
                            {
                                t.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.3f));
                                RenderInlineRuns(t, item);
                            });
                        });
                    }
                });
                break;

            case NumberedListBlock numberedList:
                column.Item().PaddingLeft(15).PaddingBottom(4).Column(listColumn =>
                {
                    var number = 1;
                    foreach (var item in numberedList.Items)
                    {
                        listColumn.Item().Row(row =>
                        {
                            row.ConstantItem(20).Text($"{number}.").FontSize(11);
                            row.RelativeItem().Text(t =>
                            {
                                t.DefaultTextStyle(s => s.FontSize(11).LineHeight(1.3f));
                                RenderInlineRuns(t, item);
                            });
                        });
                        number++;
                    }
                });
                break;

            case TableBlock table:
                column.Item().PaddingVertical(8).Element(c => RenderTableBlock(c, table));
                break;

            case MonospaceBlock mono:
                column.Item().Background(Colors.Grey.Lighten4).Padding(5).Text(mono.Text).FontSize(8).FontFamily(Fonts.Courier);
                break;

            case ImageBlock image:
                column.Item().PaddingTop(6).Image(image.PngBytes).FitWidth();
                if (image.Caption != null)
                    column.Item().AlignCenter().Text(image.Caption).FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                break;

            case CardBlock card:
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(cardColumn =>
                {
                    if (card.Title != null)
                        cardColumn.Item().Text(card.Title).Bold().FontSize(10);
                    foreach (var child in card.Content)
                        RenderBlock(cardColumn, child);
                });
                break;
        }
    }

    private static void RenderTableBlock(IContainer container, TableBlock table)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                foreach (var _ in table.Headers)
                    cols.RelativeColumn();
            });

            foreach (var header in table.Headers)
            {
                t.Cell().Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5)
                    .Text(header).Bold().FontSize(10);
            }

            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var isHighlighted = table.HighlightRowIndex == rowIndex;
                var bg = isHighlighted ? Colors.Yellow.Lighten4 : Colors.White;
                foreach (var cell in table.Rows[rowIndex])
                {
                    t.Cell().Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(cell).FontSize(10);
                }
            }
        });
    }

    private static void RenderInlineRuns(TextDescriptor text, IReadOnlyList<InlineRun> runs)
    {
        foreach (var run in runs)
        {
            var span = text.Span(run.Text);
            if (run.Bold)
                span.Bold();
            if (run.Italic)
                span.Italic();
            if (run.Code)
                span.FontFamily(Fonts.Courier);
        }
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated by SamurAI Council • Page ").FontSize(9).FontColor(Colors.Grey.Darken1);
            text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
            text.Span(" of ").FontSize(9).FontColor(Colors.Grey.Darken1);
            text.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }
}
