using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

[TestClass]
public class CouncilServiceTests
{
    private Mock<ILlmService> _mockLlmService = null!;
    private Mock<ILogger<CouncilService>> _mockLogger = null!;
    private CouncilConfiguration _config = null!;
    private CompanyDataConfiguration _companyDataConfig = null!;
    private CouncilService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLlmService = new Mock<ILlmService>();
        _mockLogger = new Mock<ILogger<CouncilService>>();

        _config = new CouncilConfiguration
        {
            CouncilModels =
            [
                new ModelConfiguration { Provider = "openai", ModelId = "gpt-4" },
                new ModelConfiguration { Provider = "anthropic", ModelId = "claude-3" },
                new ModelConfiguration { Provider = "google", ModelId = "gemini-pro" }
            ],
            ChairmanModel = new ModelConfiguration { Provider = "openai", ModelId = "gpt-4-chairman" },
            TitleGenerationModel = new ModelConfiguration { Provider = "openai", ModelId = "gpt-3.5-turbo" }
        };

        // Company data is disabled by default in tests (no connection string)
        _companyDataConfig = new CompanyDataConfiguration();

        var options = Options.Create(_config);
        var companyDataOptions = Options.Create(_companyDataConfig);
        _service = new CouncilService(_mockLlmService.Object, options, companyDataOptions, _mockLogger.Object);
    }

    #region Stage1CollectResponsesAsync Tests

    [TestMethod]
    public async Task Stage1CollectResponsesAsync_QueriesAllCouncilModels()
    {
        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = "Response from GPT-4",
                ["anthropic/claude-3"] = "Response from Claude",
                ["google/gemini-pro"] = "Response from Gemini"
            });

        var result = await _service.Stage1CollectResponsesAsync("Test query");

        Assert.AreEqual(3, result.Count);
        _mockLlmService.Verify(x => x.QueryModelsParallelAsync(
            It.Is<IEnumerable<ModelConfiguration>>(m => m.Count() == 3),
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Stage1CollectResponsesAsync_HandlesPartialFailures()
    {
        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = "Response from GPT-4",
                ["anthropic/claude-3"] = null, // Failed
                ["google/gemini-pro"] = "Response from Gemini"
            });

        var result = await _service.Stage1CollectResponsesAsync("Test query");

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(r => r.Response != null));
    }

    [TestMethod]
    public async Task Stage1CollectResponsesAsync_ReturnsEmptyListWhenAllFail()
    {
        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = null,
                ["anthropic/claude-3"] = null,
                ["google/gemini-pro"] = null
            });

        var result = await _service.Stage1CollectResponsesAsync("Test query");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task Stage1CollectResponsesAsync_WithNoCouncilModels_ReturnsEmptyList()
    {
        _config.CouncilModels.Clear();
        var options = Options.Create(_config);
        var companyDataOptions = Options.Create(_companyDataConfig);
        var service = new CouncilService(_mockLlmService.Object, options, companyDataOptions, _mockLogger.Object);

        var result = await service.Stage1CollectResponsesAsync("Test query");

        Assert.AreEqual(0, result.Count);
        _mockLlmService.Verify(x => x.QueryModelsParallelAsync(
            It.IsAny<IEnumerable<ModelConfiguration>>(),
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Stage2CollectRankingsAsync Tests

    [TestMethod]
    public async Task Stage2CollectRankingsAsync_CreatesAnonymizedPrompt()
    {
        var stage1Results = new List<Core.Models.Stage1Response>
        {
            new() { Model = "openai/gpt-4", Response = "GPT-4 response" },
            new() { Model = "anthropic/claude-3", Response = "Claude response" }
        };

        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = "FINAL RANKING:\n1. Response A\n2. Response B",
                ["anthropic/claude-3"] = "FINAL RANKING:\n1. Response B\n2. Response A",
                ["google/gemini-pro"] = "FINAL RANKING:\n1. Response A\n2. Response B"
            });

        var (rankings, labelToModel) = await _service.Stage2CollectRankingsAsync("Test query", stage1Results);

        // Verify rankings were collected
        Assert.AreEqual(3, rankings.Count);

        // Verify label to model mapping was created
        Assert.AreEqual(2, labelToModel.Count);
        Assert.IsTrue(labelToModel.ContainsKey("Response A"));
        Assert.IsTrue(labelToModel.ContainsKey("Response B"));
    }

    [TestMethod]
    public async Task Stage2CollectRankingsAsync_ParsesRankingsCorrectly()
    {
        var stage1Results = new List<Core.Models.Stage1Response>
        {
            new() { Model = "model-1", Response = "Response 1" },
            new() { Model = "model-2", Response = "Response 2" }
        };

        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = "FINAL RANKING:\n1. Response B\n2. Response A",
                ["anthropic/claude-3"] = "FINAL RANKING:\n1. Response A\n2. Response B",
                ["google/gemini-pro"] = "FINAL RANKING:\n1. Response A\n2. Response B"
            });

        var (rankings, _) = await _service.Stage2CollectRankingsAsync("Test query", stage1Results);

        // Verify parsed rankings
        Assert.IsTrue(rankings.All(r => r.ParsedRanking.Count == 2));
    }

    [TestMethod]
    public async Task Stage2CollectRankingsAsync_WithLessThan2Stage1Results_ReturnsEmptyResults()
    {
        var stage1Results = new List<Core.Models.Stage1Response>
        {
            new() { Model = "openai/gpt-4", Response = "Single response" }
        };

        var (rankings, labelToModel) = await _service.Stage2CollectRankingsAsync("Test query", stage1Results);

        Assert.AreEqual(0, rankings.Count);
        Assert.AreEqual(0, labelToModel.Count);
    }

    #endregion

    #region Stage3SynthesizeFinalAsync Tests

    [TestMethod]
    public async Task Stage3SynthesizeFinalAsync_QueriesChairmanModel()
    {
        var stage1Results = new List<Core.Models.Stage1Response>
        {
            new() { Model = "model-1", Response = "Response 1" }
        };
        var stage2Results = new List<Core.Models.Stage2Ranking>();

        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.Is<ModelConfiguration>(m => m.ModelId == "gpt-4-chairman"),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Final synthesized response");

        var result = await _service.Stage3SynthesizeFinalAsync("Test query", stage1Results, stage2Results);

        Assert.AreEqual("openai/gpt-4-chairman", result.Model);
        Assert.AreEqual("Final synthesized response", result.Response);
    }

    [TestMethod]
    public async Task Stage3SynthesizeFinalAsync_WithChairmanFailure_UsesFallback()
    {
        var stage1Results = new List<Core.Models.Stage1Response>
        {
            new() { Model = "model-1", Response = "Fallback response content" }
        };
        var stage2Results = new List<Core.Models.Stage2Ranking>();

        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _service.Stage3SynthesizeFinalAsync("Test query", stage1Results, stage2Results);

        Assert.IsTrue(result.Model.Contains("fallback"));
        Assert.AreEqual("Fallback response content", result.Response);
    }

    #endregion

    #region RunFullCouncilAsync Tests

    [TestMethod]
    public async Task RunFullCouncilAsync_ExecutesAll3Stages()
    {
        // Setup Stage 1
        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.Is<IEnumerable<ChatMessage>>(m => m.Any(msg => msg.Content.Contains("Test query"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = "Response from GPT-4",
                ["anthropic/claude-3"] = "Response from Claude",
                ["google/gemini-pro"] = "Response from Gemini"
            });

        // Setup Stage 2
        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.Is<IEnumerable<ChatMessage>>(m => m.Any(msg => msg.Content.Contains("FINAL RANKING"))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = "FINAL RANKING:\n1. Response A\n2. Response B\n3. Response C",
                ["anthropic/claude-3"] = "FINAL RANKING:\n1. Response B\n2. Response A\n3. Response C",
                ["google/gemini-pro"] = "FINAL RANKING:\n1. Response A\n2. Response C\n3. Response B"
            });

        // Setup Stage 3
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.Is<ModelConfiguration>(m => m.ModelId == "gpt-4-chairman"),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Final synthesized answer");

        var result = await _service.RunFullCouncilAsync("Test query");

        Assert.AreEqual(3, result.Stage1.Count);
        Assert.AreEqual(3, result.Stage2.Count);
        Assert.IsNotNull(result.Stage3);
        Assert.AreEqual("Final synthesized answer", result.Stage3.Response);
        Assert.IsTrue(result.Metadata.LabelToModel.Count > 0);
    }

    [TestMethod]
    public async Task RunFullCouncilAsync_WithStage1Failure_ReturnsErrorResult()
    {
        _mockLlmService
            .Setup(x => x.QueryModelsParallelAsync(
                It.IsAny<IEnumerable<ModelConfiguration>>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["openai/gpt-4"] = null,
                ["anthropic/claude-3"] = null,
                ["google/gemini-pro"] = null
            });

        var result = await _service.RunFullCouncilAsync("Test query");

        Assert.AreEqual(0, result.Stage1.Count);
        Assert.IsNotNull(result.Stage3);
        Assert.IsTrue(result.Stage3.Response.Contains("Error"));
    }

    #endregion

    #region GenerateConversationTitleAsync Tests

    [TestMethod]
    public async Task GenerateConversationTitleAsync_ReturnsTitleFromModel()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.Is<ModelConfiguration>(m => m.ModelId == "gpt-3.5-turbo"),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Capital of France");

        var result = await _service.GenerateConversationTitleAsync("What is the capital of France?");

        Assert.AreEqual("Capital of France", result);
    }

    [TestMethod]
    public async Task GenerateConversationTitleAsync_TrimsQuotesAndFormatting()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("\"**Python Basics**\"");

        var result = await _service.GenerateConversationTitleAsync("Teach me Python");

        Assert.AreEqual("Python Basics", result);
    }

    [TestMethod]
    public async Task GenerateConversationTitleAsync_TruncatesLongTitles()
    {
        var longTitle = new string('X', 100);
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(longTitle);

        var result = await _service.GenerateConversationTitleAsync("Question");

        Assert.IsTrue(result.Length <= 53); // 50 + "..."
        Assert.IsTrue(result.EndsWith("..."));
    }

    [TestMethod]
    public async Task GenerateConversationTitleAsync_ReturnsDefaultOnFailure()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _service.GenerateConversationTitleAsync("Question");

        Assert.AreEqual("New Conversation", result);
    }

    [TestMethod]
    public async Task GenerateConversationTitleAsync_ReturnsDefaultOnException()
    {
        _mockLlmService
            .Setup(x => x.QueryModelAsync(
                It.IsAny<ModelConfiguration>(),
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var result = await _service.GenerateConversationTitleAsync("Question");

        Assert.AreEqual("New Conversation", result);
    }

    #endregion
}
