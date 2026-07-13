using System.Text.Json.Serialization;

namespace SamurAICouncil.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "role")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
public abstract class Message
{
    // Serialized on the wire via the polymorphic "role" discriminator above;
    // ignore the property itself to avoid a discriminator/property name clash.
    [JsonIgnore]
    public abstract string Role { get; }
}

public class UserMessage : Message
{
    [JsonIgnore]
    public override string Role => "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class AssistantMessage : Message
{
    [JsonIgnore]
    public override string Role => "assistant";

    [JsonPropertyName("stage1")]
    public List<Stage1Response>? Stage1 { get; set; }

    [JsonPropertyName("stage2")]
    public List<Stage2Ranking>? Stage2 { get; set; }

    [JsonPropertyName("stage3")]
    public Stage3Response? Stage3 { get; set; }

    [JsonPropertyName("metadata")]
    public CouncilMetadata? Metadata { get; set; }

    [JsonPropertyName("loading")]
    public LoadingState? Loading { get; set; }
}

public class LoadingState
{
    [JsonPropertyName("stage1")]
    public bool Stage1 { get; set; }

    [JsonPropertyName("stage2")]
    public bool Stage2 { get; set; }

    [JsonPropertyName("stage3")]
    public bool Stage3 { get; set; }
}
