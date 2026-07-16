using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamurAICouncil.Core.Configuration;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Orchestrates the 3-stage LLM Council deliberation process.
/// </summary>
public partial class CouncilService : ICouncilService
{
    private readonly ILlmService _llmService;
    private readonly CouncilConfiguration _config;
    private readonly CompanyDataConfiguration _companyDataConfig;
    private readonly CompanyDataTool? _companyDataTool;
    private readonly ILogger<CouncilService> _logger;

    // Keywords that suggest a query might need company data
    private static readonly string[] CompanyDataKeywords =
    [
        "sales", "revenue", "product", "customer", "store", "inventory",
        "profit", "order", "purchase", "transaction", "quarter", "annual",
        "monthly", "category", "brand", "manufacturer", "contoso", "retail",
        "top", "best", "worst", "highest", "lowest", "compare", "trend"
    ];

    public CouncilService(
        ILlmService llmService,
        IOptions<CouncilConfiguration> config,
        IOptions<CompanyDataConfiguration> companyDataConfig,
        ILogger<CouncilService> logger,
        ICompanyDataService? companyDataService = null,
        CompanyDataTool? companyDataTool = null)
    {
        _llmService = llmService;
        _config = config.Value;
        _companyDataConfig = companyDataConfig.Value;
        _companyDataTool = companyDataTool;
        _logger = logger;

        if (_companyDataConfig.IsEnabled && _companyDataTool != null)
        {
            _logger.LogInformation("Company data tools enabled for council queries");
        }
    }

    /// <summary>
    /// Determines if a query might benefit from company data tools.
    /// </summary>
    private bool ShouldUseCompanyDataTools(string query)
    {
        if (!_companyDataConfig.IsEnabled || _companyDataTool == null)
        {
            return false;
        }

        var lowerQuery = query.ToLowerInvariant();
        return CompanyDataKeywords.Any(keyword => lowerQuery.Contains(keyword));
    }

    /// <summary>
    /// Gets the list of tools to use for a query.
    /// </summary>
    private List<ILlmTool> GetToolsForQuery(string query)
    {
        var tools = new List<ILlmTool>();

        if (ShouldUseCompanyDataTools(query) && _companyDataTool != null)
        {
            tools.Add(_companyDataTool);
            _logger.LogDebug("Company data tool enabled for query");
        }

        return tools;
    }

    public async Task<List<Stage1Response>> Stage1CollectResponsesAsync(
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stage 1: Collecting responses from {Count} council models",
            _config.CouncilModels.Count);

        if (_config.CouncilModels.Count == 0)
        {
            _logger.LogWarning("No council models configured");
            return [];
        }

        var tools = GetToolsForQuery(userQuery);
        var hasTools = tools.Count > 0;

        // Build system prompt - include tool info if available
        var systemPrompt = hasTools
            ? "You are a helpful AI assistant with access to company data tools. " +
              "You can query the ContosoRetailDW database for sales, products, customers, and inventory information. " +
              "Use the query_company_data tool when the user asks about company data. " +
              "Provide a thorough and accurate response to the user's question."
            : "You are a helpful AI assistant. Provide a thorough and accurate response to the user's question.";

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(systemPrompt),
            ChatMessage.User(userQuery)
        };

        // Query all models in parallel (with tools if available)
        var stage1Results = new List<Stage1Response>();

        if (hasTools)
        {
            // Query models with tool support
            var tasks = _config.CouncilModels.Select(async model =>
            {
                var result = await _llmService.QueryModelWithToolsAsync(model, messages, tools, cancellationToken);
                return (model.FullModelId, result);
            });

            var results = await Task.WhenAll(tasks);

            foreach (var (modelId, result) in results)
            {
                if (result.Content != null)
                {
                    stage1Results.Add(new Stage1Response
                    {
                        Model = modelId,
                        Response = result.Content,
                        ToolUsages = result.ToolUsages
                    });
                    _logger.LogDebug("Model {Model} responded successfully, used {ToolCount} tools",
                        modelId, result.ToolUsages.Count);
                }
                else
                {
                    _logger.LogWarning("Model {Model} failed to respond", modelId);
                }
            }
        }
        else
        {
            // Query without tools (original behavior)
            var responses = await _llmService.QueryModelsParallelAsync(
                _config.CouncilModels,
                messages,
                cancellationToken);

            foreach (var (modelId, response) in responses)
            {
                if (response != null)
                {
                    stage1Results.Add(new Stage1Response
                    {
                        Model = modelId,
                        Response = response
                    });
                    _logger.LogDebug("Model {Model} responded successfully", modelId);
                }
                else
                {
                    _logger.LogWarning("Model {Model} failed to respond", modelId);
                }
            }
        }

        _logger.LogInformation("Stage 1 complete: {SuccessCount}/{TotalCount} models responded",
            stage1Results.Count, _config.CouncilModels.Count);

        return stage1Results;
    }

    public async Task<(List<Stage2Ranking> Rankings, Dictionary<string, string> LabelToModel)>
        Stage2CollectRankingsAsync(
            string userQuery,
            List<Stage1Response> stage1Results,
            CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stage 2: Collecting peer rankings from {Count} models",
            _config.CouncilModels.Count);

        if (stage1Results.Count < 2)
        {
            _logger.LogWarning("Not enough Stage 1 responses for peer review (need at least 2)");
            return ([], new Dictionary<string, string>());
        }

        // Anonymize responses
        var (anonymizedResponses, labelToModel) = AnonymizationHelper.AnonymizeResponses(stage1Results);

        // Create the Stage 2 prompt
        var stage2Prompt = AnonymizationHelper.CreateStage2Prompt(userQuery, anonymizedResponses);

        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are an expert evaluator. Carefully analyze each response and provide a fair ranking."),
            ChatMessage.User(stage2Prompt)
        };

        // Query all models in parallel
        var responses = await _llmService.QueryModelsParallelAsync(
            _config.CouncilModels,
            messages,
            cancellationToken);

        // Parse rankings from each response
        var stage2Results = new List<Stage2Ranking>();

        foreach (var (modelId, response) in responses)
        {
            if (response != null)
            {
                var parsedRanking = RankingParser.ParseRankingFromText(response);

                stage2Results.Add(new Stage2Ranking
                {
                    Model = modelId,
                    Ranking = response,
                    ParsedRanking = parsedRanking
                });

                _logger.LogDebug("Model {Model} ranked: {Rankings}",
                    modelId, string.Join(", ", parsedRanking));
            }
            else
            {
                _logger.LogWarning("Model {Model} failed to provide ranking", modelId);
            }
        }

        _logger.LogInformation("Stage 2 complete: {SuccessCount}/{TotalCount} models provided rankings",
            stage2Results.Count, _config.CouncilModels.Count);

        return (stage2Results, labelToModel);
    }

    public async Task<Stage3Response> Stage3SynthesizeFinalAsync(
        string userQuery,
        List<Stage1Response> stage1Results,
        List<Stage2Ranking> stage2Results,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stage 3: Synthesizing final response with chairman model");

        var chairmanModel = _config.ChairmanModel;
        if (chairmanModel == null)
        {
            _logger.LogWarning("No chairman model configured, using first council model");
            chairmanModel = _config.CouncilModels.FirstOrDefault();

            if (chairmanModel == null)
            {
                return new Stage3Response
                {
                    Model = "none",
                    Response = "Error: No models configured for synthesis."
                };
            }
        }

        // Build context from Stage 1 and Stage 2 results
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("## Original Question");
        contextBuilder.AppendLine(userQuery);
        contextBuilder.AppendLine();

        contextBuilder.AppendLine("## Individual Responses");
        foreach (var stage1Response in stage1Results)
        {
            var shortName = AnonymizationHelper.GetShortModelName(stage1Response.Model);
            contextBuilder.AppendLine($"### {shortName}");
            contextBuilder.AppendLine(stage1Response.Response);
            contextBuilder.AppendLine();
        }

        // Add aggregate rankings if available
        if (stage2Results.Count > 0)
        {
            contextBuilder.AppendLine("## Peer Review Summary");
            contextBuilder.AppendLine("The responses were anonymously reviewed and ranked by all models.");

            // We don't have labelToModel here, so we'll create a simple summary
            foreach (var ranking in stage2Results)
            {
                var shortName = AnonymizationHelper.GetShortModelName(ranking.Model);
                if (ranking.ParsedRanking.Count > 0)
                {
                    contextBuilder.AppendLine($"- {shortName} ranked: {string.Join(" > ", ranking.ParsedRanking)}");
                }
            }
            contextBuilder.AppendLine();
        }

        var synthesisPrompt = $"""
            Based on the responses and peer reviews below, synthesize the best possible answer to the user's question.
            Draw from the strongest elements of each response while correcting any errors.

            {contextBuilder}

            Write the final answer as a concise **executive brief** for a busy decision-maker:

            - Open with ONE bold sentence that directly answers the question — the bottom line.
            - Follow with 2–4 short sentences of interpretation: what it means, the key comparison
              or trend, and any caveat. Expand slightly only if the question genuinely needs more depth.
            - Round figures for readability (e.g. "$2.23B", not "$2,228,460,305.60"); give exact
              values only when precision truly matters.
            - When the data is numeric or tabular, DO NOT reproduce the full table or restate every
              row/number in prose — a chart or table is already displayed to the user alongside your
              answer. Refer to it briefly (e.g. "see the breakdown below") instead of listing figures.
            - Do NOT embed images or image/file links (no Markdown image syntax like `![...](...)`) —
              charts and tables are rendered automatically beside your answer; an image link only
              produces a broken image.
            - Use clean Markdown: bold for the lead answer and key terms; a short bullet list only if
              there are genuinely distinct points; a proper GFM table (| … | with a `---` separator
              row) ONLY if a small table is essential and is not already visualized. Avoid headings in
              a brief answer.
            - Be tight and professional. Prefer clarity over completeness; do not restate the question
              or pad with filler.

            Final answer:
            """;

        // Check if tools should be enabled for Stage 3
        var tools = GetToolsForQuery(userQuery);
        var hasTools = tools.Count > 0;

        const string styleGuide =
            " Write like a senior analyst briefing an executive: lead with the answer, stay concise, " +
            "and format cleanly in Markdown. Never dump raw data tables or long number lists in prose when " +
            "a chart or table is already shown — interpret the data, don't transcribe it.";

        var systemPrompt = hasTools
            ? "You are the chairman of an AI council with access to company data tools. " +
              "Your role is to synthesize the best final answer from multiple AI responses and their peer reviews. " +
              "You can use the query_company_data tool to verify or supplement information if needed." + styleGuide
            : "You are the chairman of an AI council. Your role is to synthesize the best final answer from multiple AI responses and their peer reviews." + styleGuide;

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(systemPrompt),
            ChatMessage.User(synthesisPrompt)
        };

        // Query with tools if available
        Stage3Response result;
        if (hasTools)
        {
            var queryResult = await _llmService.QueryModelWithToolsAsync(chairmanModel, messages, tools, cancellationToken);

            if (string.IsNullOrEmpty(queryResult.Content))
            {
                _logger.LogWarning("Chairman model returned null or empty response, using fallback");

                var fallbackResponse = stage1Results.FirstOrDefault()?.Response
                    ?? "Unable to generate a response at this time.";

                return new Stage3Response
                {
                    Model = chairmanModel.FullModelId + " (fallback)",
                    Response = fallbackResponse
                };
            }

            result = new Stage3Response
            {
                Model = chairmanModel.FullModelId,
                Response = queryResult.Content,
                ToolUsages = queryResult.ToolUsages
            };
        }
        else
        {
            var response = await _llmService.QueryModelAsync(chairmanModel, messages, cancellationToken);

            if (string.IsNullOrEmpty(response))
            {
                _logger.LogWarning("Chairman model returned null or empty response, using fallback");

                var fallbackResponse = stage1Results.FirstOrDefault()?.Response
                    ?? "Unable to generate a response at this time.";

                return new Stage3Response
                {
                    Model = chairmanModel.FullModelId + " (fallback)",
                    Response = fallbackResponse
                };
            }

            result = new Stage3Response
            {
                Model = chairmanModel.FullModelId,
                Response = response
            };
        }

        _logger.LogInformation("Stage 3 complete: Chairman synthesized final response (tools used: {ToolCount})",
            result.ToolUsages.Count);

        return result;
    }

    public async Task<CouncilResult> RunFullCouncilAsync(
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting full council process for query");

        // Stage 1: Collect individual responses
        var stage1Results = await Stage1CollectResponsesAsync(userQuery, cancellationToken);

        if (stage1Results.Count == 0)
        {
            _logger.LogError("No responses collected in Stage 1, aborting council process");
            return new CouncilResult
            {
                Stage1 = [],
                Stage2 = [],
                Stage3 = new Stage3Response
                {
                    Model = "error",
                    Response = "Error: No council models responded. Please check your configuration and API keys."
                },
                Metadata = new CouncilMetadata()
            };
        }

        // Stage 2: Collect peer rankings
        var (stage2Results, labelToModel) = await Stage2CollectRankingsAsync(
            userQuery, stage1Results, cancellationToken);

        // Calculate aggregate rankings
        var aggregateRankings = AggregateRankingCalculator.Calculate(stage2Results, labelToModel);

        // Stage 3: Synthesize final response
        var stage3Result = await Stage3SynthesizeFinalAsync(
            userQuery, stage1Results, stage2Results, cancellationToken);

        // Propagate chart from the best-ranked Stage 1 response to Stage 3
        ChartRecommendation? chartFromStage1 = null;

        // Try to get chart from the top-ranked response first
        if (aggregateRankings.Count > 0)
        {
            var topRankedModel = aggregateRankings.First().Model;
            var topRankedResponse = stage1Results.FirstOrDefault(r => r.Model == topRankedModel);

            if (topRankedResponse != null)
            {
                chartFromStage1 = topRankedResponse.ToolUsages
                    .FirstOrDefault(t => t.Chart != null)?.Chart;

                if (chartFromStage1 != null)
                {
                    _logger.LogInformation(
                        "Using chart from top-ranked response ({Model}): {ChartType} - {Title}",
                        topRankedModel, chartFromStage1.Type, chartFromStage1.Title);
                }
            }
        }

        // Fallback: if top-ranked response has no chart, find first available chart
        if (chartFromStage1 == null)
        {
            chartFromStage1 = stage1Results
                .SelectMany(r => r.ToolUsages)
                .FirstOrDefault(t => t.Chart != null)?.Chart;

            if (chartFromStage1 != null)
            {
                _logger.LogInformation(
                    "Using fallback chart (first available): {ChartType} - {Title}",
                    chartFromStage1.Type, chartFromStage1.Title);
            }
        }

        if (chartFromStage1 != null)
        {
            stage3Result.Chart = chartFromStage1;
        }
        else
        {
            _logger.LogDebug("No chart found in Stage 1 tool usages to propagate");
        }

        // Calculate tool usage stats
        var toolsEnabled = ShouldUseCompanyDataTools(userQuery);
        var totalToolCalls = stage1Results.Sum(r => r.ToolUsages.Count) + stage3Result.ToolUsages.Count;

        _logger.LogInformation("Council process complete (tools enabled: {ToolsEnabled}, total tool calls: {ToolCalls})",
            toolsEnabled, totalToolCalls);

        return new CouncilResult
        {
            Stage1 = stage1Results,
            Stage2 = stage2Results,
            Stage3 = stage3Result,
            Metadata = new CouncilMetadata
            {
                LabelToModel = labelToModel,
                AggregateRankings = aggregateRankings,
                ToolsEnabled = toolsEnabled,
                TotalToolCalls = totalToolCalls
            }
        };
    }

    public async Task<string> GenerateConversationTitleAsync(
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        const string defaultTitle = "New Conversation";
        const int maxTitleLength = 50;

        var titleModel = _config.TitleGenerationModel ?? _config.ChairmanModel ?? _config.CouncilModels.FirstOrDefault();

        if (titleModel == null)
        {
            return defaultTitle;
        }

        var messages = new List<ChatMessage>
        {
            ChatMessage.System("Generate a very short title (3-5 words) that summarizes the user's question. Respond with ONLY the title, nothing else."),
            ChatMessage.User(userQuery)
        };

        try
        {
            var response = await _llmService.QueryModelAsync(titleModel, messages, cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                return defaultTitle;
            }

            // Clean up the response
            var title = response.Trim()
                .Trim('"', '\'', '*', '_') // Remove quotes and markdown formatting
                .Trim();

            // Truncate if too long
            if (title.Length > maxTitleLength)
            {
                title = title[..maxTitleLength].TrimEnd() + "...";
            }

            return string.IsNullOrWhiteSpace(title) ? defaultTitle : title;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate conversation title");
            return defaultTitle;
        }
    }
}
