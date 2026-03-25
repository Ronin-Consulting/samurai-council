using Microsoft.Extensions.Logging;
using Moq;
using SamurAICouncil.Core.Models;
using SamurAICouncil.Data.Repositories;
using SamurAICouncil.Web.Services;

namespace SamurAICouncil.Web.Tests.Integration;

[TestClass]
public class ConversationServiceIntegrationTests
{
    private ConversationRepository _conversationRepository = null!;
    private MessageRepository _messageRepository = null!;
    private ConversationService _service = null!;
    private Mock<ILogger<ConversationService>> _mockLogger = null!;

    [AssemblyInitialize]
    public static async Task AssemblyInitialize(TestContext context)
    {
        await PostgresFixture.InitializeAsync();
    }

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        await PostgresFixture.DisposeAsync();
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        await PostgresFixture.ClearDataAsync();

        _conversationRepository = new ConversationRepository(PostgresFixture.ConnectionString);
        _messageRepository = new MessageRepository(PostgresFixture.ConnectionString);
        _mockLogger = new Mock<ILogger<ConversationService>>();
        _service = new ConversationService(_mockLogger.Object, _conversationRepository, _messageRepository);
    }

    [TestMethod]
    public void IsPersistenceEnabled_WithRepositories_ReturnsTrue()
    {
        Assert.IsTrue(_service.IsPersistenceEnabled);
    }

    [TestMethod]
    public void IsPersistenceEnabled_WithoutRepositories_ReturnsFalse()
    {
        var serviceWithoutDb = new ConversationService(_mockLogger.Object);
        Assert.IsFalse(serviceWithoutDb.IsPersistenceEnabled);
    }

    [TestMethod]
    public async Task CreateConversationAsync_CreatesConversation_ReturnsWithId()
    {
        var conversation = await _service.CreateConversationAsync();

        Assert.AreNotEqual(Guid.Empty, conversation.Id);
        Assert.IsNotNull(conversation.Title);
        Assert.IsTrue(conversation.CreatedAt > DateTime.MinValue);
    }

    [TestMethod]
    public async Task CreateConversationAsync_WithCustomTitle_SetsTitle()
    {
        var conversation = await _service.CreateConversationAsync("My Test Chat");

        Assert.AreEqual("My Test Chat", conversation.Title);
    }

    [TestMethod]
    public async Task SaveUserMessageAsync_SavesMessage_CanBeRetrieved()
    {
        var conversation = await _service.CreateConversationAsync();
        var userMessage = new UserMessage { Content = "Hello, Council!" };

        await _service.SaveUserMessageAsync(conversation.Id, userMessage);

        var loaded = await _service.LoadConversationAsync(conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(1, loaded.Messages.Count);
        Assert.IsInstanceOfType(loaded.Messages[0], typeof(UserMessage));
        Assert.AreEqual("Hello, Council!", ((UserMessage)loaded.Messages[0]).Content);
    }

    [TestMethod]
    public async Task SaveAssistantMessageAsync_SavesAllStages_CanBeRetrieved()
    {
        var conversation = await _service.CreateConversationAsync();
        var userMessage = new UserMessage { Content = "Test question" };
        await _service.SaveUserMessageAsync(conversation.Id, userMessage);

        var assistantMessage = new AssistantMessage
        {
            Stage1 = new List<Stage1Response>
            {
                new() { Model = "gpt-4", Response = "GPT-4 response" },
                new() { Model = "claude-3", Response = "Claude response" }
            },
            Stage2 = new List<Stage2Ranking>
            {
                new()
                {
                    Model = "gpt-4",
                    Ranking = "A > B",
                    ParsedRanking = new List<string> { "A", "B" }
                }
            },
            Stage3 = new Stage3Response { Model = "claude-3", Response = "Synthesized answer" },
            Metadata = new CouncilMetadata
            {
                LabelToModel = new Dictionary<string, string> { { "A", "claude-3" } }
            }
        };

        await _service.SaveAssistantMessageAsync(conversation.Id, assistantMessage);

        var loaded = await _service.LoadConversationAsync(conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(2, loaded.Messages.Count);

        var loadedAssistant = loaded.Messages[1] as AssistantMessage;
        Assert.IsNotNull(loadedAssistant);
        Assert.AreEqual(2, loadedAssistant.Stage1.Count);
        Assert.AreEqual(1, loadedAssistant.Stage2.Count);
        Assert.AreEqual("Synthesized answer", loadedAssistant.Stage3!.Response);
    }

    [TestMethod]
    public async Task SaveAssistantMessageAsync_WithoutStage3_DoesNotSave()
    {
        var conversation = await _service.CreateConversationAsync();
        var userMessage = new UserMessage { Content = "Test" };
        await _service.SaveUserMessageAsync(conversation.Id, userMessage);

        // Assistant message without Stage3 (incomplete)
        var incompleteMessage = new AssistantMessage
        {
            Stage1 = new List<Stage1Response>
            {
                new() { Model = "gpt-4", Response = "Response" }
            },
            Stage3 = null // Not yet complete
        };

        await _service.SaveAssistantMessageAsync(conversation.Id, incompleteMessage);

        var loaded = await _service.LoadConversationAsync(conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(1, loaded.Messages.Count); // Only user message
    }

    [TestMethod]
    public async Task ListConversationsAsync_ReturnsConversations()
    {
        // Create conversations with unique titles to identify them
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        await _service.CreateConversationAsync($"Chat_{uniqueId}_1");
        await _service.CreateConversationAsync($"Chat_{uniqueId}_2");
        await _service.CreateConversationAsync($"Chat_{uniqueId}_3");

        var list = await _service.ListConversationsAsync();

        // Verify at least our 3 conversations exist
        Assert.IsTrue(list.Count >= 3, "Should have at least 3 conversations");
        var ourChats = list.Where(c => c.Title.Contains(uniqueId)).ToList();
        Assert.AreEqual(3, ourChats.Count, "Should find exactly our 3 conversations");
    }

    [TestMethod]
    public async Task LoadConversationAsync_NonExistent_ReturnsNull()
    {
        var result = await _service.LoadConversationAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateTitleAsync_UpdatesTitle_CanBeRetrieved()
    {
        var conversation = await _service.CreateConversationAsync();

        await _service.UpdateTitleAsync(conversation.Id, "Updated Title");

        var loaded = await _service.LoadConversationAsync(conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("Updated Title", loaded.Title);
    }

    [TestMethod]
    public async Task DeleteConversationAsync_RemovesConversation()
    {
        var conversation = await _service.CreateConversationAsync();
        var userMessage = new UserMessage { Content = "Test" };
        await _service.SaveUserMessageAsync(conversation.Id, userMessage);

        await _service.DeleteConversationAsync(conversation.Id);

        var loaded = await _service.LoadConversationAsync(conversation.Id);
        Assert.IsNull(loaded);
    }

    [TestMethod]
    public async Task FullConversationFlow_MultipleMessages_WorksCorrectly()
    {
        // Create conversation
        var conversation = await _service.CreateConversationAsync();

        // First exchange
        var user1 = new UserMessage { Content = "First question" };
        await _service.SaveUserMessageAsync(conversation.Id, user1);

        var assistant1 = new AssistantMessage
        {
            Stage1 = new List<Stage1Response> { new() { Model = "gpt-4", Response = "First response" } },
            Stage2 = new List<Stage2Ranking>(),
            Stage3 = new Stage3Response { Model = "gpt-4", Response = "First synthesis" }
        };
        await _service.SaveAssistantMessageAsync(conversation.Id, assistant1);

        // Second exchange
        var user2 = new UserMessage { Content = "Follow-up question" };
        await _service.SaveUserMessageAsync(conversation.Id, user2);

        var assistant2 = new AssistantMessage
        {
            Stage1 = new List<Stage1Response> { new() { Model = "claude-3", Response = "Second response" } },
            Stage2 = new List<Stage2Ranking>(),
            Stage3 = new Stage3Response { Model = "claude-3", Response = "Second synthesis" }
        };
        await _service.SaveAssistantMessageAsync(conversation.Id, assistant2);

        // Update title
        await _service.UpdateTitleAsync(conversation.Id, "Multi-turn conversation");

        // Load and verify
        var loaded = await _service.LoadConversationAsync(conversation.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("Multi-turn conversation", loaded.Title);
        Assert.AreEqual(4, loaded.Messages.Count);

        Assert.IsInstanceOfType(loaded.Messages[0], typeof(UserMessage));
        Assert.IsInstanceOfType(loaded.Messages[1], typeof(AssistantMessage));
        Assert.IsInstanceOfType(loaded.Messages[2], typeof(UserMessage));
        Assert.IsInstanceOfType(loaded.Messages[3], typeof(AssistantMessage));
    }
}
