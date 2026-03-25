using SamurAICouncil.Core.Models;
using SamurAICouncil.Data.Tests.Fixtures;

namespace SamurAICouncil.Data.Tests.Repositories;

[TestClass]
public class MessageRepositoryTests : IntegrationTestBase
{
    [TestMethod]
    public async Task AddUserMessageAsync_CreatesAndReturnsUserMessage()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();
        var conv = await convRepo.CreateConversationAsync();
        var content = "Hello, World!";

        // Act
        var message = await msgRepo.AddUserMessageAsync(conv.Id, content);

        // Assert
        Assert.IsNotNull(message);
        Assert.IsInstanceOfType(message, typeof(UserMessage));
        Assert.AreEqual(content, message.Content);
    }

    [TestMethod]
    public async Task AddAssistantMessageAsync_CreatesAndReturnsAssistantMessage()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();
        var conv = await convRepo.CreateConversationAsync();

        var stage1 = CreateSampleStage1Responses();
        var stage2 = CreateSampleStage2Rankings();
        var stage3 = CreateSampleStage3Response();
        var metadata = CreateSampleMetadata();

        // Act
        var message = await msgRepo.AddAssistantMessageAsync(
            conv.Id, stage1, stage2, stage3, metadata);

        // Assert
        Assert.IsNotNull(message);
        Assert.IsInstanceOfType(message, typeof(AssistantMessage));
        Assert.AreEqual(3, message.Stage1.Count);
        Assert.AreEqual(3, message.Stage2.Count);
        Assert.IsNotNull(message.Stage3);
        Assert.IsNotNull(message.Metadata);
    }

    [TestMethod]
    public async Task AddAssistantMessageAsync_WithNullMetadata_Succeeds()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();
        var conv = await convRepo.CreateConversationAsync();

        var stage1 = CreateSampleStage1Responses();
        var stage2 = CreateSampleStage2Rankings();
        var stage3 = CreateSampleStage3Response();

        // Act
        var message = await msgRepo.AddAssistantMessageAsync(
            conv.Id, stage1, stage2, stage3, metadata: null);

        // Assert
        Assert.IsNotNull(message);
        Assert.IsNull(message.Metadata);
    }

    [TestMethod]
    public async Task GetMessagesForConversationAsync_ReturnsEmptyListWhenNoMessages()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();
        var conv = await convRepo.CreateConversationAsync();

        // Act
        var messages = await msgRepo.GetMessagesForConversationAsync(conv.Id);

        // Assert
        Assert.IsNotNull(messages);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public async Task GetMessagesForConversationAsync_ReturnsEmptyListForNonExistentConversation()
    {
        // Arrange
        var msgRepo = CreateMessageRepository();
        var nonExistentId = Guid.NewGuid();

        // Act
        var messages = await msgRepo.GetMessagesForConversationAsync(nonExistentId);

        // Assert
        Assert.IsNotNull(messages);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public async Task GetMessagesForConversationAsync_ReturnsUserMessages()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();
        var conv = await convRepo.CreateConversationAsync();

        await msgRepo.AddUserMessageAsync(conv.Id, "First message");
        await msgRepo.AddUserMessageAsync(conv.Id, "Second message");

        // Act
        var messages = await msgRepo.GetMessagesForConversationAsync(conv.Id);

        // Assert
        Assert.AreEqual(2, messages.Count);
        Assert.IsInstanceOfType(messages[0], typeof(UserMessage));
        Assert.IsInstanceOfType(messages[1], typeof(UserMessage));
        Assert.AreEqual("First message", ((UserMessage)messages[0]).Content);
        Assert.AreEqual("Second message", ((UserMessage)messages[1]).Content);
    }

    [TestMethod]
    public async Task GetMessagesForConversationAsync_ReturnsAssistantMessages()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();
        var conv = await convRepo.CreateConversationAsync();

        var stage1 = CreateSampleStage1Responses();
        var stage2 = CreateSampleStage2Rankings();
        var stage3 = CreateSampleStage3Response();
        var metadata = CreateSampleMetadata();

        await msgRepo.AddAssistantMessageAsync(conv.Id, stage1, stage2, stage3, metadata);

        // Act
        var messages = await msgRepo.GetMessagesForConversationAsync(conv.Id);

        // Assert
        Assert.AreEqual(1, messages.Count);
        var assistantMsg = messages[0] as AssistantMessage;
        Assert.IsNotNull(assistantMsg);

        // Verify Stage1 deserialization
        Assert.AreEqual(3, assistantMsg.Stage1.Count);
        Assert.AreEqual("gpt-4", assistantMsg.Stage1[0].Model);
        Assert.AreEqual("Response from GPT-4", assistantMsg.Stage1[0].Response);

        // Verify Stage2 deserialization
        Assert.AreEqual(3, assistantMsg.Stage2.Count);
        Assert.AreEqual("gpt-4", assistantMsg.Stage2[0].Model);
        Assert.AreEqual("B, A, C", assistantMsg.Stage2[0].Ranking);
        Assert.AreEqual(3, assistantMsg.Stage2[0].ParsedRanking.Count);

        // Verify Stage3 deserialization
        Assert.IsNotNull(assistantMsg.Stage3);
        Assert.AreEqual("gpt-4-chairman", assistantMsg.Stage3.Model);
        Assert.AreEqual("Final synthesized response", assistantMsg.Stage3.Response);

        // Verify Metadata deserialization
        Assert.IsNotNull(assistantMsg.Metadata);
        Assert.AreEqual(3, assistantMsg.Metadata.AggregateRankings.Count);
    }

    [TestMethod]
    public async Task GetMessagesForConversationAsync_ReturnsMixedMessagesInOrder()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();
        var conv = await convRepo.CreateConversationAsync();

        var stage1 = CreateSampleStage1Responses();
        var stage2 = CreateSampleStage2Rankings();
        var stage3 = CreateSampleStage3Response();

        // Create messages in order: user, assistant, user, assistant
        await msgRepo.AddUserMessageAsync(conv.Id, "Question 1");
        await Task.Delay(10); // Ensure different timestamps
        await msgRepo.AddAssistantMessageAsync(conv.Id, stage1, stage2, stage3);
        await Task.Delay(10);
        await msgRepo.AddUserMessageAsync(conv.Id, "Question 2");
        await Task.Delay(10);
        await msgRepo.AddAssistantMessageAsync(conv.Id, stage1, stage2, stage3);

        // Act
        var messages = await msgRepo.GetMessagesForConversationAsync(conv.Id);

        // Assert
        Assert.AreEqual(4, messages.Count);
        Assert.IsInstanceOfType(messages[0], typeof(UserMessage));
        Assert.IsInstanceOfType(messages[1], typeof(AssistantMessage));
        Assert.IsInstanceOfType(messages[2], typeof(UserMessage));
        Assert.IsInstanceOfType(messages[3], typeof(AssistantMessage));
        Assert.AreEqual("Question 1", ((UserMessage)messages[0]).Content);
        Assert.AreEqual("Question 2", ((UserMessage)messages[2]).Content);
    }

    [TestMethod]
    public async Task GetMessagesForConversationAsync_OnlyReturnsMessagesForSpecifiedConversation()
    {
        // Arrange
        var convRepo = CreateConversationRepository();
        var msgRepo = CreateMessageRepository();

        var conv1 = await convRepo.CreateConversationAsync();
        var conv2 = await convRepo.CreateConversationAsync();

        await msgRepo.AddUserMessageAsync(conv1.Id, "Message for conv1");
        await msgRepo.AddUserMessageAsync(conv2.Id, "Message for conv2");
        await msgRepo.AddUserMessageAsync(conv1.Id, "Another message for conv1");

        // Act
        var messages1 = await msgRepo.GetMessagesForConversationAsync(conv1.Id);
        var messages2 = await msgRepo.GetMessagesForConversationAsync(conv2.Id);

        // Assert
        Assert.AreEqual(2, messages1.Count);
        Assert.AreEqual(1, messages2.Count);
        Assert.AreEqual("Message for conv1", ((UserMessage)messages1[0]).Content);
        Assert.AreEqual("Message for conv2", ((UserMessage)messages2[0]).Content);
    }

    #region Helper Methods

    private static List<Stage1Response> CreateSampleStage1Responses()
    {
        return
        [
            new Stage1Response { Model = "gpt-4", Response = "Response from GPT-4" },
            new Stage1Response { Model = "claude-3", Response = "Response from Claude" },
            new Stage1Response { Model = "gemini-pro", Response = "Response from Gemini" }
        ];
    }

    private static List<Stage2Ranking> CreateSampleStage2Rankings()
    {
        return
        [
            new Stage2Ranking
            {
                Model = "gpt-4",
                Ranking = "B, A, C",
                ParsedRanking = ["Response B", "Response A", "Response C"]
            },
            new Stage2Ranking
            {
                Model = "claude-3",
                Ranking = "A, B, C",
                ParsedRanking = ["Response A", "Response B", "Response C"]
            },
            new Stage2Ranking
            {
                Model = "gemini-pro",
                Ranking = "B, C, A",
                ParsedRanking = ["Response B", "Response C", "Response A"]
            }
        ];
    }

    private static Stage3Response CreateSampleStage3Response()
    {
        return new Stage3Response
        {
            Model = "gpt-4-chairman",
            Response = "Final synthesized response"
        };
    }

    private static CouncilMetadata CreateSampleMetadata()
    {
        return new CouncilMetadata
        {
            LabelToModel = new Dictionary<string, string>
            {
                ["Response A"] = "gpt-4",
                ["Response B"] = "claude-3",
                ["Response C"] = "gemini-pro"
            },
            AggregateRankings =
            [
                new AggregateRanking { Model = "gpt-4", AverageRank = 1.5, RankingsCount = 3 },
                new AggregateRanking { Model = "claude-3", AverageRank = 2.0, RankingsCount = 3 },
                new AggregateRanking { Model = "gemini-pro", AverageRank = 2.5, RankingsCount = 3 }
            ]
        };
    }

    #endregion
}
