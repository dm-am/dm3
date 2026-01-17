using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Migration for adding OutboxEvent table
/// </summary>
public partial class AddOutboxEvent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxEvents",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AggregateId = table.Column<Guid>(nullable: false),
                EventType = table.Column<int>(nullable: false),
                Payload = table.Column<string>(nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(nullable: true),
                IsProcessed = table.Column<bool>(nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxEvents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxEvents_IsProcessed",
            table: "OutboxEvents",
            column: "IsProcessed",
            filter: "\"IsProcessed\" = false");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxEvents");
    }
}
