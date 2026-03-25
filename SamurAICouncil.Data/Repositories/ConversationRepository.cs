using Dapper;
using Npgsql;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Data.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly string _connectionString;

    public ConversationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Conversation> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO conversations (id, created_at, title)
            VALUES (@Id, @CreatedAt, @Title)
            RETURNING id, created_at, title
            """;

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Title = "New Conversation"
        };

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(sql, new
        {
            conversation.Id,
            conversation.CreatedAt,
            conversation.Title
        });

        return conversation;
    }

    public async Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, created_at, title
            FROM conversations
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ConversationRow>(sql, new { Id = id });

        if (row == null)
            return null;

        return new Conversation
        {
            Id = row.id,
            CreatedAt = row.created_at,
            Title = row.title,
            Messages = [] // Messages loaded separately
        };
    }

    public async Task<List<ConversationMetadata>> ListConversationsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.id,
                c.created_at,
                c.title,
                COUNT(m.id) as message_count
            FROM conversations c
            LEFT JOIN messages m ON m.conversation_id = c.id
            GROUP BY c.id, c.created_at, c.title
            ORDER BY c.created_at DESC
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ConversationMetadataRow>(sql);

        return rows.Select(r => new ConversationMetadata
        {
            Id = r.id,
            CreatedAt = r.created_at,
            Title = r.title,
            MessageCount = (int)r.message_count
        }).ToList();
    }

    public async Task UpdateConversationTitleAsync(Guid id, string title, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE conversations
            SET title = @Title
            WHERE id = @Id
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(sql, new { Id = id, Title = title });
    }

    public async Task DeleteConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM conversations WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<int> DeleteConversationsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return 0;

        const string sql = "DELETE FROM conversations WHERE id = ANY(@Ids)";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(sql, new { Ids = idList });
    }

    // Internal row types for Dapper mapping (snake_case columns)
    private record ConversationRow(Guid id, DateTime created_at, string title);
    private record ConversationMetadataRow(Guid id, DateTime created_at, string title, long message_count);
}
