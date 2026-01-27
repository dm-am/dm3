using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Game new fields: rename SettingName to NarrativeSetting, add recruitment fields
/// </summary>
public partial class GameNewFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Rename SettingName to NarrativeSetting
        migrationBuilder.RenameColumn(
            name: "SettingName",
            table: "Games",
            newName: "NarrativeSetting");

        // Add RecruitmentPlayerLimit
        migrationBuilder.AddColumn<int>(
            name: "RecruitmentPlayerLimit",
            table: "Games",
            type: "integer",
            nullable: true);

        // Add RecruitmentStartedUtc
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RecruitmentStartedUtc",
            table: "Games",
            type: "timestamp with time zone",
            nullable: true);

        // Set RecruitmentStartedUtc for games with open recruitment
        // Using ReleaseDate as approximation
        migrationBuilder.Sql(@"
            UPDATE ""Games""
            SET ""RecruitmentStartedUtc"" = ""ReleaseDate""
            WHERE ""IsRecruitmentOpen"" = true AND ""ReleaseDate"" IS NOT NULL;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove recruitment columns
        migrationBuilder.DropColumn(
            name: "RecruitmentPlayerLimit",
            table: "Games");

        migrationBuilder.DropColumn(
            name: "RecruitmentStartedUtc",
            table: "Games");

        // Rename NarrativeSetting back to SettingName
        migrationBuilder.RenameColumn(
            name: "NarrativeSetting",
            table: "Games",
            newName: "SettingName");
    }
}
