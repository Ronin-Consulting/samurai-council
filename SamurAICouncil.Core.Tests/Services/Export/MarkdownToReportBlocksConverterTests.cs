using SamurAICouncil.Core.Services.Export;
using SamurAICouncil.Core.Services.Export.Model;

namespace SamurAICouncil.Core.Tests.Services.Export;

[TestClass]
public class MarkdownToReportBlocksConverterTests
{
    [TestMethod]
    public void EmptyOrNullInput_ReturnsNoBlocks()
    {
        Assert.AreEqual(0, MarkdownToReportBlocksConverter.Convert(null).Count);
        Assert.AreEqual(0, MarkdownToReportBlocksConverter.Convert("").Count);
        Assert.AreEqual(0, MarkdownToReportBlocksConverter.Convert("   \n  ").Count);
    }

    [TestMethod]
    public void Headings_ProduceHeadingBlocksWithCorrectLevel()
    {
        var blocks = MarkdownToReportBlocksConverter.Convert("# H1\n\n## H2\n\n### H3");

        Assert.AreEqual(3, blocks.Count);
        var h1 = (HeadingBlock)blocks[0];
        var h2 = (HeadingBlock)blocks[1];
        var h3 = (HeadingBlock)blocks[2];
        Assert.AreEqual(1, h1.Level);
        Assert.AreEqual(2, h2.Level);
        Assert.AreEqual(3, h3.Level);
        Assert.AreEqual("H1", string.Concat(h1.Runs.Select(r => r.Text)));
    }

    [TestMethod]
    public void PlainParagraph_ProducesSingleUnformattedRun()
    {
        var blocks = MarkdownToReportBlocksConverter.Convert("Just plain text.");

        Assert.AreEqual(1, blocks.Count);
        var p = (ParagraphBlock)blocks[0];
        Assert.AreEqual("Just plain text.", string.Concat(p.Runs.Select(r => r.Text)));
        Assert.IsFalse(p.Runs.Any(r => r.Bold || r.Italic));
    }

    [TestMethod]
    public void BoldAndItalic_ProduceRunsWithCorrectFlags()
    {
        var blocks = MarkdownToReportBlocksConverter.Convert("This is **bold** and *italic* text.");

        var p = (ParagraphBlock)blocks[0];
        var boldRun = p.Runs.FirstOrDefault(r => r.Text == "bold");
        var italicRun = p.Runs.FirstOrDefault(r => r.Text == "italic");

        Assert.IsNotNull(boldRun);
        Assert.IsTrue(boldRun!.Bold);
        Assert.IsFalse(boldRun.Italic);

        Assert.IsNotNull(italicRun);
        Assert.IsTrue(italicRun!.Italic);
        Assert.IsFalse(italicRun.Bold);
    }

    [TestMethod]
    public void MixedBoldItalic_SetsBothFlags()
    {
        var blocks = MarkdownToReportBlocksConverter.Convert("***both***");
        var p = (ParagraphBlock)blocks[0];
        var run = p.Runs.First(r => r.Text == "both");

        Assert.IsTrue(run.Bold);
        Assert.IsTrue(run.Italic);
    }

    [TestMethod]
    public void InlineCode_SetsCodeFlag()
    {
        var blocks = MarkdownToReportBlocksConverter.Convert("Use `SELECT *` here.");
        var p = (ParagraphBlock)blocks[0];
        var codeRun = p.Runs.FirstOrDefault(r => r.Text == "SELECT *");

        Assert.IsNotNull(codeRun);
        Assert.IsTrue(codeRun!.Code);
    }

    [TestMethod]
    public void UnorderedList_ProducesBulletListBlock()
    {
        var blocks = MarkdownToReportBlocksConverter.Convert("- one\n- two\n- three");

        Assert.AreEqual(1, blocks.Count);
        var list = (BulletListBlock)blocks[0];
        Assert.AreEqual(3, list.Items.Count);
        Assert.AreEqual("one", string.Concat(list.Items[0].Select(r => r.Text)));
    }

    [TestMethod]
    public void OrderedList_ProducesNumberedListBlock()
    {
        var blocks = MarkdownToReportBlocksConverter.Convert("1. first\n2. second");

        Assert.AreEqual(1, blocks.Count);
        var list = (NumberedListBlock)blocks[0];
        Assert.AreEqual(2, list.Items.Count);
    }

    [TestMethod]
    public void WellFormedGfmTable_ProducesTableBlockWithHeadersAndRows()
    {
        var markdown = "| Name | Value |\n| --- | --- |\n| Alpha | 1 |\n| Beta | 2 |";
        var blocks = MarkdownToReportBlocksConverter.Convert(markdown);

        Assert.AreEqual(1, blocks.Count);
        var table = (TableBlock)blocks[0];
        CollectionAssert.AreEqual(new[] { "Name", "Value" }, table.Headers.ToArray());
        Assert.AreEqual(2, table.Rows.Count);
        CollectionAssert.AreEqual(new[] { "Alpha", "1" }, table.Rows[0].ToArray());
        CollectionAssert.AreEqual(new[] { "Beta", "2" }, table.Rows[1].ToArray());
    }

    [TestMethod]
    public void MalformedTable_RaggedRows_DoesNotThrow()
    {
        // Missing a cell in the second data row, and no real separator alignment -
        // documents Markdig's stricter-than-the-old-regex-scanner behavior: this may
        // parse as a table with an empty trailing cell, or fall back to paragraphs,
        // but it must not throw.
        var markdown = "| A | B | C |\n| --- | --- | --- |\n| 1 | 2 |\n| 3 | 4 | 5 | 6 |";

        IReadOnlyList<ReportBlock> blocks = null!;
        var ex = Record(() => blocks = MarkdownToReportBlocksConverter.Convert(markdown));

        Assert.IsNull(ex, $"Conversion should not throw on malformed tables, but threw: {ex}");
        Assert.IsTrue(blocks.Count > 0, "Malformed input should still produce some block, not silently vanish");
    }

    [TestMethod]
    public void PlainTextWithPipesButNoTable_DoesNotProduceTableBlock()
    {
        // A sentence that happens to contain a pipe character shouldn't be misdetected as a table -
        // this is stricter than the old heuristic (text.Contains("|") && text.Contains("---")).
        var blocks = MarkdownToReportBlocksConverter.Convert("Use the `a | b` operator, not `a --- b`.");

        Assert.IsFalse(blocks.Any(b => b is TableBlock));
    }

    private static Exception? Record(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
