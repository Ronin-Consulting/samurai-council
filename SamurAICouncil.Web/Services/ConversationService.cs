using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Web.Services;

/// <summary>
/// Service for managing conversations with database persistence.
/// </summary>
public class ConversationService
{
    private readonly IConversationRepository? _conversationRepository;
    private readonly IMessageRepository? _messageRepository;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        ILogger<ConversationService> logger,
        IConversationRepository? conversationRepository = null,
        IMessageRepository? messageRepository = null)
    {
        _logger = logger;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
    }

    public bool IsPersistenceEnabled => _conversationRepository != null && _messageRepository != null;

    /// <summary>
    /// List all conversations with metadata.
    /// </summary>
    public async Task<List<ConversationSummary>> ListConversationsAsync()
    {
        if (_conversationRepository == null)
        {
            return new List<ConversationSummary>();
        }

        try
        {
            var metadata = await _conversationRepository.ListConversationsAsync();
            return metadata
                .Select(m => new ConversationSummary(m.Id, m.Title, m.CreatedAt, null))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list conversations");
            return new List<ConversationSummary>();
        }
    }

    /// <summary>
    /// Load a conversation with all its messages.
    /// </summary>
    public async Task<Conversation?> LoadConversationAsync(Guid conversationId)
    {
        if (_conversationRepository == null || _messageRepository == null)
        {
            return null;
        }

        try
        {
            var conversation = await _conversationRepository.GetConversationAsync(conversationId);
            if (conversation == null)
            {
                return null;
            }

            var messages = await _messageRepository.GetMessagesForConversationAsync(conversationId);
            conversation.Messages = messages.ToList();
            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load conversation {ConversationId}", conversationId);
            return null;
        }
    }

    /// <summary>
    /// Create a new conversation in the database.
    /// </summary>
    public async Task<Conversation> CreateConversationAsync(string title = "New Conversation")
    {
        if (_conversationRepository != null)
        {
            try
            {
                var conversation = await _conversationRepository.CreateConversationAsync();
                if (!string.IsNullOrEmpty(title) && title != "New Conversation")
                {
                    await _conversationRepository.UpdateConversationTitleAsync(conversation.Id, title);
                    conversation.Title = title;
                }
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create conversation in database");
            }
        }

        // Fallback for in-memory when no database
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Save a user message to the database.
    /// </summary>
    public async Task SaveUserMessageAsync(Guid conversationId, UserMessage message)
    {
        if (_messageRepository == null)
        {
            return;
        }

        try
        {
            await _messageRepository.AddUserMessageAsync(conversationId, message.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user message for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Save an assistant message to the database.
    /// </summary>
    public async Task SaveAssistantMessageAsync(Guid conversationId, AssistantMessage message)
    {
        if (_messageRepository == null)
        {
            return;
        }

        // Skip saving if message is incomplete (no stage3 response yet)
        if (message.Stage3 == null)
        {
            return;
        }

        try
        {
            await _messageRepository.AddAssistantMessageAsync(
                conversationId,
                message.Stage1,
                message.Stage2,
                message.Stage3,
                message.Metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save assistant message for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Update a conversation's title.
    /// </summary>
    public async Task UpdateTitleAsync(Guid conversationId, string title)
    {
        if (_conversationRepository == null)
        {
            return;
        }

        try
        {
            await _conversationRepository.UpdateConversationTitleAsync(conversationId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update title for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Delete a conversation.
    /// </summary>
    public async Task DeleteConversationAsync(Guid conversationId)
    {
        if (_conversationRepository == null)
        {
            return;
        }

        try
        {
            await _conversationRepository.DeleteConversationAsync(conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Delete multiple conversations.
    /// </summary>
    /// <param name="conversationIds">The IDs of conversations to delete.</param>
    /// <returns>The number of conversations deleted, or 0 if an error occurred.</returns>
    public async Task<int> DeleteConversationsAsync(IEnumerable<Guid> conversationIds)
    {
        if (_conversationRepository == null)
        {
            return 0;
        }

        try
        {
            var idList = conversationIds.ToList();
            var count = await _conversationRepository.DeleteConversationsAsync(idList);
            _logger.LogInformation("Deleted {Count} conversations", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversations");
            return 0;
        }
    }
}
