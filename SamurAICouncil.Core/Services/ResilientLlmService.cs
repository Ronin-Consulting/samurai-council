using Microsoft.Extensions.Logging;
using Polly;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Resilience;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Resilient wrapper for ILlmService with Polly retry and circuit breaker policies.
/// </summary>
public class ResilientLlmService : ILlmService
{
    private readonly ILlmService _inner;
    private readonly AsyncPolicy _policy;
    private readonly ILogger<ResilientLlmService> _logger;

    public ResilientLlmService(ILlmService inner, ILogger<ResilientLlmService> logger)
        : this(inner, LlmResiliencePolicies.CreateCombinedPolicy(logger), logger)
    {
    }

    public ResilientLlmService(ILlmService inner, AsyncPolicy policy, ILogger<ResilientLlmService> logger)
    {
        _inner = inner;
        _policy = policy;
        _logger = logger;
    }

    public async Task<string?> QueryModelAsync(
        ModelConfiguration model,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _policy.ExecuteAsync(
                ct => _inner.QueryModelAsync(model, messages, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM query failed for model {Model} after all retries", model.FullModelId);
            return null;
        }
    }

    public async Task<LlmQueryResult> QueryModelWithToolsAsync(
        ModelConfiguration model,
        IEnumerable<ChatMessage> messages,
        IEnumerable<ILlmTool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _policy.ExecuteAsync(
                ct => _inner.QueryModelWithToolsAsync(model, messages, tools, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM query with tools failed for model {Model} after all retries", model.FullModelId);
            return new LlmQueryResult { Content = null };
        }
    }

    public async Task<Dictionary<string, string?>> QueryModelsParallelAsync(
        IEnumerable<ModelConfiguration> models,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var messagesList = messages.ToList();
        var modelsList = models.ToList();

        // Execute each model query with resilience policy
        var tasks = modelsList.Select(async model =>
        {
            var response = await QueryModelAsync(model, messagesList, cancellationToken);
            return (model.FullModelId, response);
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(r => r.FullModelId, r => r.response);
    }
}
