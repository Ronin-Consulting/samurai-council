using System.Text.Json;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Interfaces;

/// <summary>
/// Represents a tool that can be used by LLM models.
/// </summary>
public interface ILlmTool
{
    /// <summary>
    /// Unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON schema for the tool's input parameters.
    /// </summary>
    JsonElement InputSchema { get; }

    /// <summary>
    /// Executes the tool with the given input.
    /// </summary>
    /// <param name="input">JSON object containing the input parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the tool execution as a string.</returns>
    Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an LLM query that may include tool use.
/// </summary>
public class LlmQueryResult
{
    /// <summary>
    /// The final text response from the model.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Tools that were used during the query.
    /// </summary>
    public List<ToolUsage> ToolUsages { get; set; } = [];

    /// <summary>
    /// Whether tools were used to generate the response.
    /// </summary>
    public bool UsedTools => ToolUsages.Count > 0;
}

/// <summary>
/// Record of a tool being used during an LLM query.
/// </summary>
public class ToolUsage
{
    /// <summary>
    /// Name of the tool that was called.
    /// </summary>
    public required string ToolName { get; set; }

    /// <summary>
    /// Input provided to the tool.
    /// </summary>
    public required string Input { get; set; }

    /// <summary>
    /// Output from the tool execution.
    /// </summary>
    public required string Output { get; set; }

    /// <summary>
    /// Optional chart recommendation based on the tool output.
    /// </summary>
    public ChartRecommendation? Chart { get; set; }
}
