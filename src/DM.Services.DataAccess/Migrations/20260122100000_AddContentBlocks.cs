using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Add ContentBlocks table for editable static content (rules, about, etc.)
/// Migrates data from InfoPages if it exists.
/// </summary>
public partial class AddContentBlocks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create ContentBlocks table
        migrationBuilder.CreateTable(
            name: "ContentBlocks",
            columns: table => new
            {
                ContentBlockId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Content = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentBlocks", x => x.ContentBlockId);
            });

        // Create unique index on Key
        migrationBuilder.CreateIndex(
            name: "IX_ContentBlocks_Key",
            table: "ContentBlocks",
            column: "Key",
            unique: true);

        // Migrate data from InfoPages if it exists, otherwise seed default content
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                -- Check if InfoPages table exists and has data
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'InfoPages') THEN
                    -- Migrate data from InfoPages to ContentBlocks
                    INSERT INTO ""ContentBlocks"" (""ContentBlockId"", ""Key"", ""Content"")
                    SELECT ""InfoPageId"", ""Key"", ""Content""
                    FROM ""InfoPages"";

                    -- Drop the old table
                    DROP TABLE ""InfoPages"";
                ELSE
                    -- Seed default rules content
                    INSERT INTO ""ContentBlocks"" (""ContentBlockId"", ""Key"", ""Content"")
                    VALUES (
                        gen_random_uuid(),
                        'rules',
                        'Для большинства участников проекта это не просто сайт в сети – это родной дом, уютное и любимое место. Чтобы не портить никому пребывание в этом месте, разработана система правил. Регистрируясь на сайте вы вроде как обязуетесь их соблюдать.

На самом деле, правила очень простые. Если просто мирно играть в игры, общаться в чате и на форуме, не пытаться ни с кем поругаться или на кого-то обидеться, вам никогда не придется сталкиваться с последствиями их нарушения!

Мы тут любим всякий ролевой сленг, поэтому модераторы у нас называются ""гоблинами"", а администраторы ""троллями"". Привыкайте, потому что дальше будет про этих злобных гоблиноидов.

Публичные разделы сайта – темы форума, общий чат и профили пользователей – не место для выяснения отношений, оскорблений, мата и прочих прелестей интернета. За эти штуки гоблины выписывают баллы по особому прейскуранту. Нахватавшись сих волшебных фруктов, пользователь отправляется в краткое изгнание из этих публичных разделов сайта – но все еще может участвовать в играх.

Если пользователь усердствует в нарушении правил приличия, то он может отправиться уже в полное изгнание. Сроки изгнаний, как полного, так и, что у нас называется ""демократичного"" с каждым разом растут – вплоть до бессрочных изгнаний.'
                    );
                END IF;
            END $$;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ContentBlocks");
    }
}
