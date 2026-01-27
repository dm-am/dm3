using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Add audit fields (DeletedByUserId, DeletedAtUtc, LastUpdateUserId) and edit history tables
/// for Comments, ForumTopics, Posts, and Characters
/// </summary>
public partial class AddAuditFieldsAndEditHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ========== Comments ==========
        // Add audit columns
        migrationBuilder.AddColumn<Guid>(
            name: "LastUpdateUserId",
            table: "Comments",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DeletedByUserId",
            table: "Comments",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "Comments",
            type: "timestamp with time zone",
            nullable: true);

        // Create CommentEdits table
        migrationBuilder.CreateTable(
            name: "CommentEdits",
            columns: table => new
            {
                CommentEditId = table.Column<Guid>(type: "uuid", nullable: false),
                CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommentEdits", x => x.CommentEditId);
                table.ForeignKey(
                    name: "FK_CommentEdits_Comments_CommentId",
                    column: x => x.CommentId,
                    principalTable: "Comments",
                    principalColumn: "CommentId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CommentEdits_Users_EditorUserId",
                    column: x => x.EditorUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Cascade);
            });

        // Indexes for Comments
        migrationBuilder.CreateIndex(
            name: "IX_Comments_LastUpdateUserId",
            table: "Comments",
            column: "LastUpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Comments_DeletedByUserId",
            table: "Comments",
            column: "DeletedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_CommentEdits_CommentId",
            table: "CommentEdits",
            column: "CommentId");

        migrationBuilder.CreateIndex(
            name: "IX_CommentEdits_EditorUserId",
            table: "CommentEdits",
            column: "EditorUserId");

        // FKs for Comments
        migrationBuilder.AddForeignKey(
            name: "FK_Comments_Users_LastUpdateUserId",
            table: "Comments",
            column: "LastUpdateUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Comments_Users_DeletedByUserId",
            table: "Comments",
            column: "DeletedByUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        // ========== ForumTopics ==========
        // Add audit columns
        migrationBuilder.AddColumn<Guid>(
            name: "LastUpdateUserId",
            table: "ForumTopics",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DeletedByUserId",
            table: "ForumTopics",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "ForumTopics",
            type: "timestamp with time zone",
            nullable: true);

        // Create TopicEdits table
        migrationBuilder.CreateTable(
            name: "TopicEdits",
            columns: table => new
            {
                TopicEditId = table.Column<Guid>(type: "uuid", nullable: false),
                ForumTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TopicEdits", x => x.TopicEditId);
                table.ForeignKey(
                    name: "FK_TopicEdits_ForumTopics_ForumTopicId",
                    column: x => x.ForumTopicId,
                    principalTable: "ForumTopics",
                    principalColumn: "ForumTopicId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TopicEdits_Users_EditorUserId",
                    column: x => x.EditorUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Cascade);
            });

        // Indexes for ForumTopics
        migrationBuilder.CreateIndex(
            name: "IX_ForumTopics_LastUpdateUserId",
            table: "ForumTopics",
            column: "LastUpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ForumTopics_DeletedByUserId",
            table: "ForumTopics",
            column: "DeletedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_TopicEdits_ForumTopicId",
            table: "TopicEdits",
            column: "ForumTopicId");

        migrationBuilder.CreateIndex(
            name: "IX_TopicEdits_EditorUserId",
            table: "TopicEdits",
            column: "EditorUserId");

        // FKs for ForumTopics
        migrationBuilder.AddForeignKey(
            name: "FK_ForumTopics_Users_LastUpdateUserId",
            table: "ForumTopics",
            column: "LastUpdateUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_ForumTopics_Users_DeletedByUserId",
            table: "ForumTopics",
            column: "DeletedByUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        // ========== Posts ==========
        // Posts already has LastUpdateUserId, add only DeletedByUserId and DeletedAtUtc
        migrationBuilder.AddColumn<Guid>(
            name: "DeletedByUserId",
            table: "Posts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "Posts",
            type: "timestamp with time zone",
            nullable: true);

        // Create PostEdits table
        migrationBuilder.CreateTable(
            name: "PostEdits",
            columns: table => new
            {
                PostEditId = table.Column<Guid>(type: "uuid", nullable: false),
                PostId = table.Column<Guid>(type: "uuid", nullable: false),
                EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PostEdits", x => x.PostEditId);
                table.ForeignKey(
                    name: "FK_PostEdits_Posts_PostId",
                    column: x => x.PostId,
                    principalTable: "Posts",
                    principalColumn: "PostId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PostEdits_Users_EditorUserId",
                    column: x => x.EditorUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Cascade);
            });

        // Indexes for Posts
        migrationBuilder.CreateIndex(
            name: "IX_Posts_DeletedByUserId",
            table: "Posts",
            column: "DeletedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PostEdits_PostId",
            table: "PostEdits",
            column: "PostId");

        migrationBuilder.CreateIndex(
            name: "IX_PostEdits_EditorUserId",
            table: "PostEdits",
            column: "EditorUserId");

        // FK for Posts
        migrationBuilder.AddForeignKey(
            name: "FK_Posts_Users_DeletedByUserId",
            table: "Posts",
            column: "DeletedByUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        // ========== Characters ==========
        // Add audit columns
        migrationBuilder.AddColumn<Guid>(
            name: "LastUpdateUserId",
            table: "Characters",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DeletedByUserId",
            table: "Characters",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "Characters",
            type: "timestamp with time zone",
            nullable: true);

        // Create CharacterEdits table
        migrationBuilder.CreateTable(
            name: "CharacterEdits",
            columns: table => new
            {
                CharacterEditId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CharacterEdits", x => x.CharacterEditId);
                table.ForeignKey(
                    name: "FK_CharacterEdits_Characters_CharacterId",
                    column: x => x.CharacterId,
                    principalTable: "Characters",
                    principalColumn: "CharacterId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CharacterEdits_Users_EditorUserId",
                    column: x => x.EditorUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Cascade);
            });

        // Indexes for Characters
        migrationBuilder.CreateIndex(
            name: "IX_Characters_LastUpdateUserId",
            table: "Characters",
            column: "LastUpdateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Characters_DeletedByUserId",
            table: "Characters",
            column: "DeletedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_CharacterEdits_CharacterId",
            table: "CharacterEdits",
            column: "CharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_CharacterEdits_EditorUserId",
            table: "CharacterEdits",
            column: "EditorUserId");

        // FKs for Characters
        migrationBuilder.AddForeignKey(
            name: "FK_Characters_Users_LastUpdateUserId",
            table: "Characters",
            column: "LastUpdateUserId",
            principalTable: "Users",
            principalColumn: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Characters_Users_DeletedByUserId",
            table: "Characters",
            column: "DeletedByUserId",
            principalTable: "Users",
            principalColumn: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ========== Characters ==========
        migrationBuilder.DropForeignKey(name: "FK_Characters_Users_LastUpdateUserId", table: "Characters");
        migrationBuilder.DropForeignKey(name: "FK_Characters_Users_DeletedByUserId", table: "Characters");
        migrationBuilder.DropTable(name: "CharacterEdits");
        migrationBuilder.DropIndex(name: "IX_Characters_LastUpdateUserId", table: "Characters");
        migrationBuilder.DropIndex(name: "IX_Characters_DeletedByUserId", table: "Characters");
        migrationBuilder.DropColumn(name: "LastUpdateUserId", table: "Characters");
        migrationBuilder.DropColumn(name: "DeletedByUserId", table: "Characters");
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "Characters");

        // ========== Posts ==========
        migrationBuilder.DropForeignKey(name: "FK_Posts_Users_DeletedByUserId", table: "Posts");
        migrationBuilder.DropTable(name: "PostEdits");
        migrationBuilder.DropIndex(name: "IX_Posts_DeletedByUserId", table: "Posts");
        migrationBuilder.DropColumn(name: "DeletedByUserId", table: "Posts");
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "Posts");

        // ========== ForumTopics ==========
        migrationBuilder.DropForeignKey(name: "FK_ForumTopics_Users_LastUpdateUserId", table: "ForumTopics");
        migrationBuilder.DropForeignKey(name: "FK_ForumTopics_Users_DeletedByUserId", table: "ForumTopics");
        migrationBuilder.DropTable(name: "TopicEdits");
        migrationBuilder.DropIndex(name: "IX_ForumTopics_LastUpdateUserId", table: "ForumTopics");
        migrationBuilder.DropIndex(name: "IX_ForumTopics_DeletedByUserId", table: "ForumTopics");
        migrationBuilder.DropColumn(name: "LastUpdateUserId", table: "ForumTopics");
        migrationBuilder.DropColumn(name: "DeletedByUserId", table: "ForumTopics");
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "ForumTopics");

        // ========== Comments ==========
        migrationBuilder.DropForeignKey(name: "FK_Comments_Users_LastUpdateUserId", table: "Comments");
        migrationBuilder.DropForeignKey(name: "FK_Comments_Users_DeletedByUserId", table: "Comments");
        migrationBuilder.DropTable(name: "CommentEdits");
        migrationBuilder.DropIndex(name: "IX_Comments_LastUpdateUserId", table: "Comments");
        migrationBuilder.DropIndex(name: "IX_Comments_DeletedByUserId", table: "Comments");
        migrationBuilder.DropColumn(name: "LastUpdateUserId", table: "Comments");
        migrationBuilder.DropColumn(name: "DeletedByUserId", table: "Comments");
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "Comments");
    }
}
