using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using OpenAI;
using Anthropic.Models.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// LLM service implementation using Semantic Kernel for OpenAI/Google and official Anthropic SDK for Anthropic.
/// Kernels are created per-call and disposed after use. The Anthropic client is cached for reuse.
/// </summary>
public sealed class SemanticKernelLlmService : ILlmService, IDisposable
{
    private readonly ILogger<SemanticKernelLlmService> _logger;
    private readonly LlmApiKeysConfiguration _apiKeys;
    private readonly HashSet<string> _enabledProviders;

    private readonly Lazy<AnthropicClient>? _anthropicClient;

    private bool _disposed;

    public SemanticKernelLlmService(
        ILogger<SemanticKernelLlmService> logger,
        IOptions<LlmApiKeysConfiguration> apiKeysOptions,
        IOptions<CouncilConfiguration> councilOptions)
    {
        _logger = logger;
        _apiKeys = apiKeysOptions.Value;

        // Collect all providers used in the council configuration
        var council = councilOptions.Value;
        _enabledProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in council.CouncilModels)
        {
            _enabledProviders.Add(model.Provider);
        }

        if (council.ChairmanModel != null)
        {
            _enabledProviders.Add(council.ChairmanModel.Provider);
        }

        if (council.TitleGenerationModel != null)
        {
            _enabledProviders.Add(council.TitleGenerationModel.Provider);
        }

        // Validate API keys for all used providers at startup
        _apiKeys.ValidateForProviders(_enabledProviders);

        // Initialize lazy Anthropic client (HttpClient wrapper benefits from reuse)
        if (_enabledProviders.Contains("anthropic"))
        {
            _anthropicClient = new Lazy<AnthropicClient>(CreateAnthropicClient, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        _logger.LogInformation(
            "SemanticKernelLlmService initialized for providers: {Providers}",
            string.Join(", ", _enabledProviders));
    }

    public async Task<string?> QueryModelAsync(
        ModelConfiguration model,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = model.Provider.ToLowerInvariant() switch
            {
                "openai" => await QueryOpenAiAsync(model.ModelId, messages, model.Temperature, cancellationToken),
                "anthropic" => await QueryAnthropicAsync(model.ModelId, messages, cancellationToken),
                "google" => await QueryGoogleAsync(model.ModelId, messages, cancellationToken),
                _ => throw new ArgumentException($"Unknown provider: {model.Provider}")
            };

            stopwatch.Stop();
            _logger.LogInformation(
                "QueryModelAsync completed for {Model} in {ElapsedMs}ms",
                model.FullModelId, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex,
                "Failed to query model {Model} after {ElapsedMs}ms",
                model.FullModelId, stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    public async Task<Dictionary<string, string?>> QueryModelsParallelAsync(
        IEnumerable<ModelConfiguration> models,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var messagesList = messages.ToList();
        var modelsList = models.ToList();

        _logger.LogInformation(
            "QueryModelsParallelAsync starting for {ModelCount} models: {Models}",
            modelsList.Count, string.Join(", ", modelsList.Select(m => m.FullModelId)));

        var tasks = modelsList.Select(model =>
        {
            var queryTask = QueryModelAsync(model, messagesList, cancellationToken);
            return (model.FullModelId, queryTask);
        }).ToList();

        await Task.WhenAll(tasks.Select(t => t.queryTask));

        var results = tasks.Select(t =>
            (t.FullModelId, response: t.queryTask.Result));
        
        stopwatch.Stop();
        _logger.LogInformation(
            "QueryModelsParallelAsync completed for {ModelCount} models in {ElapsedMs}ms (wall clock)",
            modelsList.Count, stopwatch.ElapsedMilliseconds);

        return results.ToDictionary(r => r.FullModelId, r => r.response);
    }

    public async Task<LlmQueryResult> QueryModelWithToolsAsync(
        ModelConfiguration model,
        IEnumerable<ChatMessage> messages,
        IEnumerable<ILlmTool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var toolsList = tools?.ToList() ?? [];

        try
        {
            var result = model.Provider.ToLowerInvariant() switch
            {
                "openai" => await QueryOpenAiWithToolsAsync(model.ModelId, messages, toolsList, model.Temperature, cancellationToken),
                "anthropic" => await QueryAnthropicWithToolsAsync(model.ModelId, messages, toolsList, cancellationToken),
                "google" => await QueryGoogleWithToolsAsync(model.ModelId, messages, toolsList, cancellationToken),
                _ => throw new ArgumentException($"Unknown provider: {model.Provider}")
            };

            stopwatch.Stop();
            _logger.LogInformation(
                "QueryModelWithToolsAsync completed for {Model} in {ElapsedMs}ms, used {ToolCount} tools",
                model.FullModelId, stopwatch.ElapsedMilliseconds, result.ToolUsages.Count);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex,
                "Failed to query model with tools {Model} after {ElapsedMs}ms",
                model.FullModelId, stopwatch.ElapsedMilliseconds);
            return new LlmQueryResult { Content = null };
        }
    }

    private Kernel CreateOpenAiKernel(string modelId)
    {
        _logger.LogDebug("Creating OpenAI Kernel for model {ModelId}", modelId);
        var builder = Kernel.CreateBuilder();
        AddOpenAiChatCompletion(builder, modelId);
        return builder.Build();
    }

    /// <summary>
    /// Registers OpenAI chat completion on the kernel builder, honoring an optional
    /// custom endpoint (e.g., a LiteLLM/Azure OpenAI-compatible gateway).
    /// </summary>
    private void AddOpenAiChatCompletion(IKernelBuilder builder, string modelId)
    {
        if (!string.IsNullOrWhiteSpace(_apiKeys.OpenAIEndpoint))
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(_apiKeys.OpenAIEndpoint) };
            if (_apiKeys.OpenAINetworkTimeoutSeconds is { } timeoutSeconds)
            {
                options.NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            }
            if (_apiKeys.OpenAIMaxRetries is { } maxRetries)
            {
                options.RetryPolicy = new ClientRetryPolicy(maxRetries: maxRetries);
            }
            var client = new OpenAIClient(new ApiKeyCredential(_apiKeys.OpenAI!), options);
            builder.AddOpenAIChatCompletion(modelId, client);
        }
        else
        {
            builder.AddOpenAIChatCompletion(modelId, _apiKeys.OpenAI!);
        }
    }

    private Kernel CreateGoogleKernel(string modelId)
    {
        _logger.LogDebug("Creating Google Kernel for model {ModelId}", modelId);
#pragma warning disable SKEXP0070 // Google connector is experimental
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion(modelId, _apiKeys.Google!);
        return builder.Build();
#pragma warning restore SKEXP0070
    }

    private AnthropicClient CreateAnthropicClient()
    {
        _logger.LogDebug("Creating Anthropic client");
        return new AnthropicClient { APIKey = _apiKeys.Anthropic! };
    }

    private async Task<string?> QueryOpenAiAsync(
        string modelId,
        IEnumerable<ChatMessage> messages,
        double? temperature,
        CancellationToken cancellationToken)
    {
        if (!_enabledProviders.Contains("openai"))
        {
            throw new InvalidOperationException("OpenAI provider is not configured");
        }

        var stopwatch = Stopwatch.StartNew();
        var kernel = CreateOpenAiKernel(modelId);
        var kernelCreationMs = stopwatch.ElapsedMilliseconds;

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = ConvertToChatHistory(messages);
            // Only construct execution settings when a temperature override is configured, so the
            // default profile (no Temperature set) makes byte-for-byte the same request as before.
            var settings = temperature is { } t ? new OpenAIPromptExecutionSettings { Temperature = t } : null;

            stopwatch.Restart();
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                cancellationToken: cancellationToken);
            var apiCallMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "OpenAI query for {ModelId}: KernelCreation={KernelMs}ms, ApiCall={ApiMs}ms",
                modelId, kernelCreationMs, apiCallMs);

            return response.Content;
        }
        finally
        {
            DisposeKernel(kernel);
        }
    }

    private async Task<string?> QueryAnthropicAsync(
        string modelId,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        if (_anthropicClient == null)
        {
            throw new InvalidOperationException("Anthropic provider is not configured");
        }

        var stopwatch = Stopwatch.StartNew();
        var anthropicClient = _anthropicClient.Value;

        var messagesList = messages.ToList();
        var systemMessage = messagesList.FirstOrDefault(m => m.Role == "system");
        var conversationMessages = messagesList
            .Where(m => m.Role != "system")
            .Select(m => new MessageParam
            {
                Role = m.Role == "user" ? Role.User : Role.Assistant,
                Content = m.Content
            })
            .ToList();

        // Build request - only include System when we have a system message
        MessageCreateParams request;
        if (systemMessage != null)
        {
            request = new MessageCreateParams
            {
                Model = modelId,
                MaxTokens = 4096,
                Messages = conversationMessages,
                System = systemMessage.Content
            };
        }
        else
        {
            request = new MessageCreateParams
            {
                Model = modelId,
                MaxTokens = 4096,
                Messages = conversationMessages
            };
        }

        var requestPrepMs = stopwatch.ElapsedMilliseconds;

        stopwatch.Restart();
        var response = await anthropicClient.Messages.Create(request, cancellationToken: cancellationToken);
        var apiCallMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Anthropic query for {ModelId}: RequestPrep={PrepMs}ms, ApiCall={ApiMs}ms",
            modelId, requestPrepMs, apiCallMs);

        // Extract text content from response blocks
        return string.Join("", response.Content
            .Where(block => block.Value is TextBlock)
            .Select(block => block.Value as TextBlock)
            .Select(textBlock => textBlock?.Text ?? ""));
    }

    private async Task<string?> QueryGoogleAsync(
        string modelId,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!_enabledProviders.Contains("google"))
        {
            throw new InvalidOperationException("Google provider is not configured");
        }

        var stopwatch = Stopwatch.StartNew();
        var kernel = CreateGoogleKernel(modelId);
        var kernelCreationMs = stopwatch.ElapsedMilliseconds;

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = ConvertToChatHistory(messages);

            stopwatch.Restart();
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);
            var apiCallMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Google query for {ModelId}: KernelCreation={KernelMs}ms, ApiCall={ApiMs}ms",
                modelId, kernelCreationMs, apiCallMs);

            return response.Content;
        }
        finally
        {
            DisposeKernel(kernel);
        }
    }

    private void DisposeKernel(Kernel kernel)
    {
        // Kernel itself doesn't implement IDisposable, but its services might
        foreach (var service in kernel.Services.GetServices<object>())
        {
            if (service is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                    _logger.LogDebug("Disposed kernel service: {ServiceType}", service.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispose kernel service: {ServiceType}", service.GetType().Name);
                }
            }
        }
    }

    private static ChatHistory ConvertToChatHistory(IEnumerable<ChatMessage> messages)
    {
        var history = new ChatHistory();

        foreach (var message in messages)
        {
            switch (message.Role.ToLowerInvariant())
            {
                case "system":
                    history.AddSystemMessage(message.Content);
                    break;
                case "user":
                    history.AddUserMessage(message.Content);
                    break;
                case "assistant":
                    history.AddAssistantMessage(message.Content);
                    break;
            }
        }

        return history;
    }

    #region Tool-enabled queries

    private async Task<LlmQueryResult> QueryOpenAiWithToolsAsync(
        string modelId,
        IEnumerable<ChatMessage> messages,
        List<ILlmTool> tools,
        double? temperature,
        CancellationToken cancellationToken)
    {
        if (!_enabledProviders.Contains("openai"))
        {
            throw new InvalidOperationException("OpenAI provider is not configured");
        }

        var stopwatch = Stopwatch.StartNew();
        var toolUsages = new List<ToolUsage>();

        // Create kernel with OpenAI and register tools as plugins
        var builder = Kernel.CreateBuilder();
        AddOpenAiChatCompletion(builder, modelId);
        var kernel = builder.Build();

        // Create wrapper functions for each tool and register them
        foreach (var tool in tools)
        {
            var toolCapture = tool; // Capture for closure
            var function = KernelFunctionFactory.CreateFromMethod(
                async (string query) =>
                {
                    var inputElement = JsonSerializer.SerializeToElement(new { query });
                    var result = await toolCapture.ExecuteAsync(inputElement, cancellationToken);

                    // Track tool usage
                    var toolUsage = new ToolUsage
                    {
                        ToolName = toolCapture.Name,
                        Input = JsonSerializer.Serialize(new { query }),
                        Output = result,
                        Chart = ExtractChartFromToolResult(result),
                        StudioChart = ExtractChartFromToolResult(result, "studioChart")
                    };
                    toolUsages.Add(toolUsage);

                    _logger.LogInformation("OpenAI tool {ToolName} executed successfully", toolCapture.Name);
                    return result;
                },
                tool.Name,
                tool.Description,
                [new KernelParameterMetadata("query") { Description = "Natural language question", IsRequired = true }]
            );

            kernel.Plugins.AddFromFunctions("Tools", [function]);
        }

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = ConvertToChatHistory(messages);

            // Enable auto function calling
#pragma warning disable SKEXP0001
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = temperature
            };
#pragma warning restore SKEXP0001

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                kernel,
                cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "OpenAI tool query for {ModelId} completed in {ElapsedMs}ms with {ToolCount} tool calls",
                modelId, stopwatch.ElapsedMilliseconds, toolUsages.Count);

            return new LlmQueryResult
            {
                Content = response.Content,
                ToolUsages = toolUsages
            };
        }
        finally
        {
            DisposeKernel(kernel);
        }
    }

    private async Task<LlmQueryResult> QueryAnthropicWithToolsAsync(
        string modelId,
        IEnumerable<ChatMessage> messages,
        List<ILlmTool> tools,
        CancellationToken cancellationToken)
    {
        if (_anthropicClient == null)
        {
            throw new InvalidOperationException("Anthropic provider is not configured");
        }

        var stopwatch = Stopwatch.StartNew();
        var anthropicClient = _anthropicClient.Value;
        var toolUsages = new List<ToolUsage>();

        // Convert ILlmTool to Anthropic Tool format (cast to ToolUnion via implicit conversion)
        var anthropicTools = tools.Select(t => (ToolUnion)new Tool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = new InputSchema
            {
                Properties = t.InputSchema.TryGetProperty("properties", out var props)
                    ? props.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                    : new Dictionary<string, JsonElement>(),
                Required = t.InputSchema.TryGetProperty("required", out var req)
                    ? req.EnumerateArray().Select(e => e.GetString()!).ToList()
                    : []
            }
        }).ToList();

        // Build initial messages
        var messagesList = messages.ToList();
        var systemMessage = messagesList.FirstOrDefault(m => m.Role == "system");
        var conversationMessages = messagesList
            .Where(m => m.Role != "system")
            .Select(m => new MessageParam
            {
                Role = m.Role == "user" ? Role.User : Role.Assistant,
                Content = m.Content
            })
            .ToList();

        const int maxToolIterations = 5;
        var iteration = 0;

        while (iteration < maxToolIterations)
        {
            iteration++;

            // Build request - set all properties in object initializer since System is init-only
            MessageCreateParams request;
            if (systemMessage != null)
            {
                request = new MessageCreateParams
                {
                    Model = modelId,
                    MaxTokens = 4096,
                    Messages = conversationMessages,
                    Tools = anthropicTools.Count > 0 ? anthropicTools : null,
                    System = systemMessage.Content
                };
            }
            else
            {
                request = new MessageCreateParams
                {
                    Model = modelId,
                    MaxTokens = 4096,
                    Messages = conversationMessages,
                    Tools = anthropicTools.Count > 0 ? anthropicTools : null
                };
            }

            var response = await anthropicClient.Messages.Create(request, cancellationToken: cancellationToken);

            // Check if Claude wants to use a tool
            var hasToolUse = false;
            var toolResults = new List<ContentBlockParam>();
            var responseContentBlocks = new List<ContentBlockParam>();

            foreach (var contentBlock in response.Content)
            {
                // Store original block for assistant message replay using JSON constructor
                responseContentBlocks.Add(new ContentBlockParam(contentBlock.Json));

                if (contentBlock.TryPickToolUse(out var toolUse))
                {
                    hasToolUse = true;
                    _logger.LogInformation("Model requested tool: {ToolName}", toolUse.Name);

                    // Find the matching tool
                    var tool = tools.FirstOrDefault(t => t.Name == toolUse.Name);
                    string toolResult;

                    if (tool != null)
                    {
                        try
                        {
                            var inputJson = JsonSerializer.Serialize(toolUse.Input);
                            // Convert Dictionary<string, JsonElement> to JsonElement for tool execution
                            var inputElement = JsonSerializer.SerializeToElement(toolUse.Input);
                            toolResult = await tool.ExecuteAsync(inputElement, cancellationToken);

                            toolUsages.Add(new ToolUsage
                            {
                                ToolName = toolUse.Name,
                                Input = inputJson,
                                Output = toolResult,
                                Chart = ExtractChartFromToolResult(toolResult),
                                StudioChart = ExtractChartFromToolResult(toolResult, "studioChart")
                            });

                            _logger.LogInformation("Tool {ToolName} executed successfully", toolUse.Name);
                        }
                        catch (Exception ex)
                        {
                            toolResult = JsonSerializer.Serialize(new { error = ex.Message });
                            _logger.LogWarning(ex, "Tool {ToolName} execution failed", toolUse.Name);
                        }
                    }
                    else
                    {
                        toolResult = JsonSerializer.Serialize(new { error = $"Unknown tool: {toolUse.Name}" });
                        _logger.LogWarning("Unknown tool requested: {ToolName}", toolUse.Name);
                    }

                    toolResults.Add(new ToolResultBlockParam(toolUse.ID)
                    {
                        Content = toolResult
                    });
                }
            }

            // If no tool use, we're done - extract text response
            if (!hasToolUse)
            {
                var textContent = string.Join("", response.Content
                    .Where(block => block.Value is TextBlock)
                    .Select(block => block.Value as TextBlock)
                    .Select(textBlock => textBlock?.Text ?? ""));

                stopwatch.Stop();
                _logger.LogInformation(
                    "Anthropic tool query for {ModelId} completed in {ElapsedMs}ms with {ToolCount} tool calls",
                    modelId, stopwatch.ElapsedMilliseconds, toolUsages.Count);

                return new LlmQueryResult
                {
                    Content = textContent,
                    ToolUsages = toolUsages
                };
            }

            // Add assistant message with tool use - use the content blocks
            conversationMessages.Add(new MessageParam
            {
                Role = Role.Assistant,
                Content = new MessageParamContent(responseContentBlocks, null)
            });

            // Add tool results as user message
            conversationMessages.Add(new MessageParam
            {
                Role = Role.User,
                Content = new MessageParamContent(toolResults, null)
            });
        }

        // Max iterations reached
        _logger.LogWarning("Max tool iterations ({Max}) reached for model {ModelId}", maxToolIterations, modelId);
        return new LlmQueryResult
        {
            Content = "I apologize, but I couldn't complete the request within the allowed number of tool calls.",
            ToolUsages = toolUsages
        };
    }

    private async Task<LlmQueryResult> QueryGoogleWithToolsAsync(
        string modelId,
        IEnumerable<ChatMessage> messages,
        List<ILlmTool> tools,
        CancellationToken cancellationToken)
    {
        if (!_enabledProviders.Contains("google"))
        {
            throw new InvalidOperationException("Google provider is not configured");
        }

        var stopwatch = Stopwatch.StartNew();
        var toolUsages = new List<ToolUsage>();

        // Create kernel with Google Gemini and register tools as plugins
        var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070 // Google connector is experimental
        builder.AddGoogleAIGeminiChatCompletion(modelId, _apiKeys.Google!);
#pragma warning restore SKEXP0070
        var kernel = builder.Build();

        // Create wrapper functions for each tool and register them
        foreach (var tool in tools)
        {
            var toolCapture = tool; // Capture for closure
            var function = KernelFunctionFactory.CreateFromMethod(
                async (string query) =>
                {
                    var inputElement = JsonSerializer.SerializeToElement(new { query });
                    var result = await toolCapture.ExecuteAsync(inputElement, cancellationToken);

                    // Track tool usage
                    toolUsages.Add(new ToolUsage
                    {
                        ToolName = toolCapture.Name,
                        Input = JsonSerializer.Serialize(new { query }),
                        Output = result,
                        Chart = ExtractChartFromToolResult(result),
                        StudioChart = ExtractChartFromToolResult(result, "studioChart")
                    });

                    _logger.LogInformation("Google tool {ToolName} executed successfully", toolCapture.Name);
                    return result;
                },
                tool.Name,
                tool.Description,
                [new KernelParameterMetadata("query") { Description = "Natural language question", IsRequired = true }]
            );

            kernel.Plugins.AddFromFunctions("Tools", [function]);
        }

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = ConvertToChatHistory(messages);

            // Enable auto function calling for Gemini
#pragma warning disable SKEXP0070 // Google connector is experimental
            var settings = new GeminiPromptExecutionSettings
            {
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
            };
#pragma warning restore SKEXP0070

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                kernel,
                cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Google tool query for {ModelId} completed in {ElapsedMs}ms with {ToolCount} tool calls",
                modelId, stopwatch.ElapsedMilliseconds, toolUsages.Count);

            return new LlmQueryResult
            {
                Content = response.Content,
                ToolUsages = toolUsages
            };
        }
        finally
        {
            DisposeKernel(kernel);
        }
    }

    #endregion

    /// <summary>
    /// Extracts chart recommendation from tool result JSON if present.
    /// </summary>
    private static ChartRecommendation? ExtractChartFromToolResult(string result, string property = "chart")
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(result);
            if (doc.RootElement.TryGetProperty(property, out var chartElement) &&
                chartElement.ValueKind != JsonValueKind.Null)
            {
                return JsonSerializer.Deserialize<ChartRecommendation>(
                    chartElement.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch (JsonException)
        {
            // Result is not valid JSON or doesn't contain chart
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose Anthropic client if it was created and is disposable
            if (_anthropicClient?.IsValueCreated == true &&
                _anthropicClient.Value is IDisposable disposableClient)
            {
                disposableClient.Dispose();
                _logger.LogDebug("Disposed Anthropic client");
            }

            // Note: Kernels are created per-call and disposed immediately after use
        }

        _disposed = true;
    }
}
