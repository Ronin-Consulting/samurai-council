using Polly;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Data.Resilience;

namespace SamurAICouncil.Data.Repositories;

/// <summary>
/// Resilient wrapper for MessageRepository with Polly retry and circuit breaker.
/// </summary>
public class ResilientMessageRepository : IMessageRepository
{
    private readonly IMessageRepository _inner;
    private readonly AsyncPolicy _policy;

    public ResilientMessageRepository(IMessageRepository inner)
        : this(inner, DatabaseResiliencePolicies.CreateCombinedPolicy())
    {
    }

    public ResilientMessageRepository(IMessageRepository inner, AsyncPolicy policy)
    {
        _inner = inner;
        _policy = policy;
    }

    public Task<UserMessage> AddUserMessageAsync(
        Guid conversationId,
        string content,
        CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.AddUserMessageAsync(conversationId, content, ct), cancellationToken);

    public Task<AssistantMessage> AddAssistantMessageAsync(
        Guid conversationId,
        List<Stage1Response> stage1,
        List<Stage2Ranking> stage2,
        Stage3Response stage3,
        CouncilMetadata? metadata = null,
        CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.AddAssistantMessageAsync(
            conversationId, stage1, stage2, stage3, metadata, ct), cancellationToken);

    public Task<List<Message>> GetMessagesForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.GetMessagesForConversationAsync(conversationId, ct), cancellationToken);
}
