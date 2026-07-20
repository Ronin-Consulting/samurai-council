namespace SamurAICouncil.Core.Configuration;

/// <summary>
/// Configuration for the LLM Council.
/// </summary>
public class CouncilConfiguration
{
    public const string SectionName = "Council";

    /// <summary>
    /// List of models that form the council. Each model participates in Stage 1 and Stage 2.
    /// Format: "provider:model" (e.g., "openai:gpt-4", "anthropic:claude-3-sonnet")
    /// </summary>
    public List<ModelConfiguration> CouncilModels { get; set; } = [];

    /// <summary>
    /// The model that serves as chairman for Stage 3 synthesis.
    /// </summary>
    public ModelConfiguration? ChairmanModel { get; set; }

    /// <summary>
    /// Model used for generating conversation titles (should be fast and cheap).
    /// </summary>
    public ModelConfiguration? TitleGenerationModel { get; set; }
}

/// <summary>
/// Configuration for a single LLM model.
/// </summary>
public class ModelConfiguration
{
    /// <summary>
    /// The provider (e.g., "openai", "anthropic", "google").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The model identifier (e.g., "gpt-4", "claude-3-sonnet", "gemini-pro").
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the model (optional, defaults to Provider/ModelId).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional sampling temperature for this model's role (0.0-2.0 for OpenAI-compatible
    /// endpoints; lower = more deterministic/focused, higher = more varied). Only applied
    /// when set — an unset value uses the provider/model's own default. Currently only
    /// honored on the "openai" provider path (used for every role via the FortyAU gateway or
    /// an OpenRouter-style OpenAI-compatible endpoint); not wired into the Anthropic/Google
    /// paths, which aren't used for open-weight models in this project.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Gets the full model identifier in "provider/model" format.
    /// </summary>
    public string FullModelId => $"{Provider}/{ModelId}";

    /// <summary>
    /// Gets the display name, falling back to the short model ID.
    /// </summary>
    public string GetDisplayName() => DisplayName ?? ModelId;
}
