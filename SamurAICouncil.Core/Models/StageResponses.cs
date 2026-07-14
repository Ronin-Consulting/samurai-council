using System.Text.Json.Serialization;
using SamurAICouncil.Core.Interfaces;

namespace SamurAICouncil.Core.Models;

/// <summary>
/// Stage 1: Individual model response to the user query.
/// </summary>
public class Stage1Response
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Tool usages during this response, if any.
    /// </summary>
    [JsonPropertyName("tool_usages")]
    public List<ToolUsage> ToolUsages { get; set; } = [];

    /// <summary>
    /// Whether this response used any tools.
    /// </summary>
    [JsonIgnore]
    public bool UsedTools => ToolUsages.Count > 0;
}

/// <summary>
/// Stage 2: Model's ranking of anonymized peer responses.
/// </summary>
public class Stage2Ranking
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("ranking")]
    public string Ranking { get; set; } = string.Empty;

    [JsonPropertyName("parsed_ranking")]
    public List<string> ParsedRanking { get; set; } = [];
}

/// <summary>
/// Stage 3: Chairman's final synthesized response.
/// </summary>
public class Stage3Response
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Tool usages during synthesis, if any.
    /// </summary>
    [JsonPropertyName("tool_usages")]
    public List<ToolUsage> ToolUsages { get; set; } = [];

    /// <summary>
    /// Whether this response used any tools.
    /// </summary>
    [JsonIgnore]
    public bool UsedTools => ToolUsages.Count > 0;

    /// <summary>
    /// Chart recommendation for visualizing query results (V1 "Classic").
    /// Propagated from Stage 1 tool usages.
    /// </summary>
    [JsonPropertyName("chart")]
    public ChartRecommendation? Chart { get; set; }

    /// <summary>
    /// Deterministically-classified recommendation (V2 "Studio"), propagated alongside <see cref="Chart"/>.
    /// </summary>
    [JsonPropertyName("studio_chart")]
    public ChartRecommendation? StudioChart { get; set; }
}

/// <summary>
/// Aggregate ranking calculated across all peer evaluations.
/// </summary>
public class AggregateRanking
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("average_rank")]
    public double AverageRank { get; set; }

    [JsonPropertyName("rankings_count")]
    public int RankingsCount { get; set; }
}

/// <summary>
/// Metadata returned alongside council results.
/// </summary>
public class CouncilMetadata
{
    /// <summary>
    /// Mapping from anonymous labels (e.g., "Response A") to model identifiers.
    /// </summary>
    [JsonPropertyName("label_to_model")]
    public Dictionary<string, string> LabelToModel { get; set; } = [];

    /// <summary>
    /// Aggregate rankings sorted by average position (lower is better).
    /// </summary>
    [JsonPropertyName("aggregate_rankings")]
    public List<AggregateRanking> AggregateRankings { get; set; } = [];

    /// <summary>
    /// Whether company data tools were available for this query.
    /// </summary>
    [JsonPropertyName("tools_enabled")]
    public bool ToolsEnabled { get; set; }

    /// <summary>
    /// Total number of tool invocations across all stages.
    /// </summary>
    [JsonPropertyName("total_tool_calls")]
    public int TotalToolCalls { get; set; }
}
