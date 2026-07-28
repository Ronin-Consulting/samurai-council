using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SamurAICouncil.Core.Services.Export.Model;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace SamurAICouncil.Core.Services.Export.Rendering;

/// <summary>
/// Renders a <see cref="ReportDocument"/> to a Word (.docx) file using the OpenXML SDK.
/// Mirrors the structure produced by the PDF (QuestPDF) and Excel (ClosedXML) renderers so all
/// three export formats present the same content.
/// </summary>
public static class WordReportRenderer
{
    /// <summary>Maximum content width for embedded images: 6 inches, expressed in EMUs (914400 EMU/inch).</summary>
    private const long MaxImageWidthEmu = 5_486_400L;

    private const string MutedGreyHex = "757575";
    private const string HeaderGreyHex = "D9D9D9";

    public static byte[] Render(ReportDocument document)
    {
        using var stream = new MemoryStream();

        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.Append(body);

            var (bulletNumberingId, decimalNumberingId) = EnsureNumbering(mainPart);
            var drawingIds = new DrawingIdAllocator();

            AppendTitle(body, document);

            foreach (var section in document.Sections)
            {
                AppendSection(mainPart, body, section, bulletNumberingId, decimalNumberingId, drawingIds);
            }

            AddFooter(mainPart, body);

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // ------------------------------------------------------------------
    // Title / sections
    // ------------------------------------------------------------------

    private static void AppendTitle(Body body, ReportDocument document)
    {
        var titleParagraph = new Paragraph();
        var titleRunProperties = new RunProperties(
            new Bold(),
            new Color { Val = ReportPalette.RedAccentHex },
            new FontSize { Val = "56" }); // 28pt
        titleParagraph.Append(new Run(titleRunProperties, new Text(document.Title) { Space = SpaceProcessingModeValues.Preserve }));
        body.Append(titleParagraph);

        if (!string.IsNullOrWhiteSpace(document.Subtitle))
        {
            var subtitleParagraph = new Paragraph();
            var subtitleRunProperties = new RunProperties(
                new Color { Val = MutedGreyHex },
                new FontSize { Val = "20" }); // 10pt
            subtitleParagraph.Append(new Run(subtitleRunProperties, new Text(document.Subtitle) { Space = SpaceProcessingModeValues.Preserve }));
            body.Append(subtitleParagraph);
        }
    }

    private static void AppendSection(
        MainDocumentPart mainPart,
        Body body,
        ReportSection section,
        int bulletNumberingId,
        int decimalNumberingId,
        DrawingIdAllocator drawingIds)
    {
        var headingParagraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }));
        headingParagraph.Append(new Run(new Text(section.Label) { Space = SpaceProcessingModeValues.Preserve }));
        body.Append(headingParagraph);

        if (!string.IsNullOrWhiteSpace(section.HeaderMeta))
        {
            var metaParagraph = new Paragraph();
            var metaRunProperties = new RunProperties(new Color { Val = MutedGreyHex }, new FontSize { Val = "18" }); // 9pt
            metaParagraph.Append(new Run(metaRunProperties, new Text(section.HeaderMeta) { Space = SpaceProcessingModeValues.Preserve }));
            body.Append(metaParagraph);
        }

        foreach (var block in section.Blocks)
        {
            AppendBlock(mainPart, body, block, bulletNumberingId, decimalNumberingId, drawingIds);
        }
    }

    // ------------------------------------------------------------------
    // Block dispatch
    // ------------------------------------------------------------------

    private static void AppendBlock(
        MainDocumentPart mainPart,
        OpenXmlCompositeElement container,
        ReportBlock block,
        int bulletNumberingId,
        int decimalNumberingId,
        DrawingIdAllocator drawingIds)
    {
        switch (block)
        {
            case HeadingBlock heading:
                container.Append(BuildHeadingParagraph(heading));
                break;

            case ParagraphBlock paragraph:
                container.Append(BuildParagraph(paragraph.Runs));
                break;

            case BulletListBlock bulletList:
                foreach (var item in bulletList.Items)
                {
                    container.Append(BuildListItemParagraph(item, bulletNumberingId));
                }
                break;

            case NumberedListBlock numberedList:
                foreach (var item in numberedList.Items)
                {
                    container.Append(BuildListItemParagraph(item, decimalNumberingId));
                }
                break;

            case TableBlock table:
                container.Append(BuildTable(table));
                break;

            case CardBlock card:
                container.Append(BuildCardTable(mainPart, card, bulletNumberingId, decimalNumberingId, drawingIds));
                break;

            case MonospaceBlock mono:
                container.Append(BuildMonospaceTable(mono));
                break;

            case ImageBlock image:
                container.Append(BuildImageParagraph(mainPart, image, drawingIds));
                if (!string.IsNullOrWhiteSpace(image.Caption))
                {
                    container.Append(BuildCaptionParagraph(image.Caption));
                }
                break;

            default:
                throw new NotSupportedException($"Unsupported report block type: {block.GetType().Name}");
        }
    }

    // ------------------------------------------------------------------
    // Paragraphs / runs / lists
    // ------------------------------------------------------------------

    private static Paragraph BuildHeadingParagraph(HeadingBlock heading)
    {
        var styleId = heading.Level switch
        {
            <= 1 => "Heading1",
            2 => "Heading2",
            _ => "Heading3",
        };

        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        foreach (var run in heading.Runs)
        {
            paragraph.Append(BuildRun(run));
        }
        return paragraph;
    }

    private static Paragraph BuildParagraph(IReadOnlyList<InlineRun> runs)
    {
        var paragraph = new Paragraph();
        foreach (var run in runs)
        {
            paragraph.Append(BuildRun(run));
        }
        if (!paragraph.HasChildren)
        {
            paragraph.Append(new Run(new Text(string.Empty)));
        }
        return paragraph;
    }

    private static Paragraph BuildListItemParagraph(IReadOnlyList<InlineRun> runs, int numberingId)
    {
        var paragraphProperties = new ParagraphProperties(
            new ParagraphStyleId { Val = "ListParagraph" },
            new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numberingId }));

        var paragraph = new Paragraph(paragraphProperties);
        foreach (var run in runs)
        {
            paragraph.Append(BuildRun(run));
        }
        if (paragraph.Elements<Run>().Any() == false)
        {
            paragraph.Append(new Run(new Text(string.Empty)));
        }
        return paragraph;
    }

    private static Run BuildRun(InlineRun inline)
    {
        var runProperties = new RunProperties();
        if (inline.Code)
        {
            runProperties.Append(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", ComplexScript = "Consolas" });
        }
        if (inline.Bold)
        {
            runProperties.Append(new Bold());
        }
        if (inline.Italic)
        {
            runProperties.Append(new Italic());
        }

        var run = new Run();
        if (runProperties.HasChildren)
        {
            run.Append(runProperties);
        }
        run.Append(new Text(inline.Text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static Paragraph BuildCaptionParagraph(string caption)
    {
        var paragraph = new Paragraph();
        var runProperties = new RunProperties(new Italic(), new Color { Val = MutedGreyHex }, new FontSize { Val = "18" });
        paragraph.Append(new Run(runProperties, new Text(caption) { Space = SpaceProcessingModeValues.Preserve }));
        return paragraph;
    }

    // ------------------------------------------------------------------
    // Tables / cards / monospace ("boxed") blocks
    // ------------------------------------------------------------------

    private static Table BuildTable(TableBlock tableBlock)
    {
        var table = new Table();
        table.Append(new TableProperties(
            BuildSingleColorBorders(ReportPalette.GreyBoxBorderHex),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));

        var tableGrid = new TableGrid();
        foreach (var _ in tableBlock.Headers)
        {
            tableGrid.Append(new GridColumn());
        }
        table.Append(tableGrid);

        var headerRow = new TableRow();
        foreach (var header in tableBlock.Headers)
        {
            var cell = new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderGreyHex }),
                new Paragraph(new Run(new RunProperties(new Bold()), new Text(header) { Space = SpaceProcessingModeValues.Preserve })));
            headerRow.Append(cell);
        }
        table.Append(headerRow);

        for (var rowIndex = 0; rowIndex < tableBlock.Rows.Count; rowIndex++)
        {
            var isHighlighted = tableBlock.HighlightRowIndex == rowIndex;
            var tableRow = new TableRow();

            foreach (var cellText in tableBlock.Rows[rowIndex])
            {
                var cellProperties = isHighlighted
                    ? new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = ReportPalette.GoldHighlightHex })
                    : new TableCellProperties();

                var cell = new TableCell(
                    cellProperties,
                    new Paragraph(new Run(new Text(cellText) { Space = SpaceProcessingModeValues.Preserve })));
                tableRow.Append(cell);
            }
            table.Append(tableRow);
        }

        return table;
    }

    private static Table BuildCardTable(
        MainDocumentPart mainPart,
        CardBlock card,
        int bulletNumberingId,
        int decimalNumberingId,
        DrawingIdAllocator drawingIds)
    {
        var table = BuildChromeTableShell(ReportPalette.GreyBoxBackgroundHex, ReportPalette.GreyBoxBorderHex, out var cell);

        if (!string.IsNullOrWhiteSpace(card.Title) || !string.IsNullOrWhiteSpace(card.Badge))
        {
            var headerParagraph = new Paragraph();
            if (!string.IsNullOrWhiteSpace(card.Title))
            {
                headerParagraph.Append(new Run(new RunProperties(new Bold()), new Text(card.Title) { Space = SpaceProcessingModeValues.Preserve }));
            }
            if (!string.IsNullOrWhiteSpace(card.Badge))
            {
                if (!string.IsNullOrWhiteSpace(card.Title))
                {
                    headerParagraph.Append(new Run(new Text(" ") { Space = SpaceProcessingModeValues.Preserve }));
                }
                var badgeRunProperties = new RunProperties(new Color { Val = MutedGreyHex }, new FontSize { Val = "16" });
                headerParagraph.Append(new Run(badgeRunProperties, new Text(card.Badge) { Space = SpaceProcessingModeValues.Preserve }));
            }
            cell.Append(headerParagraph);
        }

        foreach (var contentBlock in card.Content)
        {
            AppendBlock(mainPart, cell, contentBlock, bulletNumberingId, decimalNumberingId, drawingIds);
        }

        EnsureCellEndsWithParagraph(cell);

        return table;
    }

    private static Table BuildMonospaceTable(MonospaceBlock block)
    {
        var table = BuildChromeTableShell(ReportPalette.GreyBoxBackgroundHex, ReportPalette.GreyBoxBorderHex, out var cell);

        var lines = (block.Text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var paragraph = new Paragraph();
            var runProperties = new RunProperties(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", ComplexScript = "Consolas" });
            paragraph.Append(new Run(runProperties, new Text(line) { Space = SpaceProcessingModeValues.Preserve }));
            cell.Append(paragraph);
        }

        EnsureCellEndsWithParagraph(cell);

        return table;
    }

    /// <summary>
    /// Builds a single-row, single-cell, borderless-looking (uniform light border) table used as the
    /// "chrome" wrapper for both <see cref="CardBlock"/> and <see cref="MonospaceBlock"/> content, so the
    /// exported .docx visually parallels the boxed cards/panes rendered by the PDF and Excel exporters.
    /// </summary>
    private static Table BuildChromeTableShell(string backgroundHex, string borderHex, out TableCell cell)
    {
        var table = new Table();
        table.Append(new TableProperties(
            BuildSingleColorBorders(borderHex),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));
        table.Append(new TableGrid(new GridColumn()));

        var row = new TableRow();
        cell = new TableCell();
        cell.Append(new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = backgroundHex }));
        row.Append(cell);
        table.Append(row);

        return table;
    }

    /// <summary>Word requires every table cell to end in a block-level paragraph (not a nested table).</summary>
    private static void EnsureCellEndsWithParagraph(TableCell cell)
    {
        var hasBlockContent = cell.ChildElements.Any(e => e is Paragraph || e is Table);
        if (!hasBlockContent || cell.ChildElements.Last() is Table)
        {
            cell.Append(new Paragraph());
        }
    }

    private static TableBorders BuildSingleColorBorders(string colorHex)
    {
        return new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4, Color = colorHex },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = colorHex },
            new LeftBorder { Val = BorderValues.Single, Size = 4, Color = colorHex },
            new RightBorder { Val = BorderValues.Single, Size = 4, Color = colorHex },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = colorHex },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = colorHex });
    }

    // ------------------------------------------------------------------
    // Images
    // ------------------------------------------------------------------

    private static Paragraph BuildImageParagraph(MainDocumentPart mainPart, ImageBlock image, DrawingIdAllocator drawingIds)
    {
        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var imageStream = new MemoryStream(image.PngBytes))
        {
            imagePart.FeedData(imageStream);
        }
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var aspectRatio = image.AspectRatio > 0 ? image.AspectRatio : 1.0;
        var widthEmu = MaxImageWidthEmu;
        var heightEmu = (long)Math.Round(widthEmu / aspectRatio);

        var drawingId = drawingIds.NextId();
        var drawingName = $"Chart{drawingId}";

        var drawing = new Drawing(
            new WP.Inline(
                new WP.Extent { Cx = widthEmu, Cy = heightEmu },
                new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new WP.DocProperties { Id = drawingId, Name = drawingName },
                new WP.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = drawingId, Name = drawingName },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            });

        return new Paragraph(new Run(drawing));
    }

    /// <summary>Call-scoped allocator for unique wp:docPr / pic:cNvPr ids within a single rendered document.</summary>
    private sealed class DrawingIdAllocator
    {
        private uint _next = 1;

        public uint NextId() => _next++;
    }

    // ------------------------------------------------------------------
    // Numbering (bullet / decimal lists)
    // ------------------------------------------------------------------

    private static (int BulletNumberingId, int DecimalNumberingId) EnsureNumbering(MainDocumentPart mainPart)
    {
        var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new Numbering();

        var bulletAbstractNum = new AbstractNum { AbstractNumberId = 0 };
        bulletAbstractNum.Append(new Level(
            new NumberingFormat { Val = NumberFormatValues.Bullet },
            new LevelText { Val = "•" },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }),
            new NumberingSymbolRunProperties(new RunFonts { Ascii = "Symbol", HighAnsi = "Symbol", Hint = FontTypeHintValues.Default }))
        {
            LevelIndex = 0,
        });

        var decimalAbstractNum = new AbstractNum { AbstractNumberId = 1 };
        decimalAbstractNum.Append(new Level(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = NumberFormatValues.Decimal },
            new LevelText { Val = "%1." },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }))
        {
            LevelIndex = 0,
        });

        numbering.Append(bulletAbstractNum, decimalAbstractNum);

        var bulletNumberingInstance = new NumberingInstance { NumberID = 1 };
        bulletNumberingInstance.Append(new AbstractNumId { Val = 0 });

        var decimalNumberingInstance = new NumberingInstance { NumberID = 2 };
        decimalNumberingInstance.Append(new AbstractNumId { Val = 1 });

        numbering.Append(bulletNumberingInstance, decimalNumberingInstance);

        numberingPart.Numbering = numbering;

        return (1, 2);
    }

    // ------------------------------------------------------------------
    // Footer
    // ------------------------------------------------------------------

    private static void AddFooter(MainDocumentPart mainPart, Body body)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var footer = new Footer();

        var footerParagraph = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));

        static RunProperties FooterRunProperties() => new(new Color { Val = MutedGreyHex }, new FontSize { Val = "16" });

        footerParagraph.Append(new Run(
            FooterRunProperties(),
            new Text("Generated by SamurAI Council • Page ") { Space = SpaceProcessingModeValues.Preserve }));

        var pageField = new SimpleField { Instruction = "PAGE" };
        pageField.Append(new Run(FooterRunProperties(), new Text("1")));
        footerParagraph.Append(pageField);

        footerParagraph.Append(new Run(FooterRunProperties(), new Text(" of ") { Space = SpaceProcessingModeValues.Preserve }));

        var numPagesField = new SimpleField { Instruction = "NUMPAGES" };
        numPagesField.Append(new Run(FooterRunProperties(), new Text("1")));
        footerParagraph.Append(numPagesField);

        footer.Append(footerParagraph);
        footerPart.Footer = footer;

        var sectionProperties = new SectionProperties(
            new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) });
        body.Append(sectionProperties);
    }
}
