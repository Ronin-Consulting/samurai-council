namespace SamurAICouncil.Core.Configuration;

/// <summary>
/// Configuration for LLM provider API keys.
/// Keys can be set via appsettings.json or environment variables (e.g., LlmApiKeys__OpenAI).
/// </summary>
public class LlmApiKeysConfiguration
{
    public const string SectionName = "LlmApiKeys";

    /// <summary>
    /// OpenAI API key for GPT models.
    /// </summary>
    public string? OpenAI { get; set; }

    /// <summary>
    /// Optional custom endpoint for the OpenAI-compatible API (e.g., a LiteLLM/Azure gateway).
    /// When set, all "openai" provider calls are routed here instead of api.openai.com.
    /// </summary>
    public string? OpenAIEndpoint { get; set; }

    /// <summary>
    /// Anthropic API key for Claude models.
    /// </summary>
    public string? Anthropic { get; set; }

    /// <summary>
    /// Google API key for Gemini models.
    /// </summary>
    public string? Google { get; set; }

    /// <summary>
    /// Optional override for the OpenAI SDK's per-attempt network timeout (default: 100s,
    /// the SDK's own built-in default). Only applied when set — an unset value preserves
    /// the SDK's default network timeout for the default (FortyAU) profile.
    /// </summary>
    public int? OpenAINetworkTimeoutSeconds { get; set; }

    /// <summary>
    /// Optional override for the OpenAI SDK's built-in retry count (default: 3 retries / 4
    /// total attempts). Lower this for less-reliable OpenAI-compatible endpoints so a stuck
    /// call fails fast instead of retrying a dead connection repeatedly (e.g. 4 attempts at
    /// the default ~100s network timeout can otherwise take ~400s to give up). Only applied
    /// when set.
    /// </summary>
    public int? OpenAIMaxRetries { get; set; }

    /// <summary>
    /// Validates that required API keys are present for the given providers.
    /// </summary>
    /// <param name="providers">List of provider names to validate (e.g., "openai", "anthropic", "google").</param>
    /// <exception cref="InvalidOperationException">Thrown when a required API key is missing.</exception>
    public void ValidateForProviders(IEnumerable<string> providers)
    {
        var missingKeys = new List<string>();

        foreach (var provider in providers.Distinct().Select(p => p.ToLowerInvariant()))
        {
            var isMissing = provider switch
            {
                "openai" => string.IsNullOrWhiteSpace(OpenAI),
                "anthropic" => string.IsNullOrWhiteSpace(Anthropic),
                "google" => string.IsNullOrWhiteSpace(Google),
                _ => false // Unknown providers are ignored
            };

            if (isMissing)
            {
                missingKeys.Add(provider);
            }
        }

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing API keys for providers: {string.Join(", ", missingKeys)}. " +
                $"Set them via configuration (LlmApiKeys section) or environment variables " +
                $"(e.g., LlmApiKeys__OpenAI, LlmApiKeys__Anthropic, LlmApiKeys__Google).");
        }
    }
}
