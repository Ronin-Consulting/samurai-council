using SamurAICouncil.Data.Tests.Fixtures;

namespace SamurAICouncil.Data.Tests.Repositories;

[TestClass]
public class ConversationRepositoryTests : IntegrationTestBase
{
    [TestMethod]
    public async Task CreateConversationAsync_CreatesAndReturnsConversation()
    {
        // Arrange
        var repo = CreateConversationRepository();

        // Act
        var conversation = await repo.CreateConversationAsync();

        // Assert
        Assert.IsNotNull(conversation);
        Assert.AreNotEqual(Guid.Empty, conversation.Id);
        Assert.AreEqual("New Conversation", conversation.Title);
        Assert.IsTrue(conversation.CreatedAt <= DateTime.UtcNow);
        Assert.IsTrue(conversation.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [TestMethod]
    public async Task GetConversationAsync_ReturnsNullForNonExistentId()
    {
        // Arrange
        var repo = CreateConversationRepository();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repo.GetConversationAsync(nonExistentId);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetConversationAsync_ReturnsConversation()
    {
        // Arrange
        var repo = CreateConversationRepository();
        var created = await repo.CreateConversationAsync();

        // Act
        var result = await repo.GetConversationAsync(created.Id);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
        Assert.AreEqual(created.Title, result.Title);
    }

    [TestMethod]
    public async Task ListConversationsAsync_ReturnsEmptyListWhenNoConversations()
    {
        // Arrange
        var repo = CreateConversationRepository();

        // Act
        var result = await repo.ListConversationsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ListConversationsAsync_ReturnsMetadataSortedByDateDescending()
    {
        // Arrange
        var repo = CreateConversationRepository();
        var conv1 = await repo.CreateConversationAsync();
        await Task.Delay(10); // Ensure different timestamps
        var conv2 = await repo.CreateConversationAsync();
        await Task.Delay(10);
        var conv3 = await repo.CreateConversationAsync();

        // Act
        var result = await repo.ListConversationsAsync();

        // Assert
        Assert.AreEqual(3, result.Count);
        // Should be sorted newest first
        Assert.AreEqual(conv3.Id, result[0].Id);
        Assert.AreEqual(conv2.Id, result[1].Id);
        Assert.AreEqual(conv1.Id, result[2].Id);
    }

    [TestMethod]
    public async Task ListConversationsAsync_IncludesMessageCount()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();

        var conv = await convRepo.CreateConversationAsync();
        await msgRepo.AddUserMessageAsync(conv.Id, "Hello");
        await msgRepo.AddUserMessageAsync(conv.Id, "World");

        // Act
        var result = await convRepo.ListConversationsAsync();

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result[0].MessageCount);
    }

    [TestMethod]
    public async Task UpdateConversationTitleAsync_UpdatesTitle()
    {
        // Arrange
        var repo = CreateConversationRepository();
        var conv = await repo.CreateConversationAsync();
        var newTitle = "Updated Title";

        // Act
        await repo.UpdateConversationTitleAsync(conv.Id, newTitle);

        // Assert
        var updated = await repo.GetConversationAsync(conv.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual(newTitle, updated.Title);
    }

    [TestMethod]
    public async Task DeleteConversationAsync_DeletesConversation()
    {
        // Arrange
        var repo = CreateConversationRepository();
        var conv = await repo.CreateConversationAsync();

        // Act
        await repo.DeleteConversationAsync(conv.Id);

        // Assert
        var result = await repo.GetConversationAsync(conv.Id);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteConversationAsync_CascadesDeleteToMessages()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();

        var conv = await convRepo.CreateConversationAsync();
        await msgRepo.AddUserMessageAsync(conv.Id, "Test message");

        // Verify message exists
        var messagesBefore = await msgRepo.GetMessagesForConversationAsync(conv.Id);
        Assert.AreEqual(1, messagesBefore.Count);

        // Act
        await convRepo.DeleteConversationAsync(conv.Id);

        // Assert - messages should be deleted due to CASCADE
        var messagesAfter = await msgRepo.GetMessagesForConversationAsync(conv.Id);
        Assert.AreEqual(0, messagesAfter.Count);
    }
}
