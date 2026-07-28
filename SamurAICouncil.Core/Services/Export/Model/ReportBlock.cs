namespace SamurAICouncil.Core.Services.Export.Model;

/// <summary>
/// A single run of inline text within a paragraph/heading/list-item, carrying its own formatting.
/// </summary>
public sealed record InlineRun(string Text, bool Bold = false, bool Italic = false, bool Code = false);

/// <summary>
/// One content element within a <see cref="ReportSection"/>. Produced either by walking a Markdig
/// AST (<see cref="HeadingBlock"/>, <see cref="ParagraphBlock"/>, lists, tables) or built directly
/// by <see cref="ReportDocumentBuilder"/> for the app's own structured sections (<see cref="CardBlock"/>,
/// <see cref="MonospaceBlock"/>, <see cref="ImageBlock"/>).
/// </summary>
public abstract record ReportBlock;

public sealed record HeadingBlock(int Level, IReadOnlyList<InlineRun> Runs) : ReportBlock;

public sealed record ParagraphBlock(IReadOnlyList<InlineRun> Runs) : ReportBlock;

public sealed record BulletListBlock(IReadOnlyList<IReadOnlyList<InlineRun>> Items) : ReportBlock;

public sealed record NumberedListBlock(IReadOnlyList<IReadOnlyList<InlineRun>> Items) : ReportBlock;

/// <summary>A GFM-style table, or the Rankings table (with an optional highlighted row, e.g. rank #1).</summary>
public sealed record TableBlock(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int? HighlightRowIndex = null) : ReportBlock;

/// <summary>A titled, chrome-wrapped container (the Query box, a per-tool box, a per-model box).</summary>
public sealed record CardBlock(string? Title, string? Badge, IReadOnlyList<ReportBlock> Content) : ReportBlock;

/// <summary>Fixed-width text (tool input/output panes).</summary>
public sealed record MonospaceBlock(string Text) : ReportBlock;

/// <summary>An embedded raster image (a rasterized chart).</summary>
public sealed record ImageBlock(byte[] PngBytes, string? Caption, double AspectRatio) : ReportBlock;
