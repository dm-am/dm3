using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Add composite indexes for performance optimization on frequently filtered columns.
/// These indexes support the IsRemoved soft-delete pattern used throughout the codebase.
/// </summary>
public partial class AddPerformanceIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Users: Search by login (login page, invitations, @mentions)
        // Covers: WHERE IsRemoved = false AND Activated = true AND Login = 'x'
        migrationBuilder.CreateIndex(
            name: "IX_Users_IsRemoved_Activated_Login",
            table: "Users",
            columns: new[] { "IsRemoved", "Activated", "Login" });

        // Games: Filter by status (game lists, dashboard)
        // Covers: WHERE IsRemoved = false AND Status = x
        migrationBuilder.CreateIndex(
            name: "IX_Games_IsRemoved_Status",
            table: "Games",
            columns: new[] { "IsRemoved", "Status" });

        // ForumTopics: Topics list in a board (forum page)
        // Covers: WHERE BoardId = x AND IsRemoved = false ORDER BY IsAttached DESC
        migrationBuilder.CreateIndex(
            name: "IX_ForumTopics_BoardId_IsRemoved_IsAttached",
            table: "ForumTopics",
            columns: new[] { "BoardId", "IsRemoved", "IsAttached" });

        // Posts: Posts in a room (game room view)
        // Covers: WHERE RoomId = x AND IsRemoved = false
        migrationBuilder.CreateIndex(
            name: "IX_Posts_RoomId_IsRemoved",
            table: "Posts",
            columns: new[] { "RoomId", "IsRemoved" });

        // Characters: Characters in a game (game characters page)
        // Covers: WHERE GameId = x AND IsRemoved = false AND Status = y
        migrationBuilder.CreateIndex(
            name: "IX_Characters_GameId_IsRemoved_Status",
            table: "Characters",
            columns: new[] { "GameId", "IsRemoved", "Status" });

        // Comments: Comments on an entity (topic, game, etc.)
        // Covers: WHERE EntityId = x AND IsRemoved = false
        migrationBuilder.CreateIndex(
            name: "IX_Comments_EntityId_IsRemoved",
            table: "Comments",
            columns: new[] { "EntityId", "IsRemoved" });

        // Rooms: Rooms in a game (game room list)
        // Covers: WHERE GameId = x AND IsRemoved = false
        migrationBuilder.CreateIndex(
            name: "IX_Rooms_GameId_IsRemoved",
            table: "Rooms",
            columns: new[] { "GameId", "IsRemoved" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_IsRemoved_Activated_Login", table: "Users");
        migrationBuilder.DropIndex(name: "IX_Games_IsRemoved_Status", table: "Games");
        migrationBuilder.DropIndex(name: "IX_ForumTopics_BoardId_IsRemoved_IsAttached", table: "ForumTopics");
        migrationBuilder.DropIndex(name: "IX_Posts_RoomId_IsRemoved", table: "Posts");
        migrationBuilder.DropIndex(name: "IX_Characters_GameId_IsRemoved_Status", table: "Characters");
        migrationBuilder.DropIndex(name: "IX_Comments_EntityId_IsRemoved", table: "Comments");
        migrationBuilder.DropIndex(name: "IX_Rooms_GameId_IsRemoved", table: "Rooms");
    }
}
