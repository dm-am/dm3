using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.Services.DataAccess.Migrations
{
    internal partial class RenameRecruitmentForum : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Fora\" SET \"Title\" = 'Поиск мастера и игроков' WHERE \"ForumId\" = '00000000-0000-0000-0000-000000000002'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Fora\" SET \"Title\" = 'Набор игроков и поиск мастера' WHERE \"ForumId\" = '00000000-0000-0000-0000-000000000002'");
        }
    }
}
