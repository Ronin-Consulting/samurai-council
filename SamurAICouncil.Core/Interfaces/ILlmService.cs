using SamurAICouncil.Core.Configuration;

namespace SamurAICouncil.Core.Interfaces;

/// <summary>
/// Service for querying LLM models.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Query a single model with the given messages.
    /// </summary>
    /// <param name="model">The model configuration to query.</param>
    /// <param name="messages">The chat messages to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model's response content, or null if the query failed.</returns>
    Task<string?> QueryModelAsync(
        ModelConfiguration model,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query a single model with the given messages and optional tools.
    /// </summary>
    /// <param name="model">The model configuration to query.</param>
    /// <param name="messages">The chat messages to send.</param>
    /// <param name="tools">Optional tools available to the model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the response and any tool usage.</returns>
    Task<LlmQueryResult> QueryModelWithToolsAsync(
        ModelConfiguration model,
        IEnumerable<ChatMessage> messages,
        IEnumerable<ILlmTool>? tools = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query multiple models in parallel with the same messages.
    /// </summary>
    /// <param name="models">The models to query.</param>
    /// <param name="messages">The chat messages to send to each model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping model full ID to response (null if failed).</returns>
    Task<Dictionary<string, string?>> QueryModelsParallelAsync(
        IEnumerable<ModelConfiguration> models,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A simple chat message for LLM queries.
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;

    public static ChatMessage User(string content) => new() { Role = "user", Content = content };
    public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
}
