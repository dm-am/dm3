using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Simplify GameStatus from 8 to 3 values + flags
/// Old: Closed=0, Finished=1, Frozen=2, Requirement=3, Draft=4, Active=5, RequiresModeration=6, Moderation=7
/// New: Draft=0, Active=1, Closed=2 + IsFinished, IsFrozen, IsRecruitmentOpen, PremoderationStatus
/// </summary>
public partial class SimplifyGameStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Add new columns
        migrationBuilder.AddColumn<int>(
            name: "PremoderationStatus",
            table: "Games",
            type: "integer",
            nullable: false,
            defaultValue: 0); // Approved

        migrationBuilder.AddColumn<bool>(
            name: "IsFinished",
            table: "Games",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsFrozen",
            table: "Games",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsRecruitmentOpen",
            table: "Games",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ClosedUtc",
            table: "Games",
            type: "timestamp with time zone",
            nullable: true);

        // Step 2: Migrate data - set flags based on old status before changing status values
        // Old Finished (1) -> set IsFinished = true
        migrationBuilder.Sql(@"UPDATE ""Games"" SET ""IsFinished"" = true WHERE ""Status"" = 1;");

        // Old Frozen (2) -> set IsFrozen = true
        migrationBuilder.Sql(@"UPDATE ""Games"" SET ""IsFrozen"" = true WHERE ""Status"" = 2;");

        // Old Requirement (3) or Active (5) -> set IsRecruitmentOpen = true for Requirement only
        migrationBuilder.Sql(@"UPDATE ""Games"" SET ""IsRecruitmentOpen"" = true WHERE ""Status"" = 3;");

        // Old RequiresModeration (6) -> set PremoderationStatus = 1 (AwaitingApproval)
        migrationBuilder.Sql(@"UPDATE ""Games"" SET ""PremoderationStatus"" = 1 WHERE ""Status"" = 6;");

        // Old Moderation (7) -> set PremoderationStatus = 2 (AwaitingEdits) - mentor is reviewing
        migrationBuilder.Sql(@"UPDATE ""Games"" SET ""PremoderationStatus"" = 2 WHERE ""Status"" = 7;");

        // Step 3: Update Status values to new enum
        // Mapping:
        // Draft (4) -> Draft (0)
        // RequiresModeration (6) -> Active (1) with PremoderationStatus
        // Moderation (7) -> Active (1) with PremoderationStatus
        // Requirement (3) -> Active (1)
        // Active (5) -> Active (1)
        // Frozen (2) -> Closed (2)
        // Closed (0) -> Closed (2)
        // Finished (1) -> Closed (2)

        // Create temporary column to avoid conflicts
        migrationBuilder.AddColumn<int>(
            name: "NewStatus",
            table: "Games",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Map old values to new
        migrationBuilder.Sql(@"
            UPDATE ""Games"" SET ""NewStatus"" = CASE ""Status""
                WHEN 4 THEN 0  -- Draft -> Draft
                WHEN 6 THEN 1  -- RequiresModeration -> Active
                WHEN 7 THEN 1  -- Moderation -> Active
                WHEN 3 THEN 1  -- Requirement -> Active
                WHEN 5 THEN 1  -- Active -> Active
                WHEN 2 THEN 2  -- Frozen -> Closed
                WHEN 0 THEN 2  -- Closed -> Closed
                WHEN 1 THEN 2  -- Finished -> Closed
                ELSE 0         -- Default to Draft
            END;
        ");

        // Copy new values back
        migrationBuilder.Sql(@"UPDATE ""Games"" SET ""Status"" = ""NewStatus"";");

        // Drop temporary column
        migrationBuilder.DropColumn(name: "NewStatus", table: "Games");

        // Step 4: Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_Games_PremoderationStatus",
            table: "Games",
            column: "PremoderationStatus");

        migrationBuilder.CreateIndex(
            name: "IX_Games_IsRecruitmentOpen",
            table: "Games",
            column: "IsRecruitmentOpen");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This migration is complex to reverse - would need to map back to old statuses
        // For simplicity, just drop the new columns and restore approximate old values

        migrationBuilder.DropIndex(name: "IX_Games_PremoderationStatus", table: "Games");
        migrationBuilder.DropIndex(name: "IX_Games_IsRecruitmentOpen", table: "Games");

        // Create temporary column
        migrationBuilder.AddColumn<int>(
            name: "OldStatus",
            table: "Games",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Map back (approximate - some data loss possible)
        migrationBuilder.Sql(@"
            UPDATE ""Games"" SET ""OldStatus"" = CASE
                WHEN ""Status"" = 0 AND ""PremoderationStatus"" = 1 THEN 6  -- Draft + AwaitingApproval -> RequiresModeration
                WHEN ""Status"" = 0 AND ""PremoderationStatus"" = 2 THEN 7  -- Draft + AwaitingEdits -> Moderation
                WHEN ""Status"" = 0 THEN 4  -- Draft -> Draft
                WHEN ""Status"" = 1 AND ""PremoderationStatus"" = 1 THEN 6  -- Active + AwaitingApproval -> RequiresModeration
                WHEN ""Status"" = 1 AND ""PremoderationStatus"" = 2 THEN 7  -- Active + AwaitingEdits -> Moderation
                WHEN ""Status"" = 1 AND ""IsRecruitmentOpen"" = true THEN 3  -- Active + Recruiting -> Requirement
                WHEN ""Status"" = 1 THEN 5  -- Active -> Active
                WHEN ""Status"" = 2 AND ""IsFinished"" = true THEN 1  -- Closed + IsFinished -> Finished
                WHEN ""Status"" = 2 AND ""IsFrozen"" = true THEN 2  -- Closed + IsFrozen -> Frozen
                WHEN ""Status"" = 2 THEN 0  -- Closed -> Closed
                ELSE 4  -- Default to Draft
            END;
        ");

        // Copy back
        migrationBuilder.Sql(@"UPDATE ""Games"" SET ""Status"" = ""OldStatus"";");

        // Drop temporary column
        migrationBuilder.DropColumn(name: "OldStatus", table: "Games");

        // Drop new columns
        migrationBuilder.DropColumn(name: "PremoderationStatus", table: "Games");
        migrationBuilder.DropColumn(name: "IsFinished", table: "Games");
        migrationBuilder.DropColumn(name: "IsFrozen", table: "Games");
        migrationBuilder.DropColumn(name: "IsRecruitmentOpen", table: "Games");
        migrationBuilder.DropColumn(name: "ClosedUtc", table: "Games");
    }
}
