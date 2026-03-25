using System.Text.Json;
using Dapper;
using Npgsql;
using SamurAICouncil.Core.Interfaces;
using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Data.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _jsonOptions;

    public MessageRepository(string connectionString)
    {
        _connectionString = connectionString;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<UserMessage> AddUserMessageAsync(
        Guid conversationId,
        string content,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO messages (id, conversation_id, role, content, created_at)
            VALUES (@Id, @ConversationId, @Role, @Content, @CreatedAt)
            """;

        var message = new UserMessage { Content = content };
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            ConversationId = conversationId,
            Role = "user",
            Content = content,
            CreatedAt = createdAt
        });

        return message;
    }

    public async Task<AssistantMessage> AddAssistantMessageAsync(
        Guid conversationId,
        List<Stage1Response> stage1,
        List<Stage2Ranking> stage2,
        Stage3Response stage3,
        CouncilMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO messages (id, conversation_id, role, stage1, stage2, stage3, metadata, created_at)
            VALUES (@Id, @ConversationId, @Role, @Stage1::jsonb, @Stage2::jsonb, @Stage3::jsonb, @Metadata::jsonb, @CreatedAt)
            """;

        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            ConversationId = conversationId,
            Role = "assistant",
            Stage1 = JsonSerializer.Serialize(stage1, _jsonOptions),
            Stage2 = JsonSerializer.Serialize(stage2, _jsonOptions),
            Stage3 = JsonSerializer.Serialize(stage3, _jsonOptions),
            Metadata = metadata != null ? JsonSerializer.Serialize(metadata, _jsonOptions) : null,
            CreatedAt = createdAt
        });

        return new AssistantMessage
        {
            Stage1 = stage1,
            Stage2 = stage2,
            Stage3 = stage3,
            Metadata = metadata
        };
    }

    public async Task<List<Message>> GetMessagesForConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, conversation_id, role, content, stage1, stage2, stage3, metadata, created_at
            FROM messages
            WHERE conversation_id = @ConversationId
            ORDER BY created_at ASC
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MessageRow>(sql, new { ConversationId = conversationId });

        var messages = new List<Message>();

        foreach (var row in rows)
        {
            if (row.role == "user")
            {
                messages.Add(new UserMessage { Content = row.content ?? string.Empty });
            }
            else if (row.role == "assistant")
            {
                var assistantMessage = new AssistantMessage
                {
                    Stage1 = DeserializeJson<List<Stage1Response>>(row.stage1),
                    Stage2 = DeserializeJson<List<Stage2Ranking>>(row.stage2),
                    Stage3 = DeserializeJson<Stage3Response>(row.stage3),
                    Metadata = DeserializeJson<CouncilMetadata>(row.metadata)
                };
                messages.Add(assistantMessage);
            }
        }

        return messages;
    }

    private T? DeserializeJson<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    // Internal row type for Dapper mapping
    private record MessageRow(
        Guid id,
        Guid conversation_id,
        string role,
        string? content,
        string? stage1,
        string? stage2,
        string? stage3,
        string? metadata,
        DateTime created_at
    );
}
