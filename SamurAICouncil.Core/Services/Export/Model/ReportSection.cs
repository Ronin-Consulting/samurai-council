namespace SamurAICouncil.Core.Services.Export.Model;

public enum ReportSectionKind { Query, FinalAnswer, ToolUsage, CouncilResponses, Rankings }

/// <summary>One labeled section of a <see cref="ReportDocument"/> (e.g. "Final Answer", "Rankings").</summary>
public sealed record ReportSection
{
    public required ReportSectionKind Kind { get; init; }
    public required string Label { get; init; }

    /// <summary>Small header-line detail, e.g. "Chairman: GPT-4o".</summary>
    public string? HeaderMeta { get; init; }

    public required IReadOnlyList<ReportBlock> Blocks { get; init; }
}
