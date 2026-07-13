using System.Text.Json.Serialization;

namespace SamurAICouncil.Core.Models;

/// <summary>
/// Lightweight conversation projection for the sidebar list (no messages).
/// </summary>
public record ConversationSummary(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime? UpdatedAt);
