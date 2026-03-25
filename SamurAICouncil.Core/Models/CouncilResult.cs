using System.Text.Json.Serialization;

namespace SamurAICouncil.Core.Models;

/// <summary>
/// Complete result from running the 3-stage council process.
/// </summary>
public class CouncilResult
{
    [JsonPropertyName("stage1")]
    public List<Stage1Response> Stage1 { get; set; } = [];

    [JsonPropertyName("stage2")]
    public List<Stage2Ranking> Stage2 { get; set; } = [];

    [JsonPropertyName("stage3")]
    public Stage3Response? Stage3 { get; set; }

    [JsonPropertyName("metadata")]
    public CouncilMetadata Metadata { get; set; } = new();
}
