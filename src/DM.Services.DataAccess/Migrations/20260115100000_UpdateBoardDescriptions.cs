using Microsoft.EntityFrameworkCore.Migrations;

namespace DM.Services.DataAccess.Migrations
{
    internal partial class UpdateBoardDescriptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Обновляем описания разделов на короткие и понятные
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Вопросы от новых пользователей сайта' WHERE \"Title\" = 'Для новичков'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Обсуждение всего, что связано с ролевыми играми' WHERE \"Title\" = 'Общий'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Обсуждение D&D, GURPS, Savage Worlds и других систем' WHERE \"Title\" = 'Игровые системы'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Поиск игроков в свою игру или мастера для участия' WHERE \"Title\" = 'Поиск мастера и игроков'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Идеи для игр, персонажей и сюжетов' WHERE \"Title\" = 'Котёл идей'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Литературные и творческие конкурсы' WHERE \"Title\" = 'Конкурсы'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Оффтопик и разговоры не по теме' WHERE \"Title\" = 'Под столом'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Предложения по развитию проекта' WHERE \"Title\" = 'Улучшение сайта'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Сообщения о багах и технических проблемах' WHERE \"Title\" = 'Ошибки'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Обновления и объявления от администрации' WHERE \"Title\" = 'Новости проекта'");
            migrationBuilder.Sql("UPDATE \"Fora\" SET \"Description\" = 'Настольные игры, видеоигры и прочее' WHERE \"Title\" = 'Неролевые игры'");

            // Удаляем скрытые разделы (не используются на новом сайте)
            // Питомник
            migrationBuilder.Sql(
                "DELETE FROM \"Fora\" WHERE \"ForumId\" = '00000000-0000-0000-0000-000000000009'");
            // Администрация
            migrationBuilder.Sql(
                "DELETE FROM \"Fora\" WHERE \"ForumId\" = '00000000-0000-0000-0000-00000000000a'");
            // Сердце подземелья
            migrationBuilder.Sql(
                "DELETE FROM \"Fora\" WHERE \"ForumId\" = '00000000-0000-0000-0000-00000000000b'");
            // Пещера троллей
            migrationBuilder.Sql(
                "DELETE FROM \"Fora\" WHERE \"ForumId\" = '00000000-0000-0000-0000-00000000000c'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Fora\" SET \"Description\" = NULL WHERE \"ForumId\" = '00000000-0000-0000-0000-000000000008'");

            // Восстанавливаем скрытые разделы
            migrationBuilder.Sql(@"
                INSERT INTO ""Fora"" (""ForumId"", ""Title"", ""Order"", ""Description"", ""ViewPolicy"", ""CreateTopicPolicy"", ""TopicsCount"", ""CommentsCount"", ""LastCommentId"", ""LastCommentTopicId"", ""LastCommentAuthorId"", ""LastCommentDate"")
                VALUES
                    ('00000000-0000-0000-0000-000000000009', 'Питомник', 10, NULL, 11, 11, 0, 0, NULL, NULL, NULL, NULL),
                    ('00000000-0000-0000-0000-00000000000a', 'Администрация', 11, NULL, 15, 15, 0, 0, NULL, NULL, NULL, NULL),
                    ('00000000-0000-0000-0000-00000000000b', 'Сердце подземелья', 12, NULL, 3, 3, 0, 0, NULL, NULL, NULL, NULL),
                    ('00000000-0000-0000-0000-00000000000c', 'Пещера троллей', 13, NULL, 1, 1, 0, 0, NULL, NULL, NULL, NULL)");
        }
    }
}
