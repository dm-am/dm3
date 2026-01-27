using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Unify ChatMessages and Messages tables into a single Messages table
/// </summary>
public partial class UnifyMessagesAndChatMessages : Migration
{
    private static readonly Guid GlobalChatId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Create virtual Conversation for GlobalChat (required for FK)
        migrationBuilder.Sql($@"
            INSERT INTO ""Conversations"" (""ConversationId"", ""LastMessageId"", ""Visavi"")
            SELECT '{GlobalChatId}', NULL, false
            WHERE NOT EXISTS (SELECT 1 FROM ""Conversations"" WHERE ""ConversationId"" = '{GlobalChatId}');
        ");

        // Step 2: Add new columns to Messages table
        migrationBuilder.AddColumn<Guid>(
            name: "DeletedByUserId",
            table: "Messages",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "Messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LastUpdateUserId",
            table: "Messages",
            type: "uuid",
            nullable: true);

        // Step 3: Create MessageEdits table
        migrationBuilder.CreateTable(
            name: "MessageEdits",
            columns: table => new
            {
                MessageEditId = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageEdits", x => x.MessageEditId);
                table.ForeignKey(
                    name: "FK_MessageEdits_Messages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Messages",
                    principalColumn: "MessageId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MessageEdits_Users_EditorUserId",
                    column: x => x.EditorUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Cascade);
            });

        // Step 4: Create indexes for new columns and table
        migrationBuilder.CreateIndex(
            name: "IX_Messages_DeletedByUserId",
            table: "Messages",
            column: "DeletedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Messages_LastUpdateUserId",
            table: "Messages",
            column: "LastUpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageEdits_MessageId",
            table: "MessageEdits",
            column: "MessageId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageEdits_EditorUserId",
            table: "MessageEdits",
            column: "EditorUserId");

        // Step 5: Add FKs for new columns
        migrationBuilder.AddForeignKey(
            name: "FK_Messages_Users_DeletedByUserId",
            table: "Messages",
            column: "DeletedByUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Messages_Users_LastUpdateUserId",
            table: "Messages",
            column: "LastUpdateUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        // Step 6: Migrate ChatMessages to Messages
        migrationBuilder.Sql($@"
            INSERT INTO ""Messages"" (""MessageId"", ""UserId"", ""ConversationId"", ""CreateDate"", ""Text"", ""IsRemoved"", ""LastUpdateDate"", ""LastUpdateUserId"", ""DeletedByUserId"", ""DeletedAtUtc"")
            SELECT
                ""ChatMessageId"",
                ""UserId"",
                '{GlobalChatId}',
                ""CreateDate"",
                ""Text"",
                COALESCE(""IsRemoved"", false),
                NULL,
                NULL,
                NULL,
                NULL
            FROM ""ChatMessages"";
        ");

        // Step 7: Migrate ChatMessageEdits to MessageEdits (if table exists)
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'ChatMessageEdits') THEN
                    INSERT INTO ""MessageEdits"" (""MessageEditId"", ""MessageId"", ""EditorUserId"", ""EditedAtUtc"")
                    SELECT ""ChatMessageEditId"", ""ChatMessageId"", ""EditorUserId"", ""EditedAtUtc""
                    FROM ""ChatMessageEdits"";
                END IF;
            END $$;
        ");

        // Step 8: Update Likes that reference ChatMessages to reference Messages
        // (Likes use EntityId which can point to different entities, no change needed)

        // Step 9: Update Warnings that reference ChatMessages
        // Warnings.EntityId can reference ChatMessages - those now point to Messages
        // (No actual FK change needed since it's by convention/polymorphic)

        // Step 10: Drop old tables
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'ChatMessageEdits') THEN
                    DROP TABLE ""ChatMessageEdits"";
                END IF;
            END $$;
        ");

        migrationBuilder.DropTable(
            name: "ChatMessages");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Step 1: Recreate ChatMessages table
        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                ChatMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreateDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Text = table.Column<string>(type: "text", nullable: true),
                IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.ChatMessageId);
                table.ForeignKey(
                    name: "FK_ChatMessages_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_UserId",
            table: "ChatMessages",
            column: "UserId");

        // Step 2: Recreate ChatMessageEdits table
        migrationBuilder.CreateTable(
            name: "ChatMessageEdits",
            columns: table => new
            {
                ChatMessageEditId = table.Column<Guid>(type: "uuid", nullable: false),
                ChatMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessageEdits", x => x.ChatMessageEditId);
                table.ForeignKey(
                    name: "FK_ChatMessageEdits_ChatMessages_ChatMessageId",
                    column: x => x.ChatMessageId,
                    principalTable: "ChatMessages",
                    principalColumn: "ChatMessageId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ChatMessageEdits_Users_EditorUserId",
                    column: x => x.EditorUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessageEdits_ChatMessageId",
            table: "ChatMessageEdits",
            column: "ChatMessageId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessageEdits_EditorUserId",
            table: "ChatMessageEdits",
            column: "EditorUserId");

        // Step 3: Migrate data back from Messages to ChatMessages
        migrationBuilder.Sql($@"
            INSERT INTO ""ChatMessages"" (""ChatMessageId"", ""UserId"", ""CreateDate"", ""Text"", ""IsRemoved"")
            SELECT ""MessageId"", ""UserId"", ""CreateDate"", ""Text"", ""IsRemoved""
            FROM ""Messages""
            WHERE ""ConversationId"" = '{GlobalChatId}';
        ");

        // Step 4: Migrate MessageEdits back to ChatMessageEdits
        migrationBuilder.Sql($@"
            INSERT INTO ""ChatMessageEdits"" (""ChatMessageEditId"", ""ChatMessageId"", ""EditorUserId"", ""EditedAtUtc"")
            SELECT me.""MessageEditId"", me.""MessageId"", me.""EditorUserId"", me.""EditedAtUtc""
            FROM ""MessageEdits"" me
            JOIN ""Messages"" m ON me.""MessageId"" = m.""MessageId""
            WHERE m.""ConversationId"" = '{GlobalChatId}';
        ");

        // Step 5: Delete global chat messages from Messages
        migrationBuilder.Sql($@"
            DELETE FROM ""Messages"" WHERE ""ConversationId"" = '{GlobalChatId}';
        ");

        // Step 6: Drop MessageEdits table
        migrationBuilder.DropTable(
            name: "MessageEdits");

        // Step 7: Drop FKs and new columns from Messages
        migrationBuilder.DropForeignKey(
            name: "FK_Messages_Users_DeletedByUserId",
            table: "Messages");

        migrationBuilder.DropForeignKey(
            name: "FK_Messages_Users_LastUpdateUserId",
            table: "Messages");

        migrationBuilder.DropIndex(
            name: "IX_Messages_DeletedByUserId",
            table: "Messages");

        migrationBuilder.DropIndex(
            name: "IX_Messages_LastUpdateUserId",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "DeletedByUserId",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "DeletedAtUtc",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "LastUpdateUserId",
            table: "Messages");

        // Step 8: Delete GlobalChat conversation
        migrationBuilder.Sql($@"
            DELETE FROM ""Conversations"" WHERE ""ConversationId"" = '{GlobalChatId}';
        ");
    }
}
