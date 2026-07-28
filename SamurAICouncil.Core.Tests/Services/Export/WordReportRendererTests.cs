using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SamurAICouncil.Core.Services.Export;
using SamurAICouncil.Core.Services.Export.Model;
using SamurAICouncil.Core.Services.Export.Rendering;

namespace SamurAICouncil.Core.Tests.Services.Export;

[TestClass]
public class WordReportRendererTests
{
    // Minimal valid 1x1 red-pixel PNG, hardcoded so tests don't depend on System.Drawing (not
    // safe cross-platform) or any other imaging library.
    private static readonly byte[] TinyPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0x1C, 0x0C, 0x02,
        0x00, 0x00, 0x00, 0x0B, 0x49, 0x44, 0x41, 0x54, 0x78, 0xDA, 0x63, 0x64, 0xF8, 0x0F, 0x00, 0x01, 0x05,
        0x01, 0x01, 0x27, 0x18, 0xE3, 0x66, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60,
        0x82,
    ];

    private static ReportDocument BuildDocument(IReadOnlyList<ReportSection> sections) => new()
    {
        Title = "SamurAI Council",
        Subtitle = "Multi-Model AI Advisory Board Response",
        Query = "What percentage of sales come from each channel?",
        Timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        Sections = sections,
    };

    private static WordprocessingDocument OpenResult(byte[] bytes) =>
        WordprocessingDocument.Open(new MemoryStream(bytes), false);

    [TestMethod]
    public void Render_ReturnsNonEmptyDocxBytes()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks = [new ParagraphBlock([new InlineRun("Hello world.")])],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);

        Assert.IsNotNull(bytes);
        Assert.IsTrue(bytes.Length > 0);

        // Zip/OPC magic number sanity check.
        Assert.AreEqual(0x50, bytes[0]);
        Assert.AreEqual(0x4B, bytes[1]);
    }

    [TestMethod]
    public void Render_HeadingBlock_ProducesHeading1StyledParagraph()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks =
                [
                    new HeadingBlock(1, [new InlineRun("Top Level Heading")]),
                    new HeadingBlock(2, [new InlineRun("Second Level Heading")]),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var body = wordDocument.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        Assert.IsTrue(paragraphs.Count > 0);

        // The section label itself is rendered as Heading1, plus the explicit HeadingBlock(1, ...).
        var heading1Paragraphs = paragraphs
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading1")
            .ToList();
        Assert.IsTrue(heading1Paragraphs.Count >= 2, "Expected at least two Heading1-styled paragraphs (section label + explicit heading).");

        var heading2Paragraphs = paragraphs
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading2")
            .ToList();
        Assert.AreEqual(1, heading2Paragraphs.Count);
    }

    [TestMethod]
    public void Render_ParagraphBlockWithFormattedRuns_PreservesBoldItalicAndCode()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks =
                [
                    new ParagraphBlock(
                    [
                        new InlineRun("Plain "),
                        new InlineRun("bold", Bold: true),
                        new InlineRun(" and "),
                        new InlineRun("italic", Italic: true),
                        new InlineRun(" and "),
                        new InlineRun("code", Code: true),
                        new InlineRun("."),
                    ]),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var runs = wordDocument.MainDocumentPart!.Document.Body!
            .Elements<Paragraph>()
            .SelectMany(p => p.Elements<Run>())
            .ToList();

        Assert.IsTrue(runs.Any(r => r.RunProperties?.Bold is not null && r.InnerText == "bold"));
        Assert.IsTrue(runs.Any(r => r.RunProperties?.Italic is not null && r.InnerText == "italic"));
        Assert.IsTrue(runs.Any(r => r.RunProperties?.RunFonts?.Ascii?.Value == "Consolas" && r.InnerText == "code"));
    }

    [TestMethod]
    public void Render_BulletListBlock_ProducesNumberedParagraphsWithBulletDefinition()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks =
                [
                    new BulletListBlock(
                    [
                        [new InlineRun("First item")],
                        [new InlineRun("Second item")],
                        [new InlineRun("Third item")],
                    ]),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var numberingPart = wordDocument.MainDocumentPart!.NumberingDefinitionsPart;
        Assert.IsNotNull(numberingPart);

        var bulletLevel = numberingPart!.Numbering.Elements<AbstractNum>()
            .SelectMany(a => a.Elements<Level>())
            .FirstOrDefault(l => l.NumberingFormat?.Val?.Value == NumberFormatValues.Bullet);
        Assert.IsNotNull(bulletLevel);
        Assert.AreEqual("•", bulletLevel!.LevelText?.Val?.Value);

        var listParagraphs = wordDocument.MainDocumentPart.Document.Body!
            .Elements<Paragraph>()
            .Where(p => p.ParagraphProperties?.NumberingProperties is not null)
            .ToList();
        Assert.AreEqual(3, listParagraphs.Count);
    }

    [TestMethod]
    public void Render_NumberedListBlock_UsesDecimalNumberingDefinition()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks =
                [
                    new NumberedListBlock(
                    [
                        [new InlineRun("Step one")],
                        [new InlineRun("Step two")],
                    ]),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var numberingPart = wordDocument.MainDocumentPart!.NumberingDefinitionsPart;
        Assert.IsNotNull(numberingPart);

        var decimalLevel = numberingPart!.Numbering.Elements<AbstractNum>()
            .SelectMany(a => a.Elements<Level>())
            .FirstOrDefault(l => l.NumberingFormat?.Val?.Value == NumberFormatValues.Decimal);
        Assert.IsNotNull(decimalLevel);
        Assert.AreEqual("%1.", decimalLevel!.LevelText?.Val?.Value);

        var listParagraphs = wordDocument.MainDocumentPart.Document.Body!
            .Elements<Paragraph>()
            .Where(p => p.ParagraphProperties?.NumberingProperties is not null)
            .ToList();
        Assert.AreEqual(2, listParagraphs.Count);
    }

    [TestMethod]
    public void Render_TableBlockWithHighlightedRow_ProducesExpectedShapeAndGoldShading()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.Rankings,
                Label = "Peer Review Rankings",
                Blocks =
                [
                    new TableBlock(
                        Headers: ["#", "Model", "Avg Rank", "Votes"],
                        Rows:
                        [
                            ["1", "GPT-4o", "1.20", "3"],
                            ["2", "Claude Sonnet", "1.80", "3"],
                            ["3", "Gemini 2", "2.10", "3"],
                        ],
                        HighlightRowIndex: 0),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var table = wordDocument.MainDocumentPart!.Document.Body!.Elements<Table>().Single();
        var rows = table.Elements<TableRow>().ToList();

        Assert.AreEqual(4, rows.Count); // header + 3 data rows

        var grid = table.Elements<TableGrid>().Single();
        Assert.AreEqual(4, grid.Elements<GridColumn>().Count());

        foreach (var row in rows)
        {
            Assert.AreEqual(4, row.Elements<TableCell>().Count());
        }

        // Highlighted row (index 0 -> the second row overall, after the header row) should carry the gold fill.
        var highlightedRow = rows[1];
        foreach (var cell in highlightedRow.Elements<TableCell>())
        {
            var fill = cell.TableCellProperties?.Shading?.Fill?.Value;
            Assert.AreEqual(ReportPalette.GoldHighlightHex, fill);
        }

        // Non-highlighted data row should not carry the gold fill.
        var normalRow = rows[2];
        foreach (var cell in normalRow.Elements<TableCell>())
        {
            var fill = cell.TableCellProperties?.Shading?.Fill?.Value;
            Assert.AreNotEqual(ReportPalette.GoldHighlightHex, fill);
        }
    }

    [TestMethod]
    public void Render_CardBlock_ProducesChromeTableWithTitleBadgeAndNestedContent()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.ToolUsage,
                Label = "Tool Usage",
                Blocks =
                [
                    new CardBlock(
                        Title: "CompanyDataTool",
                        Badge: "SQL Query",
                        Content:
                        [
                            new ParagraphBlock([new InlineRun("Retrieved 5 rows.")]),
                        ]),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var body = wordDocument.MainDocumentPart!.Document.Body!;
        var cardTable = body.Elements<Table>().Single();

        var cell = cardTable.Elements<TableRow>().Single().Elements<TableCell>().Single();
        var fill = cell.TableCellProperties?.Shading?.Fill?.Value;
        Assert.AreEqual(ReportPalette.GreyBoxBackgroundHex, fill);

        var cellText = cell.InnerText;
        StringAssert.Contains(cellText, "CompanyDataTool");
        StringAssert.Contains(cellText, "SQL Query");
        StringAssert.Contains(cellText, "Retrieved 5 rows.");

        // Cell must end in a paragraph (Word requirement).
        Assert.IsInstanceOfType(cell.ChildElements.Last(), typeof(Paragraph));
    }

    [TestMethod]
    public void Render_CardBlockWithNestedTable_EndsCellWithTrailingParagraph()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.ToolUsage,
                Label = "Tool Usage",
                Blocks =
                [
                    new CardBlock(
                        Title: "Result",
                        Badge: null,
                        Content:
                        [
                            new TableBlock(["A", "B"], [["1", "2"]]),
                        ]),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var outerTable = wordDocument.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var cell = outerTable.Elements<TableRow>().Single().Elements<TableCell>().Single();

        // Should contain: header paragraph, nested Table, trailing empty Paragraph.
        Assert.IsTrue(cell.Elements<Table>().Any());
        Assert.IsInstanceOfType(cell.ChildElements.Last(), typeof(Paragraph));
    }

    [TestMethod]
    public void Render_MonospaceBlock_ProducesConsolasShadedBox()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.ToolUsage,
                Label = "Tool Usage",
                Blocks =
                [
                    new MonospaceBlock("SELECT TOP 5 * FROM Sales\nORDER BY Revenue DESC"),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var table = wordDocument.MainDocumentPart!.Document.Body!.Elements<Table>().Single();
        var cell = table.Elements<TableRow>().Single().Elements<TableCell>().Single();

        var fill = cell.TableCellProperties?.Shading?.Fill?.Value;
        Assert.AreEqual(ReportPalette.GreyBoxBackgroundHex, fill);

        var monoRuns = cell.Elements<Paragraph>().SelectMany(p => p.Elements<Run>()).ToList();
        Assert.AreEqual(2, monoRuns.Count); // one paragraph per line
        Assert.IsTrue(monoRuns.All(r => r.RunProperties?.RunFonts?.Ascii?.Value == "Consolas"));
    }

    [TestMethod]
    public void Render_ImageBlock_AddsImagePartAndCaption()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks =
                [
                    new ImageBlock(TinyPngBytes, "Sales by Channel", AspectRatio: 1.5),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var imageParts = wordDocument.MainDocumentPart!.ImageParts.ToList();
        Assert.AreEqual(1, imageParts.Count);

        var body = wordDocument.MainDocumentPart.Document.Body!;
        var drawings = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>().ToList();
        Assert.AreEqual(1, drawings.Count);

        var captionParagraph = body.Elements<Paragraph>()
            .FirstOrDefault(p => p.InnerText.Contains("Sales by Channel"));
        Assert.IsNotNull(captionParagraph);
    }

    [TestMethod]
    public void Render_WithoutImageBlock_HasNoImageParts()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks = [new ParagraphBlock([new InlineRun("No images here.")])],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        Assert.AreEqual(0, wordDocument.MainDocumentPart!.ImageParts.Count());
    }

    [TestMethod]
    public void Render_MultipleImageBlocks_AssignsUniqueDrawingIds()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks =
                [
                    new ImageBlock(TinyPngBytes, null, 1.0),
                    new ImageBlock(TinyPngBytes, null, 2.0),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        Assert.AreEqual(2, wordDocument.MainDocumentPart!.ImageParts.Count());

        var docPropertyIds = wordDocument.MainDocumentPart.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>()
            .Select(p => p.Id?.Value)
            .ToList();

        Assert.AreEqual(2, docPropertyIds.Count);
        CollectionAssert.AllItemsAreUnique(docPropertyIds);
    }

    [TestMethod]
    public void Render_FooterIsPresentWithPageFields()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks = [new ParagraphBlock([new InlineRun("Content.")])],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var footerParts = wordDocument.MainDocumentPart!.FooterParts.ToList();
        Assert.AreEqual(1, footerParts.Count);

        var footerText = footerParts[0].Footer.InnerText;
        StringAssert.Contains(footerText, "Generated by SamurAI Council");

        var simpleFields = footerParts[0].Footer.Descendants<SimpleField>().Select(f => f.Instruction?.Value).ToList();
        CollectionAssert.Contains(simpleFields, "PAGE");
        CollectionAssert.Contains(simpleFields, "NUMPAGES");

        var sectionProperties = wordDocument.MainDocumentPart.Document.Body!.Elements<SectionProperties>().SingleOrDefault();
        Assert.IsNotNull(sectionProperties);
        Assert.IsNotNull(sectionProperties!.GetFirstChild<FooterReference>());
    }

    [TestMethod]
    public void Render_TitleAndSubtitle_AppearAsFirstBodyParagraphsWithRedTitleColor()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                Blocks = [new ParagraphBlock([new InlineRun("Body text.")])],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);
        using var wordDocument = OpenResult(bytes);

        var paragraphs = wordDocument.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();

        var titleParagraph = paragraphs[0];
        var titleRun = titleParagraph.Elements<Run>().Single();
        Assert.AreEqual("SamurAI Council", titleRun.InnerText);
        Assert.AreEqual(ReportPalette.RedAccentHex, titleRun.RunProperties?.Color?.Val?.Value);
        Assert.IsNotNull(titleRun.RunProperties?.Bold);

        var subtitleParagraph = paragraphs[1];
        StringAssert.Contains(subtitleParagraph.InnerText, "Multi-Model AI Advisory Board Response");
    }

    [TestMethod]
    public void Render_FullDocument_WithAllBlockTypesTogether_ProducesValidPackage()
    {
        var document = BuildDocument([
            new ReportSection
            {
                Kind = ReportSectionKind.Query,
                Label = "Query",
                HeaderMeta = "January 01, 2026 12:00:00 UTC",
                Blocks = [new ParagraphBlock([new InlineRun("What percentage of sales come from each channel?")])],
            },
            new ReportSection
            {
                Kind = ReportSectionKind.FinalAnswer,
                Label = "Final Answer",
                HeaderMeta = "Chairman: Claude Opus",
                Blocks =
                [
                    new HeadingBlock(2, [new InlineRun("Summary")]),
                    new ParagraphBlock([new InlineRun("Plain, "), new InlineRun("bold", Bold: true), new InlineRun(" text.")]),
                    new BulletListBlock([[new InlineRun("Point A")], [new InlineRun("Point B")]]),
                    new NumberedListBlock([[new InlineRun("Step 1")], [new InlineRun("Step 2")]]),
                    new ImageBlock(TinyPngBytes, "Chart caption", 1.77),
                ],
            },
            new ReportSection
            {
                Kind = ReportSectionKind.ToolUsage,
                Label = "Tool Usage",
                Blocks =
                [
                    new CardBlock("CompanyDataTool", "SQL Query",
                    [
                        new MonospaceBlock("SELECT * FROM Sales"),
                    ]),
                ],
            },
            new ReportSection
            {
                Kind = ReportSectionKind.Rankings,
                Label = "Peer Review Rankings",
                Blocks =
                [
                    new TableBlock(["#", "Model"], [["1", "GPT-4o"], ["2", "Gemini"]], HighlightRowIndex: 0),
                ],
            },
        ]);

        var bytes = WordReportRenderer.Render(document);

        using var wordDocument = OpenResult(bytes);
        var body = wordDocument.MainDocumentPart!.Document.Body!;

        Assert.IsTrue(body.Elements<Paragraph>().Count() > 0);
        Assert.AreEqual(2, body.Elements<Table>().Count()); // card table + rankings table
        Assert.AreEqual(1, wordDocument.MainDocumentPart.ImageParts.Count());
        Assert.IsNotNull(wordDocument.MainDocumentPart.NumberingDefinitionsPart);
        Assert.AreEqual(1, wordDocument.MainDocumentPart.FooterParts.Count());
    }
}
