namespace SamurAICouncil.Core.Configuration;

/// <summary>
/// Configuration for all LLM providers.
/// </summary>
public class LlmProvidersConfiguration
{
    public const string SectionName = "LlmProviders";

    public OpenAIConfiguration? OpenAI { get; set; }
    public AnthropicConfiguration? Anthropic { get; set; }
    public GoogleConfiguration? Google { get; set; }
}

/// <summary>
/// OpenAI provider configuration.
/// </summary>
public class OpenAIConfiguration
{
    /// <summary>
    /// OpenAI API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional organization ID.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Optional custom endpoint URL (for Azure OpenAI or compatible APIs).
    /// </summary>
    public string? Endpoint { get; set; }
}

/// <summary>
/// Anthropic provider configuration.
/// </summary>
public class AnthropicConfiguration
{
    /// <summary>
    /// Anthropic API key.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Google (Gemini) provider configuration.
/// </summary>
public class GoogleConfiguration
{
    /// <summary>
    /// Google AI API key.
    /// </summary>
    public string? ApiKey { get; set; }
}
