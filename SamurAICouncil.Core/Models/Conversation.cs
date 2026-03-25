using System.Text.Json.Serialization;

namespace SamurAICouncil.Core.Models;

public class Conversation
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "New Conversation";

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = [];
}

public class ConversationMetadata
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "New Conversation";

    [JsonPropertyName("message_count")]
    public int MessageCount { get; set; }
}
