using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.Services.DataAccess.Migrations
{
    /// <summary>
    /// Add denormalized fields to Forum (Board) and LastUpdateDate to ForumTopic
    /// </summary>
    public partial class AddBoardDenormalizedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add LastUpdateDate to ForumTopics
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUpdateDate",
                table: "ForumTopics",
                nullable: true);

            // Add denormalized fields to Fora (Boards)
            migrationBuilder.AddColumn<int>(
                name: "TopicsCount",
                table: "Fora",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CommentsCount",
                table: "Fora",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LastCommentId",
                table: "Fora",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastCommentTopicId",
                table: "Fora",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastCommentAuthorId",
                table: "Fora",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastCommentDate",
                table: "Fora",
                nullable: true);

            // Populate TopicsCount
            migrationBuilder.Sql(@"
                UPDATE ""Fora"" f
                SET ""TopicsCount"" = (
                    SELECT COUNT(*)
                    FROM ""ForumTopics"" t
                    WHERE t.""ForumId"" = f.""ForumId"" AND t.""IsRemoved"" = false
                )
            ");

            // Populate CommentsCount
            migrationBuilder.Sql(@"
                UPDATE ""Fora"" f
                SET ""CommentsCount"" = (
                    SELECT COUNT(*)
                    FROM ""Comments"" c
                    INNER JOIN ""ForumTopics"" t ON c.""EntityId"" = t.""ForumTopicId""
                    WHERE t.""ForumId"" = f.""ForumId""
                      AND t.""IsRemoved"" = false
                      AND c.""IsRemoved"" = false
                )
            ");

            // Populate LastComment fields
            migrationBuilder.Sql(@"
                UPDATE ""Fora"" f
                SET
                    ""LastCommentId"" = lc.""CommentId"",
                    ""LastCommentTopicId"" = lc.""EntityId"",
                    ""LastCommentAuthorId"" = lc.""UserId"",
                    ""LastCommentDate"" = lc.""CreateDate""
                FROM (
                    SELECT DISTINCT ON (t.""ForumId"")
                        t.""ForumId"",
                        c.""CommentId"",
                        c.""EntityId"",
                        c.""UserId"",
                        c.""CreateDate""
                    FROM ""Comments"" c
                    INNER JOIN ""ForumTopics"" t ON c.""EntityId"" = t.""ForumTopicId""
                    WHERE t.""IsRemoved"" = false AND c.""IsRemoved"" = false
                    ORDER BY t.""ForumId"", c.""CreateDate"" DESC
                ) lc
                WHERE f.""ForumId"" = lc.""ForumId""
            ");

            // Add indexes for foreign keys
            migrationBuilder.CreateIndex(
                name: "IX_Fora_LastCommentId",
                table: "Fora",
                column: "LastCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Fora_LastCommentAuthorId",
                table: "Fora",
                column: "LastCommentAuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Fora_Comments_LastCommentId",
                table: "Fora",
                column: "LastCommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Fora_Users_LastCommentAuthorId",
                table: "Fora",
                column: "LastCommentAuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fora_Comments_LastCommentId",
                table: "Fora");

            migrationBuilder.DropForeignKey(
                name: "FK_Fora_Users_LastCommentAuthorId",
                table: "Fora");

            migrationBuilder.DropIndex(
                name: "IX_Fora_LastCommentId",
                table: "Fora");

            migrationBuilder.DropIndex(
                name: "IX_Fora_LastCommentAuthorId",
                table: "Fora");

            migrationBuilder.DropColumn(
                name: "LastUpdateDate",
                table: "ForumTopics");

            migrationBuilder.DropColumn(
                name: "TopicsCount",
                table: "Fora");

            migrationBuilder.DropColumn(
                name: "CommentsCount",
                table: "Fora");

            migrationBuilder.DropColumn(
                name: "LastCommentId",
                table: "Fora");

            migrationBuilder.DropColumn(
                name: "LastCommentTopicId",
                table: "Fora");

            migrationBuilder.DropColumn(
                name: "LastCommentAuthorId",
                table: "Fora");

            migrationBuilder.DropColumn(
                name: "LastCommentDate",
                table: "Fora");
        }
    }
}
