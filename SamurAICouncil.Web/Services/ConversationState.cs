using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Web.Services;

public class ConversationState
{
    private Conversation? _currentConversation;
    private readonly List<ConversationSummary> _conversations = new();
    private bool _isProcessing;

    public event Action? OnChange;

    public Conversation? CurrentConversation
    {
        get => _currentConversation;
        set
        {
            _currentConversation = value;
            NotifyStateChanged();
        }
    }

    public IReadOnlyList<ConversationSummary> Conversations => _conversations.AsReadOnly();

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            _isProcessing = value;
            NotifyStateChanged();
        }
    }

    public void SetConversations(IEnumerable<ConversationSummary> conversations)
    {
        _conversations.Clear();
        _conversations.AddRange(conversations);
        NotifyStateChanged();
    }

    public void AddConversation(ConversationSummary conversation)
    {
        _conversations.Insert(0, conversation);
        NotifyStateChanged();
    }

    public void UpdateConversation(ConversationSummary conversation)
    {
        var index = _conversations.FindIndex(c => c.Id == conversation.Id);
        if (index >= 0)
        {
            _conversations[index] = conversation;
            NotifyStateChanged();
        }
    }

    public void RemoveConversation(Guid conversationId)
    {
        _conversations.RemoveAll(c => c.Id == conversationId);
        if (_currentConversation?.Id == conversationId)
        {
            _currentConversation = null;
        }
        NotifyStateChanged();
    }

    public void StartNewConversation()
    {
        _currentConversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "New Conversation",
            CreatedAt = DateTime.UtcNow
        };
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

public record ConversationSummary(Guid Id, string Title, DateTime CreatedAt, DateTime? UpdatedAt);
