using Microsoft.Extensions.Logging;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// One event emitted while streaming a council run to a client (e.g. over SSE).
/// </summary>
/// <param name="Event">Event name: loading | stage1 | stage2 | stage3 | title | done | error.</param>
/// <param name="Data">Serializable payload for the event (may be null).</param>
public record CouncilStreamEvent(string Event, object? Data);

/// <summary>
/// Application service that orchestrates the 3-stage council run and conversation
/// persistence for the API layer. Ports the logic that previously lived in the Blazor
/// page (Chat.razor SendMessage: aggregate rankings + chart propagation) and the Blazor
/// ConversationService (repository access with a graceful "persistence disabled" fallback).
/// Repositories are optional; when absent, runs in-memory (no persistence).
/// </summary>
public class CouncilConversationService
{
    private readonly ICouncilService _council;
    private readonly IConversationRepository? _conversationRepository;
    private readonly IMessageRepository? _messageRepository;
    private readonly ILogger<CouncilConversationService> _logger;

    public CouncilConversationService(
        ICouncilService council,
        ILogger<CouncilConversationService> logger,
        IConversationRepository? conversationRepository = null,
        IMessageRepository? messageRepository = null)
    {
        _council = council;
        _logger = logger;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
    }

    public bool IsPersistenceEnabled => _conversationRepository != null && _messageRepository != null;

    // ---- Conversation CRUD (ported from Web/Services/ConversationService.cs) ----

    public async Task<List<ConversationSummary>> ListConversationsAsync(CancellationToken ct = default)
    {
        if (_conversationRepository == null) return [];
        try
        {
            var metadata = await _conversationRepository.ListConversationsAsync(ct);
            return metadata.Select(m => new ConversationSummary(m.Id, m.Title, m.CreatedAt, null)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list conversations");
            return [];
        }
    }

    public async Task<Conversation?> LoadConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (_conversationRepository == null || _messageRepository == null) return null;
        try
        {
            var conversation = await _conversationRepository.GetConversationAsync(conversationId, ct);
            if (conversation == null) return null;
            var messages = await _messageRepository.GetMessagesForConversationAsync(conversationId, ct);
            conversation.Messages = messages.ToList();
            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load conversation {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task<Conversation> CreateConversationAsync(string title = "New Conversation", CancellationToken ct = default)
    {
        if (_conversationRepository != null)
        {
            try
            {
                var conversation = await _conversationRepository.CreateConversationAsync(ct);
                if (!string.IsNullOrEmpty(title) && title != "New Conversation")
                {
                    await _conversationRepository.UpdateConversationTitleAsync(conversation.Id, title, ct);
                    conversation.Title = title;
                }
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create conversation in database");
            }
        }
        return new Conversation { Id = Guid.NewGuid(), Title = title, CreatedAt = DateTime.UtcNow };
    }

    public async Task UpdateTitleAsync(Guid conversationId, string title, CancellationToken ct = default)
    {
        if (_conversationRepository == null) return;
        try { await _conversationRepository.UpdateConversationTitleAsync(conversationId, title, ct); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to update title for {ConversationId}", conversationId); }
    }

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (_conversationRepository == null) return;
        try { await _conversationRepository.DeleteConversationAsync(conversationId, ct); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to delete conversation {ConversationId}", conversationId); }
    }

    public async Task<int> DeleteConversationsAsync(IEnumerable<Guid> conversationIds, CancellationToken ct = default)
    {
        if (_conversationRepository == null) return 0;
        try
        {
            var count = await _conversationRepository.DeleteConversationsAsync(conversationIds.ToList(), ct);
            _logger.LogInformation("Deleted {Count} conversations", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversations");
            return 0;
        }
    }

    /// <summary>
    /// Loads an assistant message by id for export. Returns the message plus the preceding
    /// user query and the message timestamp, or null if not found.
    /// </summary>
    public async Task<(AssistantMessage Message, string Query, DateTime Timestamp)?> GetAssistantMessageAsync(
        Guid conversationId, CancellationToken ct = default)
    {
        if (_messageRepository == null) return null;
        var messages = await _messageRepository.GetMessagesForConversationAsync(conversationId, ct);
        // Most recent assistant message + the user message immediately before it.
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i] is AssistantMessage asst)
            {
                var query = i > 0 && messages[i - 1] is UserMessage um ? um.Content : string.Empty;
                return (asst, query, DateTime.UtcNow);
            }
        }
        return null;
    }

    // ---- Streaming council run (ported from Chat.razor SendMessage) ----

    /// <summary>
    /// Runs the full 3-stage council for a user query, invoking <paramref name="emit"/> as each
    /// stage completes so the caller can stream progress (SSE). Persists the user message up front
    /// and the assistant message once Stage 3 is available.
    /// </summary>
    public async Task RunCouncilStreamAsync(
        Guid conversationId,
        string userQuery,
        Func<CouncilStreamEvent, Task> emit,
        CancellationToken ct = default)
    {
        // Determine whether this is the first exchange (drives title generation).
        var isFirstExchange = false;
        if (_messageRepository != null)
        {
            try
            {
                var existing = await _messageRepository.GetMessagesForConversationAsync(conversationId, ct);
                isFirstExchange = existing.Count == 0;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not count messages for {ConversationId}", conversationId); }
        }

        // Persist the user message.
        if (_messageRepository != null)
        {
            try { await _messageRepository.AddUserMessageAsync(conversationId, userQuery, ct); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to save user message for {ConversationId}", conversationId); }
        }

        var assistant = new AssistantMessage { Loading = new LoadingState { Stage1 = true } };

        try
        {
            await emit(new CouncilStreamEvent("loading", new LoadingState { Stage1 = true }));

            // Stage 1
            var stage1 = await _council.Stage1CollectResponsesAsync(userQuery, ct);
            assistant.Stage1 = stage1;
            await emit(new CouncilStreamEvent("stage1", stage1));

            if (stage1.Count == 0)
            {
                var errStage3 = new Stage3Response
                {
                    Model = "error",
                    Response = "Unable to get responses from any AI model. Please check your API configuration."
                };
                assistant.Stage3 = errStage3;
                await emit(new CouncilStreamEvent("stage3", errStage3));
                await emit(new CouncilStreamEvent("done", null));
                return;
            }

            await emit(new CouncilStreamEvent("loading", new LoadingState { Stage2 = true }));

            // Stage 2 + aggregate rankings
            var (stage2, labelToModel) = await _council.Stage2CollectRankingsAsync(userQuery, stage1, ct);
            var aggregateRankings = AggregateRankingCalculator.Calculate(stage2, labelToModel);
            var metadata = new CouncilMetadata
            {
                LabelToModel = labelToModel,
                AggregateRankings = aggregateRankings,
                ToolsEnabled = stage1.Any(s => s.UsedTools),
                TotalToolCalls = stage1.Sum(s => s.ToolUsages.Count)
            };
            assistant.Stage2 = stage2;
            assistant.Metadata = metadata;
            await emit(new CouncilStreamEvent("stage2", new { rankings = stage2, metadata }));

            await emit(new CouncilStreamEvent("loading", new LoadingState { Stage3 = true }));

            // Stage 3 + chart propagation (top aggregate-ranked Stage 1 response -> Stage 3)
            var stage3 = await _council.Stage3SynthesizeFinalAsync(userQuery, stage1, stage2, ct);

            ChartRecommendation? chart = null;
            if (aggregateRankings.Count > 0)
            {
                var topModel = aggregateRankings[0].Model;
                chart = stage1.FirstOrDefault(r => r.Model == topModel)?
                    .ToolUsages.FirstOrDefault(t => t.Chart != null)?.Chart;
            }
            chart ??= stage1.SelectMany(r => r.ToolUsages).FirstOrDefault(t => t.Chart != null)?.Chart;
            if (chart != null)
            {
                stage3.Chart = chart;
                metadata.TotalToolCalls += stage3.ToolUsages.Count;
                _logger.LogInformation("Propagated chart to Final Answer: {Type} - {Title}", chart.Type, chart.Title);
            }

            assistant.Stage3 = stage3;
            assistant.Loading = null;
            await emit(new CouncilStreamEvent("stage3", stage3));

            // Persist the completed assistant message.
            if (_messageRepository != null && assistant.Stage3 != null)
            {
                try
                {
                    await _messageRepository.AddAssistantMessageAsync(
                        conversationId, assistant.Stage1, assistant.Stage2!, assistant.Stage3, assistant.Metadata, ct);
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to save assistant message for {ConversationId}", conversationId); }
            }

            // Title generation for the first exchange.
            if (isFirstExchange)
            {
                try
                {
                    var title = await _council.GenerateConversationTitleAsync(userQuery, ct);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        await UpdateTitleAsync(conversationId, title, ct);
                        await emit(new CouncilStreamEvent("title", new { title }));
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Title generation failed for {ConversationId}", conversationId); }
            }

            await emit(new CouncilStreamEvent("done", null));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Council run cancelled for {ConversationId}", conversationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Council run failed for {ConversationId}", conversationId);
            var errStage3 = new Stage3Response
            {
                Model = "error",
                Response = $"An error occurred while processing your request: {ex.Message}"
            };
            await emit(new CouncilStreamEvent("error", new { message = ex.Message, stage3 = errStage3 }));
        }
    }
}
