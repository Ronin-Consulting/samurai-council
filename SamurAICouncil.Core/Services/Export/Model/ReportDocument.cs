namespace SamurAICouncil.Core.Services.Export.Model;

/// <summary>
/// The single shared representation of an exportable council response, consumed identically by the
/// PDF, Excel, and Word renderers so all three formats render the same content structure.
/// Built once by <see cref="ReportDocumentBuilder"/> from an <c>AssistantMessage</c> + query + timestamp.
/// </summary>
public sealed record ReportDocument
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Query { get; init; }
    public required DateTime Timestamp { get; init; }
    public required IReadOnlyList<ReportSection> Sections { get; init; }
}
