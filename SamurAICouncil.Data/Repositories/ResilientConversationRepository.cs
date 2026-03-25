using Polly;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Data.Resilience;

namespace SamurAICouncil.Data.Repositories;

/// <summary>
/// Resilient wrapper for ConversationRepository with Polly retry and circuit breaker.
/// </summary>
public class ResilientConversationRepository : IConversationRepository
{
    private readonly IConversationRepository _inner;
    private readonly AsyncPolicy _policy;

    public ResilientConversationRepository(IConversationRepository inner)
        : this(inner, DatabaseResiliencePolicies.CreateCombinedPolicy())
    {
    }

    public ResilientConversationRepository(IConversationRepository inner, AsyncPolicy policy)
    {
        _inner = inner;
        _policy = policy;
    }

    public Task<Conversation> CreateConversationAsync(CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.CreateConversationAsync(ct), cancellationToken);

    public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.GetConversationAsync(id, ct), cancellationToken);

    public Task<List<ConversationMetadata>> ListConversationsAsync(CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.ListConversationsAsync(ct), cancellationToken);

    public Task UpdateConversationTitleAsync(Guid id, string title, CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.UpdateConversationTitleAsync(id, title, ct), cancellationToken);

    public Task DeleteConversationAsync(Guid id, CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.DeleteConversationAsync(id, ct), cancellationToken);

    public Task<int> DeleteConversationsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(ct => _inner.DeleteConversationsAsync(ids, ct), cancellationToken);
}
