using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Interfaces;

/// <summary>
/// Repository for conversation persistence.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Create a new conversation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation.</returns>
    Task<Conversation> CreateConversationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a conversation by ID, including all messages.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation, or null if not found.</returns>
    Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all conversations (metadata only, no messages).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of conversation metadata sorted by creation date (newest first).</returns>
    Task<List<ConversationMetadata>> ListConversationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the title of a conversation.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="title">The new title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateConversationTitleAsync(Guid id, string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a conversation and all its messages.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteConversationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple conversations and all their messages.
    /// </summary>
    /// <param name="ids">The conversation IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of conversations deleted.</returns>
    Task<int> DeleteConversationsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
