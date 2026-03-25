using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Interfaces;

/// <summary>
/// Repository for message persistence.
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Add a user message to a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="content">The message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created message.</returns>
    Task<UserMessage> AddUserMessageAsync(
        Guid conversationId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add an assistant message with all 3 stages to a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="stage1">Stage 1 responses.</param>
    /// <param name="stage2">Stage 2 rankings.</param>
    /// <param name="stage3">Stage 3 final response.</param>
    /// <param name="metadata">Council metadata (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created message.</returns>
    Task<AssistantMessage> AddAssistantMessageAsync(
        Guid conversationId,
        List<Stage1Response> stage1,
        List<Stage2Ranking> stage2,
        Stage3Response stage3,
        CouncilMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all messages for a conversation in order.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of messages in chronological order.</returns>
    Task<List<Message>> GetMessagesForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);
}
