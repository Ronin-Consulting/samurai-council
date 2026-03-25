using FluentMigrator;

namespace SamurAICouncil.Data.Migrations;

[Migration(2)]
public class Migration_002_CreateMessagesTable : Migration
{
    public override void Up()
    {
        Create.Table("messages")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("conversation_id").AsGuid().NotNullable()
                .ForeignKey("fk_messages_conversation", "conversations", "id")
                .OnDelete(System.Data.Rule.Cascade)
            .WithColumn("role").AsString(20).NotNullable() // 'user' or 'assistant'
            .WithColumn("content").AsString(int.MaxValue).Nullable() // User message content
            .WithColumn("stage1").AsCustom("JSONB").Nullable() // Assistant stage 1 responses
            .WithColumn("stage2").AsCustom("JSONB").Nullable() // Assistant stage 2 rankings
            .WithColumn("stage3").AsCustom("JSONB").Nullable() // Assistant stage 3 final response
            .WithColumn("metadata").AsCustom("JSONB").Nullable() // Council metadata
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        // Index for retrieving messages by conversation
        Create.Index("ix_messages_conversation_id")
            .OnTable("messages")
            .OnColumn("conversation_id");

        // Index for ordering messages within a conversation
        Create.Index("ix_messages_conversation_created")
            .OnTable("messages")
            .OnColumn("conversation_id").Ascending()
            .OnColumn("created_at").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_messages_conversation_created").OnTable("messages");
        Delete.Index("ix_messages_conversation_id").OnTable("messages");
        Delete.ForeignKey("fk_messages_conversation").OnTable("messages");
        Delete.Table("messages");
    }
}
