using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <inheritdoc />
public partial class RenameNannyToMentor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "NannyId",
            table: "Games",
            newName: "MentorId");

        migrationBuilder.RenameIndex(
            name: "IX_Games_NannyId",
            table: "Games",
            newName: "IX_Games_MentorId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "MentorId",
            table: "Games",
            newName: "NannyId");

        migrationBuilder.RenameIndex(
            name: "IX_Games_MentorId",
            table: "Games",
            newName: "IX_Games_NannyId");
    }
}
