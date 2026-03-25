using SamurAICouncil.Data.Repositories;

namespace SamurAICouncil.Data.Tests.Fixtures;

/// <summary>
/// Base class for integration tests that require database access.
/// </summary>
[TestClass]
public abstract class IntegrationTestBase
{
    protected static string ConnectionString => PostgresFixture.ConnectionString;

    protected ConversationRepository CreateConversationRepository()
        => new(ConnectionString);

    protected MessageRepository CreateMessageRepository()
        => new(ConnectionString);

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
        // Clear data before each test for isolation
        await PostgresFixture.ClearDataAsync();
    }
}
