using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Add LastUpdateDate column to Messages table
/// </summary>
public partial class AddLastUpdateDateToMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastUpdateDate",
            table: "Messages",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastUpdateDate",
            table: "Messages");
    }
}
