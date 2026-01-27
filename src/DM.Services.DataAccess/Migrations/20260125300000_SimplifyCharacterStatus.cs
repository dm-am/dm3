using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Simplify CharacterStatus from 5 to 4 values + flags
/// Old: Registration=0, Declined=1, Active=2, Dead=3, Left=4
/// New: UnderReview=0, Declined=1, Active=2, Retired=3 + IsDead, IsPlayerLeft, IsPlayerExiled
/// </summary>
public partial class SimplifyCharacterStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Add new columns
        migrationBuilder.AddColumn<bool>(
            name: "IsDead",
            table: "Characters",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPlayerLeft",
            table: "Characters",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPlayerExiled",
            table: "Characters",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Step 2: Set flags based on old status before changing status values
        // Old Dead (3) -> set IsDead = true
        migrationBuilder.Sql(@"UPDATE ""Characters"" SET ""IsDead"" = true WHERE ""Status"" = 3;");

        // Old Left (4) -> set IsPlayerLeft = true
        migrationBuilder.Sql(@"UPDATE ""Characters"" SET ""IsPlayerLeft"" = true WHERE ""Status"" = 4;");

        // Step 3: Update Status values to new enum
        // Mapping:
        // Registration (0) -> UnderReview (0) - no change needed
        // Declined (1) -> Declined (1) - no change needed
        // Active (2) -> Active (2) - no change needed
        // Dead (3) -> Retired (3)
        // Left (4) -> Retired (3)

        // Update Left to Retired
        migrationBuilder.Sql(@"UPDATE ""Characters"" SET ""Status"" = 3 WHERE ""Status"" = 4;");
        // Dead is already 3 (Retired) so no change needed
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Map back (approximate - some data loss possible for IsPlayerExiled)
        // Retired with IsDead -> Dead (3)
        migrationBuilder.Sql(@"UPDATE ""Characters"" SET ""Status"" = 3 WHERE ""Status"" = 3 AND ""IsDead"" = true;");

        // Retired with IsPlayerLeft -> Left (4)
        migrationBuilder.Sql(@"UPDATE ""Characters"" SET ""Status"" = 4 WHERE ""Status"" = 3 AND ""IsPlayerLeft"" = true;");

        // Retired without flags (could be exiled) -> Left (4) as default
        migrationBuilder.Sql(@"UPDATE ""Characters"" SET ""Status"" = 4 WHERE ""Status"" = 3 AND ""IsDead"" = false AND ""IsPlayerLeft"" = false;");

        // Drop new columns
        migrationBuilder.DropColumn(name: "IsDead", table: "Characters");
        migrationBuilder.DropColumn(name: "IsPlayerLeft", table: "Characters");
        migrationBuilder.DropColumn(name: "IsPlayerExiled", table: "Characters");
    }
}
