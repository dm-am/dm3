using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <inheritdoc />
public partial class RenameForaToBoards : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Rename Fora table to Boards
        migrationBuilder.RenameTable(
            name: "Fora",
            newName: "Boards");

        // Rename ForumId to BoardId in Boards table
        migrationBuilder.RenameColumn(
            name: "ForumId",
            table: "Boards",
            newName: "BoardId");

        // Rename ForumModerators table to BoardModerators
        migrationBuilder.RenameTable(
            name: "ForumModerators",
            newName: "BoardModerators");

        // Rename ForumModeratorId to BoardModeratorId in BoardModerators
        migrationBuilder.RenameColumn(
            name: "ForumModeratorId",
            table: "BoardModerators",
            newName: "BoardModeratorId");

        // Rename ForumId to BoardId in BoardModerators
        migrationBuilder.RenameColumn(
            name: "ForumId",
            table: "BoardModerators",
            newName: "BoardId");

        // Rename ForumId to BoardId in ForumTopics
        migrationBuilder.RenameColumn(
            name: "ForumId",
            table: "ForumTopics",
            newName: "BoardId");

        // Rename indexes
        migrationBuilder.RenameIndex(
            name: "IX_ForumModerators_ForumId",
            table: "BoardModerators",
            newName: "IX_BoardModerators_BoardId");

        migrationBuilder.RenameIndex(
            name: "IX_ForumModerators_UserId",
            table: "BoardModerators",
            newName: "IX_BoardModerators_UserId");

        migrationBuilder.RenameIndex(
            name: "IX_ForumTopics_ForumId",
            table: "ForumTopics",
            newName: "IX_ForumTopics_BoardId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revert ForumTopics indexes
        migrationBuilder.RenameIndex(
            name: "IX_ForumTopics_BoardId",
            table: "ForumTopics",
            newName: "IX_ForumTopics_ForumId");

        // Revert BoardModerators indexes
        migrationBuilder.RenameIndex(
            name: "IX_BoardModerators_UserId",
            table: "BoardModerators",
            newName: "IX_ForumModerators_UserId");

        migrationBuilder.RenameIndex(
            name: "IX_BoardModerators_BoardId",
            table: "BoardModerators",
            newName: "IX_ForumModerators_ForumId");

        // Revert ForumId in ForumTopics
        migrationBuilder.RenameColumn(
            name: "BoardId",
            table: "ForumTopics",
            newName: "ForumId");

        // Revert BoardId to ForumId in BoardModerators
        migrationBuilder.RenameColumn(
            name: "BoardId",
            table: "BoardModerators",
            newName: "ForumId");

        // Revert BoardModeratorId to ForumModeratorId
        migrationBuilder.RenameColumn(
            name: "BoardModeratorId",
            table: "BoardModerators",
            newName: "ForumModeratorId");

        // Revert BoardModerators table to ForumModerators
        migrationBuilder.RenameTable(
            name: "BoardModerators",
            newName: "ForumModerators");

        // Revert BoardId to ForumId in Boards
        migrationBuilder.RenameColumn(
            name: "BoardId",
            table: "Boards",
            newName: "ForumId");

        // Revert Boards table to Fora
        migrationBuilder.RenameTable(
            name: "Boards",
            newName: "Fora");
    }
}
