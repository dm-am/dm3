using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to Rooms table
            migrationBuilder.AddColumn<bool>(
                name: "ViewPrivateText",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ViewDiceResults",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiceEnabled",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Copy settings from Games to Rooms
            // ViewPrivateText = Game.ShowPrivateMessages
            // ViewDiceResults = NOT Game.HideDiceResult
            migrationBuilder.Sql(@"
                UPDATE ""Rooms"" r
                SET ""ViewPrivateText"" = g.""ShowPrivateMessages"",
                    ""ViewDiceResults"" = NOT g.""HideDiceResult"",
                    ""DiceEnabled"" = true
                FROM ""Games"" g
                WHERE r.""GameId"" = g.""GameId""
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ViewPrivateText",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ViewDiceResults",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "DiceEnabled",
                table: "Rooms");
        }
    }
}
