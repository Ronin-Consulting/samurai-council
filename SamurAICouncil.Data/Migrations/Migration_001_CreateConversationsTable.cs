using FluentMigrator;

namespace SamurAICouncil.Data.Migrations;

[Migration(1)]
public class Migration_001_CreateConversationsTable : Migration
{
    public override void Up()
    {
        Create.Table("conversations")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("title").AsString(255).NotNullable().WithDefaultValue("New Conversation");

        // Index for listing conversations sorted by creation date
        Create.Index("ix_conversations_created_at")
            .OnTable("conversations")
            .OnColumn("created_at")
            .Descending();
    }

    public override void Down()
    {
        Delete.Index("ix_conversations_created_at").OnTable("conversations");
        Delete.Table("conversations");
    }
}
