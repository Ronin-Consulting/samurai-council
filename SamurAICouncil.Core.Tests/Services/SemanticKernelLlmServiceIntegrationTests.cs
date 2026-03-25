using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

/// <summary>
/// Integration tests for SemanticKernelLlmService that make real API calls.
///
/// USAGE:
/// Set the following environment variables before running:
///   - OPENAI_API_KEY: Your OpenAI API key (for OpenAI tests)
///   - ANTHROPIC_API_KEY: Your Anthropic API key (for Anthropic tests)
///   - GOOGLE_API_KEY: Your Google AI API key (for Google tests)
///
/// Run specific provider tests:
///   dotnet test --filter "TestCategory=Integration&FullyQualifiedName~OpenAi"
///   dotnet test --filter "TestCategory=Integration&FullyQualifiedName~Anthropic"
///   dotnet test --filter "TestCategory=Integration&FullyQualifiedName~Google"
///
/// Run all integration tests:
///   dotnet test --filter "TestCategory=Integration" -- MSTest.Parallelize.Workers=1
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class SemanticKernelLlmServiceIntegrationTests
{
    private static string? OpenAiApiKey;
    private static string? AnthropicApiKey;
    private static string? GoogleApiKey;

    private Mock<ILogger<SemanticKernelLlmService>> _mockLogger = null!;

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        // Try to load .env file if it exists
        LoadEnvironmentVariables();

        // Get API keys from environment
        OpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        GoogleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    }

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = CreateConsoleLogger();
    }

    /// <summary>
    /// Creates a mock logger that outputs to the console for debugging integration tests.
    /// </summary>
    private static Mock<ILogger<SemanticKernelLlmService>> CreateConsoleLogger()
    {
        var mock = new Mock<ILogger<SemanticKernelLlmService>>();

        mock.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var logLevel = (LogLevel)invocation.Arguments[0];
                var state = invocation.Arguments[2];
                var exception = (Exception?)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                // Use reflection to call the formatter
                var formatterType = formatter.GetType();
                var invokeMethod = formatterType.GetMethod("Invoke");
                var message = invokeMethod?.Invoke(formatter, [state, exception]) as string;

                var levelPrefix = logLevel switch
                {
                    LogLevel.Trace => "[TRC]",
                    LogLevel.Debug => "[DBG]",
                    LogLevel.Information => "[INF]",
                    LogLevel.Warning => "[WRN]",
                    LogLevel.Error => "[ERR]",
                    LogLevel.Critical => "[CRT]",
                    _ => "[???]"
                };

                Console.WriteLine($"{levelPrefix} {message}");

                if (exception != null)
                {
                    Console.WriteLine($"      Exception: {exception.Message}");
                }
            }));

        mock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        return mock;
    }

    private static void LoadEnvironmentVariables()
    {
        // Look for .env file in various locations
        var envFilePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "env_vars"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "env_vars"), // Up to solution root
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "env_vars"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "env_vars")
        };

        foreach (var envFilePath in envFilePaths)
        {
            if (File.Exists(envFilePath))
            {
                Console.WriteLine($"Loading .env file from: {envFilePath}");

                foreach (var line in File.ReadAllLines(envFilePath))
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        // Only set if not already set (environment variables take precedence)
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }

                // Stop after loading the first .env file found
                break;
            }
        }
    }

    #region OpenAI Integration Tests

    [TestMethod]
    [TestCategory("OpenAI")]
    public async Task OpenAi_QueryModel_ReturnsResponse()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var model = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is 2 + 2? Answer with just the number.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "OpenAI should return a response");
        Console.WriteLine($"OpenAI Response: {response}");
        Assert.IsTrue(response.Contains("4"), "Response should contain the answer '4'");
    }

    [TestMethod]
    [TestCategory("OpenAI")]
    public async Task OpenAi_QueryModel_WithSystemMessage_ReturnsResponse()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var model = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are a helpful assistant that responds in exactly 3 words."),
            ChatMessage.User("Say hello to me.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "OpenAI should return a response");
        Console.WriteLine($"OpenAI Response: {response}");
    }

    [TestMethod]
    [TestCategory("OpenAI")]
    public async Task OpenAi_QueryModelsParallel_ReturnsMultipleResponses()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var models = new List<ModelConfiguration>
        {
            new() { Provider = "openai", ModelId = "gpt-4o-mini" },
            new() { Provider = "openai", ModelId = "gpt-4o" } // Same model twice for testing
        };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is 1 + 1? Answer with just the number.")
        };

        var responses = await service.QueryModelsParallelAsync(models, messages);

        Assert.AreEqual(2, responses.Count, "Should have 2 responses");
        foreach (var (modelId, response) in responses)
        {
            Console.WriteLine($"{modelId}: {response}");
            Assert.IsNotNull(response, $"Response for {modelId} should not be null");
        }
    }

    private SemanticKernelLlmService CreateServiceForOpenAi()
    {
        var apiKeys = new LlmApiKeysConfiguration { OpenAI = OpenAiApiKey };
        var council = new CouncilConfiguration
        {
            CouncilModels = [new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" }]
        };

        return new SemanticKernelLlmService(
            _mockLogger.Object,
            Options.Create(apiKeys),
            Options.Create(council));
    }

    #endregion

    #region Anthropic Integration Tests

    [TestMethod]
    [TestCategory("Anthropic")]
    public async Task Anthropic_QueryModel_ReturnsResponse()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is 2 + 2? Answer with just the number.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "Anthropic should return a response");
        Console.WriteLine($"Anthropic Response: {response}");
        Assert.IsTrue(response.Contains("4"), "Response should contain the answer '4'");
    }

    [TestMethod]
    [TestCategory("Anthropic")]
    public async Task Anthropic_QueryModel_WithSystemMessage_ReturnsResponse()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are a helpful assistant that responds in exactly 3 words."),
            ChatMessage.User("Say hello to me.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "Anthropic should return a response");
        Console.WriteLine($"Anthropic Response: {response}");
    }

    [TestMethod]
    [TestCategory("Anthropic")]
    public async Task Anthropic_QueryModel_MultiTurnConversation_ReturnsResponse()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("My name is Alice."),
            ChatMessage.Assistant("Nice to meet you, Alice!"),
            ChatMessage.User("What is my name? Answer with just the name.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "Anthropic should return a response");
        Console.WriteLine($"Anthropic Response: {response}");
        Assert.IsTrue(response.Contains("Alice"), "Response should remember the name 'Alice'");
    }

    private SemanticKernelLlmService CreateServiceForAnthropic()
    {
        var apiKeys = new LlmApiKeysConfiguration { Anthropic = AnthropicApiKey };
        var council = new CouncilConfiguration
        {
            CouncilModels = [new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" }]
        };

        return new SemanticKernelLlmService(
            _mockLogger.Object,
            Options.Create(apiKeys),
            Options.Create(council));
    }

    #endregion

    #region Google Integration Tests

    [TestMethod]
    [TestCategory("Google")]
    public async Task Google_QueryModel_ReturnsResponse()
    {
        SkipIfMissingApiKey(GoogleApiKey, "GOOGLE_API_KEY");

        var service = CreateServiceForGoogle();
        var model = new ModelConfiguration { Provider = "google", ModelId = "gemini-2.5-flash" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is 2 + 2? Answer with just the number.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "Google should return a response");
        Console.WriteLine($"Google Response: {response}");
        Assert.IsTrue(response.Contains("4"), "Response should contain the answer '4'");
    }

    [TestMethod]
    [TestCategory("Google")]
    public async Task Google_QueryModel_WithSystemMessage_ReturnsResponse()
    {
        SkipIfMissingApiKey(GoogleApiKey, "GOOGLE_API_KEY");

        var service = CreateServiceForGoogle();
        var model = new ModelConfiguration { Provider = "google", ModelId = "gemini-2.5-flash" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are a helpful assistant that responds in exactly 3 words."),
            ChatMessage.User("Say hello to me.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "Google should return a response");
        Console.WriteLine($"Google Response: {response}");
    }

    [TestMethod]
    [TestCategory("Google")]
    public async Task Google_QueryModel_GeminiPro_ReturnsResponse()
    {
        SkipIfMissingApiKey(GoogleApiKey, "GOOGLE_API_KEY");

        var service = CreateServiceForGoogle("gemini-2.5-pro");
        var model = new ModelConfiguration { Provider = "google", ModelId = "gemini-2.5-pro" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is the capital of France? Answer with just the city name.")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNotNull(response, "Google should return a response");
        Console.WriteLine($"Google Response: {response}");
        Assert.IsTrue(response.Contains("Paris"), "Response should contain 'Paris'");
    }

    private SemanticKernelLlmService CreateServiceForGoogle(string modelId = "gemini-2.5-flash")
    {
        var apiKeys = new LlmApiKeysConfiguration { Google = GoogleApiKey };
        var council = new CouncilConfiguration
        {
            CouncilModels = [new ModelConfiguration { Provider = "google", ModelId = modelId }]
        };

        return new SemanticKernelLlmService(
            _mockLogger.Object,
            Options.Create(apiKeys),
            Options.Create(council));
    }

    #endregion

    #region Multi-Provider Integration Tests

    [TestMethod]
    [TestCategory("MultiProvider")]
    public async Task MultiProvider_QueryModelsParallel_AllProvidersRespond()
    {
        var availableProviders = new List<string>();
        if (!string.IsNullOrWhiteSpace(OpenAiApiKey)) availableProviders.Add("openai");
        if (!string.IsNullOrWhiteSpace(AnthropicApiKey)) availableProviders.Add("anthropic");
        if (!string.IsNullOrWhiteSpace(GoogleApiKey)) availableProviders.Add("google");

        if (availableProviders.Count < 2)
        {
            Assert.Inconclusive("At least 2 API keys required for multi-provider test. " +
                $"Available: {string.Join(", ", availableProviders)}");
            return;
        }

        var service = CreateServiceForMultipleProviders(availableProviders);
        var models = GetModelsForProviders(availableProviders);
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is 3 + 3? Answer with just the number.")
        };

        var responses = await service.QueryModelsParallelAsync(models, messages);

        Console.WriteLine($"Queried {responses.Count} providers:");
        foreach (var (modelId, response) in responses)
        {
            Console.WriteLine($"  {modelId}: {response?.Substring(0, Math.Min(100, response?.Length ?? 0))}...");
        }

        var successfulResponses = responses.Count(r => r.Value != null);
        Assert.IsTrue(successfulResponses >= 2,
            $"Expected at least 2 successful responses, got {successfulResponses}");
    }

    private SemanticKernelLlmService CreateServiceForMultipleProviders(List<string> providers)
    {
        var apiKeys = new LlmApiKeysConfiguration
        {
            OpenAI = providers.Contains("openai") ? OpenAiApiKey : null,
            Anthropic = providers.Contains("anthropic") ? AnthropicApiKey : null,
            Google = providers.Contains("google") ? GoogleApiKey : null
        };

        var council = new CouncilConfiguration
        {
            CouncilModels = GetModelsForProviders(providers)
        };

        return new SemanticKernelLlmService(
            _mockLogger.Object,
            Options.Create(apiKeys),
            Options.Create(council));
    }

    private static List<ModelConfiguration> GetModelsForProviders(List<string> providers)
    {
        var models = new List<ModelConfiguration>();

        if (providers.Contains("openai"))
            models.Add(new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" });

        if (providers.Contains("anthropic"))
            models.Add(new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" });

        if (providers.Contains("google"))
            models.Add(new ModelConfiguration { Provider = "google", ModelId = "gemini-1.5-flash" });

        return models;
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    [TestCategory("OpenAI")]
    public async Task OpenAi_InvalidModel_ReturnsNull()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var model = new ModelConfiguration { Provider = "openai", ModelId = "nonexistent-model-xyz" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("Hello")
        };

        var response = await service.QueryModelAsync(model, messages);

        // The service should handle errors gracefully and return null
        Assert.IsNull(response, "Invalid model should return null (graceful failure)");
    }

    [TestMethod]
    [TestCategory("Anthropic")]
    public async Task Anthropic_InvalidModel_ReturnsNull()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "nonexistent-model-xyz" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("Hello")
        };

        var response = await service.QueryModelAsync(model, messages);

        Assert.IsNull(response, "Invalid model should return null (graceful failure)");
    }

    [TestMethod]
    public void MissingApiKey_ThrowsOnInitialization()
    {
        var apiKeys = new LlmApiKeysConfiguration
        {
            OpenAI = null, // Missing API key
            Anthropic = null,
            Google = null
        };

        var council = new CouncilConfiguration
        {
            CouncilModels = [new ModelConfiguration { Provider = "openai", ModelId = "gpt-4" }]
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new SemanticKernelLlmService(
                _mockLogger.Object,
                Options.Create(apiKeys),
                Options.Create(council)));

        Assert.IsTrue(exception.Message.Contains("Missing API keys"),
            "Exception message should mention missing API keys");
    }

    #endregion

    #region Tool Invocation Integration Tests

    [TestMethod]
    [TestCategory("OpenAI")]
    [TestCategory("ToolInvocation")]
    public async Task OpenAi_QueryModelWithTools_UsesToolWhenRequested()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var model = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" };
        var tool = new CalculatorTool();
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are a helpful assistant. Use the calculator tool when asked to perform calculations."),
            ChatMessage.User("What is 42 times 17? Use the calculator tool to compute this.")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, [tool]);

        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsNotNull(result.Content, "Content should not be null");
        Console.WriteLine($"OpenAI Tool Response: {result.Content}");
        Console.WriteLine($"Tools Used: {result.UsedTools}");

        foreach (var usage in result.ToolUsages)
        {
            Console.WriteLine($"  Tool: {usage.ToolName}, Input: {usage.Input}, Output: {usage.Output}");
        }

        Assert.IsTrue(result.UsedTools, "Tool should have been used");
        Assert.IsTrue(result.ToolUsages.Any(u => u.ToolName == "calculator"), "Calculator tool should have been called");
        Assert.IsTrue(result.Content.Contains("714"), "Response should contain the correct answer (714)");
    }

    [TestMethod]
    [TestCategory("OpenAI")]
    [TestCategory("ToolInvocation")]
    public async Task OpenAi_QueryModelWithTools_DoesNotUseToolWhenNotNeeded()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var model = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" };
        var tool = new CalculatorTool();
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is the capital of France? Answer with just the city name.")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, [tool]);

        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsNotNull(result.Content, "Content should not be null");
        Console.WriteLine($"OpenAI Response (no tool needed): {result.Content}");
        Console.WriteLine($"Tools Used: {result.UsedTools}");

        Assert.IsFalse(result.UsedTools, "Tool should NOT have been used for this question");
        Assert.IsTrue(result.Content.Contains("Paris"), "Response should contain 'Paris'");
    }

    [TestMethod]
    [TestCategory("Anthropic")]
    [TestCategory("ToolInvocation")]
    public async Task Anthropic_QueryModelWithTools_UsesToolWhenRequested()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" };
        var tool = new CalculatorTool();
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are a helpful assistant. Use the calculator tool when asked to perform calculations."),
            ChatMessage.User("What is 42 times 17? Use the calculator tool to compute this.")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, [tool]);

        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsNotNull(result.Content, "Content should not be null");
        Console.WriteLine($"Anthropic Tool Response: {result.Content}");
        Console.WriteLine($"Tools Used: {result.UsedTools}");

        foreach (var usage in result.ToolUsages)
        {
            Console.WriteLine($"  Tool: {usage.ToolName}, Input: {usage.Input}, Output: {usage.Output}");
        }

        Assert.IsTrue(result.UsedTools, "Tool should have been used");
        Assert.IsTrue(result.ToolUsages.Any(u => u.ToolName == "calculator"), "Calculator tool should have been called");
        Assert.IsTrue(result.Content.Contains("714"), "Response should contain the correct answer (714)");
    }

    [TestMethod]
    [TestCategory("Anthropic")]
    [TestCategory("ToolInvocation")]
    public async Task Anthropic_QueryModelWithTools_DoesNotUseToolWhenNotNeeded()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" };
        var tool = new CalculatorTool();
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is the capital of France? Answer with just the city name.")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, [tool]);

        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsNotNull(result.Content, "Content should not be null");
        Console.WriteLine($"Anthropic Response (no tool needed): {result.Content}");
        Console.WriteLine($"Tools Used: {result.UsedTools}");

        Assert.IsFalse(result.UsedTools, "Tool should NOT have been used for this question");
        Assert.IsTrue(result.Content.Contains("Paris"), "Response should contain 'Paris'");
    }

    [TestMethod]
    [TestCategory("OpenAI")]
    [TestCategory("ToolInvocation")]
    public async Task OpenAi_QueryModelWithTools_NoTools_ReturnsNormalResponse()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var model = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is 2 + 2? Answer with just the number.")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, tools: null);

        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsNotNull(result.Content, "Content should not be null");
        Console.WriteLine($"OpenAI Response (no tools provided): {result.Content}");

        Assert.IsFalse(result.UsedTools, "No tools should be recorded when none provided");
        Assert.IsTrue(result.Content.Contains("4"), "Response should contain '4'");
    }

    [TestMethod]
    [TestCategory("Anthropic")]
    [TestCategory("ToolInvocation")]
    public async Task Anthropic_QueryModelWithTools_NoTools_ReturnsNormalResponse()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" };
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What is 2 + 2? Answer with just the number.")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, tools: null);

        Assert.IsNotNull(result, "Result should not be null");
        Assert.IsNotNull(result.Content, "Content should not be null");
        Console.WriteLine($"Anthropic Response (no tools provided): {result.Content}");

        Assert.IsFalse(result.UsedTools, "No tools should be recorded when none provided");
        Assert.IsTrue(result.Content.Contains("4"), "Response should contain '4'");
    }

    [TestMethod]
    [TestCategory("OpenAI")]
    [TestCategory("ToolInvocation")]
    public async Task OpenAi_QueryModelWithTools_MultipleTools_SelectsCorrectTool()
    {
        SkipIfMissingApiKey(OpenAiApiKey, "OPENAI_API_KEY");

        var service = CreateServiceForOpenAi();
        var model = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4o-mini" };
        var tools = new ILlmTool[] { new CalculatorTool(), new WeatherTool() };
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You have access to a calculator and weather tools. Use the appropriate tool."),
            ChatMessage.User("What is 100 divided by 4?")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, tools);

        Assert.IsNotNull(result, "Result should not be null");
        Console.WriteLine($"OpenAI Multi-Tool Response: {result.Content}");

        if (result.UsedTools)
        {
            foreach (var usage in result.ToolUsages)
            {
                Console.WriteLine($"  Tool: {usage.ToolName}, Input: {usage.Input}");
            }
            Assert.IsTrue(result.ToolUsages.Any(u => u.ToolName == "calculator"), "Should use calculator, not weather");
            Assert.IsFalse(result.ToolUsages.Any(u => u.ToolName == "get_weather"), "Should not use weather tool");
        }

        Assert.IsTrue(result.Content!.Contains("25"), "Response should contain the answer (25)");
    }

    [TestMethod]
    [TestCategory("Anthropic")]
    [TestCategory("ToolInvocation")]
    public async Task Anthropic_QueryModelWithTools_MultipleTools_SelectsCorrectTool()
    {
        SkipIfMissingApiKey(AnthropicApiKey, "ANTHROPIC_API_KEY");

        var service = CreateServiceForAnthropic();
        var model = new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3-5-haiku-20241022" };
        var tools = new ILlmTool[] { new CalculatorTool(), new WeatherTool() };
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You have access to a calculator and weather tools. Use the appropriate tool."),
            ChatMessage.User("What is 100 divided by 4?")
        };

        var result = await service.QueryModelWithToolsAsync(model, messages, tools);

        Assert.IsNotNull(result, "Result should not be null");
        Console.WriteLine($"Anthropic Multi-Tool Response: {result.Content}");

        if (result.UsedTools)
        {
            foreach (var usage in result.ToolUsages)
            {
                Console.WriteLine($"  Tool: {usage.ToolName}, Input: {usage.Input}");
            }
            Assert.IsTrue(result.ToolUsages.Any(u => u.ToolName == "calculator"), "Should use calculator, not weather");
            Assert.IsFalse(result.ToolUsages.Any(u => u.ToolName == "get_weather"), "Should not use weather tool");
        }

        Assert.IsTrue(result.Content!.Contains("25"), "Response should contain the answer (25)");
    }

    #endregion

    #region Helpers

    private static void SkipIfMissingApiKey(string? apiKey, string keyName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Inconclusive($"Skipping test: {keyName} environment variable not set. " +
                $"Set it with: export {keyName}=\"your-api-key\"");
        }
    }

    #endregion

    #region Test Tools

    /// <summary>
    /// A simple calculator tool for testing tool invocation.
    /// </summary>
    private class CalculatorTool : ILlmTool
    {
        public string Name => "calculator";
        public string Description => "Performs basic arithmetic operations (add, subtract, multiply, divide)";

        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                operation = new
                {
                    type = "string",
                    description = "The operation to perform: add, subtract, multiply, divide",
                    @enum = new[] { "add", "subtract", "multiply", "divide" }
                },
                a = new
                {
                    type = "number",
                    description = "First operand"
                },
                b = new
                {
                    type = "number",
                    description = "Second operand"
                }
            },
            required = new[] { "operation", "a", "b" }
        });

        public Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
        {
            var operation = input.GetProperty("operation").GetString();
            var a = input.GetProperty("a").GetDouble();
            var b = input.GetProperty("b").GetDouble();

            var result = operation switch
            {
                "add" => a + b,
                "subtract" => a - b,
                "multiply" => a * b,
                "divide" => b != 0 ? a / b : double.NaN,
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            return Task.FromResult(JsonSerializer.Serialize(new { result, operation, a, b }));
        }
    }

    /// <summary>
    /// A mock weather tool for testing multiple tool selection.
    /// </summary>
    private class WeatherTool : ILlmTool
    {
        public string Name => "get_weather";
        public string Description => "Gets the current weather for a city";

        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                city = new
                {
                    type = "string",
                    description = "The city to get weather for"
                }
            },
            required = new[] { "city" }
        });

        public Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
        {
            var city = input.GetProperty("city").GetString();
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                city,
                temperature = 72,
                conditions = "sunny",
                humidity = 45
            }));
        }
    }

    #endregion
}
