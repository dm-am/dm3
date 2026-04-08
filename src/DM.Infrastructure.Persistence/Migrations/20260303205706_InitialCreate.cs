using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pg_trgm extension for fuzzy text search
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "OutboxEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingRegistrations",
                columns: table => new
                {
                    PendingRegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Salt = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHashVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TokenCreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedRules = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRegistrations", x => x.PendingRegistrationId);
                });

            migrationBuilder.CreateTable(
                name: "TagGroups",
                columns: table => new
                {
                    TagGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagGroups", x => x.TagGroupId);
                });

            migrationBuilder.InsertData(
                table: "TagGroups",
                columns: new[] { "TagGroupId", "Title", "Description", "SortOrder" },
                values: new object[,]
                {
                    { Guid.Parse("00000000-0000-0000-0000-000000000000"), "Система", "Ролевая система или набор правил, по которым ведется игра", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000001"), "Жанр", "Жанр и сеттинг игрового мира", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000002"), "Формат игры", "Тип игрового процесса и взаимодействия между участниками", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000003"), "Формат постов", "Стиль и объем игровых постов", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000004"), "Темп", "Ожидаемая скорость игры и частота постов", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000005"), "Ограничения", "Особые требования и ограничения для участников", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000006"), "Новички", "Игры от новичков и для новичков", 6 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000007"), "Деликатный контент", "Контент, требующий осознанного согласия участников", 7 }
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortId = table.Column<int>(type: "integer", nullable: false),
                    TagGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.TagId);
                    table.ForeignKey(
                        name: "FK_Tags_TagGroups_TagGroupId",
                        column: x => x.TagGroupId,
                        principalTable: "TagGroups",
                        principalColumn: "TagGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Tag group GUIDs:
            // 00000000-0000-0000-0000-000000000000 = Система
            // 00000000-0000-0000-0000-000000000001 = Жанр
            // 00000000-0000-0000-0000-000000000002 = Формат игры
            // 00000000-0000-0000-0000-000000000003 = Формат постов
            // 00000000-0000-0000-0000-000000000004 = Темп
            // 00000000-0000-0000-0000-000000000005 = Ограничения
            // 00000000-0000-0000-0000-000000000006 = Новички
            // 00000000-0000-0000-0000-000000000007 = Деликатный контент
            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "TagId", "ShortId", "TagGroupId", "Title", "Description", "SortOrder" },
                values: new object[,]
                {
                    // === Система ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000001"), 1, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Black Bird Pie", "Простая система с кубиком d6", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000002"), 2, Guid.Parse("00000000-0000-0000-0000-000000000000"), "D&D", "Dungeons & Dragons — все редакции классической ролевой системы", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000003"), 3, Guid.Parse("00000000-0000-0000-0000-000000000000"), "D&D 5e", "Dungeons & Dragons 5th Edition", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000004"), 4, Guid.Parse("00000000-0000-0000-0000-000000000000"), "D100", "Системы на основе процентного броска", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000005"), 5, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Dawn of Worlds", "Система для совместного создания мира", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000006"), 6, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Fallout", "Адаптация сеттинга Fallout", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000007"), 7, Guid.Parse("00000000-0000-0000-0000-000000000000"), "FATAL", "Без комментариев", 6 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000008"), 8, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Fate", "Нарративная система с аспектами и фейт-пойнтами", 7 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000009"), 9, Guid.Parse("00000000-0000-0000-0000-000000000000"), "FUDGE", "Универсальный движок для реализации практически любого концепта", 8 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000a"), 10, Guid.Parse("00000000-0000-0000-0000-000000000000"), "GURPS", "Универсальная система на базе броска 3d6 vs Сложность", 9 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000b"), 11, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Interlock", "Система от R. Talsorian Games (Cyberpunk 2020 и другие)", 10 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000c"), 12, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Microscope", "Система для создания эпических историй", 11 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000d"), 13, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Pathfinder 1e", "Pathfinder первой редакции", 12 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000e"), 14, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Pathfinder 2e", "Pathfinder второй редакции", 13 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000f"), 15, Guid.Parse("00000000-0000-0000-0000-000000000000"), "PbtA", "Нарративные системы на базе 2d6 vs Сложность", 14 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000010"), 16, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Risus", "Минималистичная комедийная система", 15 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000011"), 17, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Savage Worlds", "Легковесная универсальная система — Fast! Furious! Fun!", 16 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000012"), 18, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Starfinder 1e", "Sci-fi спин-офф Pathfinder", 17 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000013"), 19, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Starfinder 2e", "Starfinder второй редакции", 18 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000014"), 20, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Warhammer", "Системы по вселенной Warhammer", 19 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000015"), 21, Guid.Parse("00000000-0000-0000-0000-000000000000"), "World of Darkness", "Мир Тьмы — вампиры, оборотни, маги", 20 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000016"), 22, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Авторская", "Оригинальная система от мастера игры", 21 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000017"), 23, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Мафия", "Психологическая детективная командная игра", 22 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000018"), 24, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Словеска", "Игра без формальной системы правил", 23 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000019"), 25, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Эра Водолея", "Отечественная система ролевых игр", 24 },

                    // === Жанр ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000001a"), 26, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Альтернативная история", "Переосмысление исторических событий", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001b"), 27, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Боевик", "Акцент на экшн и сражениях", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001c"), 28, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Детектив", "Расследования и разгадывание тайн", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001d"), 29, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Зомби", "Зомби-апокалипсис и выживание", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001e"), 30, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Историческое", "Действие в реальную историческую эпоху", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001f"), 31, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Киберпанк", "Высокие технологии, низкий уровень жизни", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000020"), 32, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Комедия", "Юмор и абсурдные ситуации", 6 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000021"), 33, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Космоопера", "Эпические приключения в космосе", 7 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000022"), 34, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Мистика", "Сверхъестественные элементы и тайны", 8 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000023"), 35, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Наши дни", "Современный реалистичный сеттинг", 9 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000024"), 36, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Постапокалипсис", "Мир после катастрофы", 10 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000025"), 37, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Психоделика", "Сюрреалистичные и необычные миры", 11 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000026"), 38, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Стимпанк", "Паровые технологии и викторианская эстетика", 12 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000027"), 39, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Триллер", "Напряжение и саспенс", 13 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000028"), 40, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Трэш", "Нарочито нелепый и провокационный контент", 14 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000029"), 41, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Ужасы", "Хоррор и атмосфера страха", 15 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002a"), 42, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Фантастика", "Научная фантастика и будущее", 16 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002b"), 43, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Фэнтези", "Магия, мечи и волшебные миры", 17 },

                    // === Формат игры ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000002c"), 44, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Dungeon Crawl", "Исследование подземелий и сражения с монстрами", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002d"), 45, Guid.Parse("00000000-0000-0000-0000-000000000002"), "PvP", "Противостояние между игроками", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002e"), 46, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Выживание", "Борьба за выживание в суровых условиях", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002f"), 47, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Песочница", "Открытый мир без сюжетных ограничений", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000030"), 48, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Стратегия", "Управление ресурсами и принятие глобальных решений", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000031"), 49, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Сюжетная", "Фокус на развитии истории", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000032"), 50, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Тактика", "Тактические бои и позиционирование", 6 },

                    // === Формат постов ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000033"), 51, Guid.Parse("00000000-0000-0000-0000-000000000003"), "Короткопост", "Короткие посты в 1-3 абзаца", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000034"), 52, Guid.Parse("00000000-0000-0000-0000-000000000003"), "Литературная", "Развернутые литературные посты", 1 },

                    // === Темп ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000035"), 53, Guid.Parse("00000000-0000-0000-0000-000000000004"), "Неторопливый", "Посты раз в несколько дней", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000036"), 54, Guid.Parse("00000000-0000-0000-0000-000000000004"), "Скоростной", "Несколько постов в день", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003e"), 62, Guid.Parse("00000000-0000-0000-0000-000000000004"), "Сухие сезоны", "Возможны продолжительные периоды без постов", 2 },

                    // === Ограничения ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000037"), 55, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Без мата", "Нецензурная лексика запрещена", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000038"), 56, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Без насилия", "Минимум жестокости и крови", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000039"), 57, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Grammar Nazi", "Повышенные требования к грамотности", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003a"), 58, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Для своих", "Игра для знакомой компании", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003b"), 59, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Обязателен мессенджер", "Обсуждение игровых вопросов во внешнем мессенджере", 4 },

                    // === Новички ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000003c"), 60, Guid.Parse("00000000-0000-0000-0000-000000000006"), "Для новичков", "Игра подходит для начинающих", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003d"), 61, Guid.Parse("00000000-0000-0000-0000-000000000006"), "Мастер-новичок", "Мастер игры — начинающий", 1 },

                    // === Деликатный контент ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000003f"), 63, Guid.Parse("00000000-0000-0000-0000-000000000007"), "ERP", "Erotic Role-Play: [tipimg:/images/erp-tooltip.gif]эротические сцены[/tipimg] как основа игрового процесса", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000040"), 64, Guid.Parse("00000000-0000-0000-0000-000000000007"), "Шок-контент", "Чернуха, максимально шокирующий и отталкивающий контент без ограничений", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000041"), 65, Guid.Parse("00000000-0000-0000-0000-000000000007"), "Острые темы", "Игра затрагивает спорные или чувствительные социальные темы", 2 }
                });

            migrationBuilder.CreateTable(
                name: "Bans",
                columns: table => new
                {
                    BanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    AccessRestrictionPolicy = table.Column<int>(type: "integer", nullable: false),
                    IsVoluntary = table.Column<bool>(type: "boolean", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bans", x => x.BanId);
                });

            migrationBuilder.CreateTable(
                name: "BlogAssistants",
                columns: table => new
                {
                    BlogAssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogAssistants", x => x.BlogAssistantId);
                });

            migrationBuilder.CreateTable(
                name: "BlogBlacklists",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogBlacklists", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "Blogs",
                columns: table => new
                {
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PremoderationStatus = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftVisibility = table.Column<int>(type: "integer", nullable: false),
                    CommentsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PublicationCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    PopularityScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PopularityScoreUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blogs", x => x.BlogId);
                });

            migrationBuilder.CreateTable(
                name: "BoardModerators",
                columns: table => new
                {
                    BoardModeratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardModerators", x => x.BoardModeratorId);
                });

            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ViewPolicy = table.Column<int>(type: "integer", nullable: false),
                    CreateTopicPolicy = table.Column<int>(type: "integer", nullable: false),
                    TopicsCount = table.Column<int>(type: "integer", nullable: false),
                    CommentsCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentTopicTitle = table.Column<string>(type: "text", nullable: true),
                    LastCommentTopicNumber = table.Column<int>(type: "integer", nullable: true),
                    LastCommentAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastTopicNumber = table.Column<int>(type: "integer", nullable: true),
                    LastTopicTitle = table.Column<string>(type: "text", nullable: true),
                    LastTopicAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastTopicCreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.BoardId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Boards_Alias",
                table: "Boards",
                column: "Alias",
                unique: true);

            // Seed forum boards
            // ViewPolicy and CreateTopicPolicy use BoardAccessPolicy flags:
            // RegularUser = 32, Mentor = 4, Moderator = 7, SeniorModerator = 11, Admin = 15, Everyone = 64, Nobody = 1
            // Alias: ASCII lowercase with hyphens, no Cyrillic
            migrationBuilder.InsertData(
                table: "Boards",
                columns: new[] { "BoardId", "Title", "Alias", "Description", "Order", "ViewPolicy", "CreateTopicPolicy", "TopicsCount", "CommentsCount" },
                values: new object[,]
                {
                    { Guid.Parse("00000000-0000-0000-0000-000000000001"), "Общий", "general", "Жизнь сообщества и решения администрации", 1, 64, 32, 1, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000002"), "Игровые системы", "game-systems", "Обсуждение правил и помощь в выборе системы", 2, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000003"), "Поиск мастера и игроков", "looking-for-group", "Набор игроков в игру или поиск мастера", 3, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000004"), "Котел идей", "ideas", "Обкатка задумок и поиск единомышленников", 4, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000005"), "Конкурсы", "contests", "Литературные и творческие состязания", 5, 64, 4, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000006"), "Под столом", "off-topic", "Музыка, книги, кино, мемы и все остальное", 6, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000007"), "Неролевые игры", "forum-games", "Словесные игры, ассоциации и прочие развлечения", 7, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000008"), "Улучшение сайта", "improvements", "Идеи и предложения по развитию сайта", 8, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000009"), "Ошибки", "bugs", "Сообщения об ошибках на сайте", 9, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000a"), "Для новичков", "newbies", "Руководства, ответы на вопросы и помощь новичкам", 10, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000b"), "Новости проекта", "news", "Официальные новости, обновления и статистика", 11, 64, 4, 0, 0 }
                });

            migrationBuilder.CreateTable(
                name: "CharacterAttributes",
                columns: table => new
                {
                    CharacterAttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAttributes", x => x.CharacterAttributeId);
                });

            migrationBuilder.CreateTable(
                name: "CharacterEdits",
                columns: table => new
                {
                    CharacterEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterEdits", x => x.CharacterEditId);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDead = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayerLeft = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayerExiled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Race = table.Column<string>(type: "text", nullable: true),
                    Class = table.Column<string>(type: "text", nullable: true),
                    Alignment = table.Column<int>(type: "integer", nullable: true),
                    Appearance = table.Column<string>(type: "text", nullable: true),
                    Temper = table.Column<string>(type: "text", nullable: true),
                    Story = table.Column<string>(type: "text", nullable: true),
                    Skills = table.Column<string>(type: "text", nullable: true),
                    Inventory = table.Column<string>(type: "text", nullable: true),
                    IsNpc = table.Column<bool>(type: "boolean", nullable: false),
                    AccessPolicy = table.Column<int>(type: "integer", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.CharacterId);
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMessageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "CommentEdits",
                columns: table => new
                {
                    CommentEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentEdits", x => x.CommentEditId);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.CommentId);
                });

            migrationBuilder.CreateTable(
                name: "GameAssistants",
                columns: table => new
                {
                    GameAssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameAssistants", x => x.GameAssistantId);
                });

            migrationBuilder.CreateTable(
                name: "GameBlacklists",
                columns: table => new
                {
                    GameBlacklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameBlacklists", x => x.GameBlacklistId);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PremoderationStatus = table.Column<int>(type: "integer", nullable: false),
                    ClosedReason = table.Column<int>(type: "integer", nullable: false),
                    DraftVisibility = table.Column<int>(type: "integer", nullable: false),
                    IsRecruitmentOpen = table.Column<bool>(type: "boolean", nullable: false),
                    RecruitmentPcLimit = table.Column<int>(type: "integer", nullable: true),
                    RecruitmentStartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecruitmentCount = table.Column<int>(type: "integer", nullable: false),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastPostCreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InactivityWarningUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosureWarningUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PopularityScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PopularityScoreUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MasterId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeSchemaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SystemName = table.Column<string>(type: "text", nullable: true),
                    NarrativeSetting = table.Column<string>(type: "text", nullable: true),
                    Info = table.Column<string>(type: "text", nullable: true),
                    HideTemper = table.Column<bool>(type: "boolean", nullable: false),
                    HideSkills = table.Column<bool>(type: "boolean", nullable: false),
                    HideInventory = table.Column<bool>(type: "boolean", nullable: false),
                    HideStory = table.Column<bool>(type: "boolean", nullable: false),
                    DisableAlignment = table.Column<bool>(type: "boolean", nullable: false),
                    HideDiceResult = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPrivateMessages = table.Column<bool>(type: "boolean", nullable: false),
                    HidePostStats = table.Column<bool>(type: "boolean", nullable: false),
                    CommentsAccessMode = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.GameId);
                });

            migrationBuilder.CreateTable(
                name: "GameTags",
                columns: table => new
                {
                    GameTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTags", x => x.GameTagId);
                    table.ForeignKey(
                        name: "FK_GameTags_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AccessType = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OrderNumber = table.Column<double>(type: "double precision", nullable: false),
                    ViewPrivateText = table.Column<bool>(type: "boolean", nullable: false),
                    ViewDiceResults = table.Column<bool>(type: "boolean", nullable: false),
                    DiceEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_Rooms_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rooms_Rooms_NextRoomId",
                        column: x => x.NextRoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId");
                    table.ForeignKey(
                        name: "FK_Rooms_Rooms_PreviousRoomId",
                        column: x => x.PreviousRoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId");
                });

            migrationBuilder.CreateTable(
                name: "GlobalChatEventParticipants",
                columns: table => new
                {
                    GlobalChatEventParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOrganizer = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalChatEventParticipants", x => x.GlobalChatEventParticipantId);
                });

            migrationBuilder.CreateTable(
                name: "GlobalChatEvents",
                columns: table => new
                {
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    StartsUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalChatEvents", x => x.GlobalChatEventId);
                });

            migrationBuilder.CreateTable(
                name: "Likes",
                columns: table => new
                {
                    LikeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Likes", x => x.LikeId);
                });

            migrationBuilder.CreateTable(
                name: "MessageEdits",
                columns: table => new
                {
                    MessageEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEdits", x => x.MessageEditId);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Messages_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_GlobalChatEvents_GlobalChatEventId",
                        column: x => x.GlobalChatEventId,
                        principalTable: "GlobalChatEvents",
                        principalColumn: "GlobalChatEventId");
                });

            migrationBuilder.CreateTable(
                name: "ModeratedProfileNotes",
                columns: table => new
                {
                    ModeratedProfileNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeratedProfileNotes", x => x.ModeratedProfileNoteId);
                });

            migrationBuilder.CreateTable(
                name: "NotepadCategories",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotepadType = table.Column<int>(type: "integer", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotepadCategories", x => x.CategoryId);
                    // FK_NotepadCategories_Users_DeletedByUserId added after Users table is created
                });

            migrationBuilder.CreateTable(
                name: "NotepadEntries",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotepadType = table.Column<int>(type: "integer", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotepadEntries", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_NotepadEntries_NotepadCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "NotepadCategories",
                        principalColumn: "CategoryId");
                    // FK_NotepadEntries_Users_DeletedByUserId added after Users table is created
                });

            migrationBuilder.CreateTable(
                name: "PostEdits",
                columns: table => new
                {
                    PostEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostEdits", x => x.PostEditId);
                });

            migrationBuilder.CreateTable(
                name: "PostPendencies",
                columns: table => new
                {
                    PendencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaitingForUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FulfilledUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReminderUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostPendencies", x => x.PendencyId);
                    table.ForeignKey(
                        name: "FK_PostPendencies_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostPendencies_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GameText = table.Column<string>(type: "text", nullable: false),
                    MetagameText = table.Column<string>(type: "text", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.PostId);
                    table.ForeignKey(
                        name: "FK_Posts_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId");
                    table.ForeignKey(
                        name: "FK_Posts_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationNumber = table.Column<int>(type: "integer", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Preview = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommentsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.PublicationId);
                    table.ForeignKey(
                        name: "FK_Publications_Blogs_BlogId",
                        column: x => x.BlogId,
                        principalTable: "Blogs",
                        principalColumn: "BlogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomAccesses",
                columns: table => new
                {
                    AccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReaderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Policy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAccesses", x => x.AccessId);
                    table.ForeignKey(
                        name: "FK_RoomAccesses_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId");
                    table.ForeignKey(
                        name: "FK_RoomAccesses_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RubricAccesses",
                columns: table => new
                {
                    RubricAccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RubricAccesses", x => x.RubricAccessId);
                });

            migrationBuilder.CreateTable(
                name: "Rubrics",
                columns: table => new
                {
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AccessType = table.Column<int>(type: "integer", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rubrics", x => x.RubricId);
                    table.ForeignKey(
                        name: "FK_Rubrics_Blogs_BlogId",
                        column: x => x.BlogId,
                        principalTable: "Blogs",
                        principalColumn: "BlogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Settings = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.SubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "TicketResponses",
                columns: table => new
                {
                    TicketResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFromModerator = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketResponses", x => x.TicketResponseId);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    AssignedModeratorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Answer = table.Column<string>(type: "text", nullable: true),
                    WarningId = table.Column<Guid>(type: "uuid", nullable: true),
                    BanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_Tickets_Bans_BanId",
                        column: x => x.BanId,
                        principalTable: "Bans",
                        principalColumn: "BanId");
                });

            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.TokenId);
                    table.ForeignKey(
                        name: "FK_Tokens_Blogs_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Blogs",
                        principalColumn: "BlogId");
                    table.ForeignKey(
                        name: "FK_Tokens_Games_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Games",
                        principalColumn: "GameId");
                });

            migrationBuilder.CreateTable(
                name: "TopicEdits",
                columns: table => new
                {
                    TopicEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicEdits", x => x.TopicEditId);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicNumber = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsAttached = table.Column<bool>(type: "boolean", nullable: false),
                    AttachOrder = table.Column<int>(type: "integer", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.TopicId);
                    table.ForeignKey(
                        name: "FK_Topics_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Topics_Comments_LastCommentId",
                        column: x => x.LastCommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId");
                });

            migrationBuilder.CreateTable(
                name: "Uploads",
                columns: table => new
                {
                    UploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Original = table.Column<bool>(type: "boolean", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MediumFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SmallFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.UploadId);
                    // NOTE: FK constraints on EntityId are removed because it's a polymorphic column
                    // that can reference Users, Games, Characters, or Posts depending on UploadType.
                    // Application logic ensures referential integrity.
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsHonorary = table.Column<bool>(type: "boolean", nullable: false),
                    AccessPolicy = table.Column<int>(type: "integer", nullable: false),
                    Salt = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PasswordHashVersion = table.Column<int>(type: "integer", nullable: false),
                    RatingDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    QualityRating = table.Column<int>(type: "integer", nullable: false),
                    QuantityRating = table.Column<int>(type: "integer", nullable: false),
                    IsNewbie = table.Column<bool>(type: "boolean", nullable: false, computedColumnSql: "\"QuantityRating\" < 100", stored: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    BirthdayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ShowBirthday = table.Column<bool>(type: "boolean", nullable: false),
                    Info = table.Column<string>(type: "text", nullable: true),
                    AvatarUploadId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscordId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TelegramId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Uploads_AvatarUploadId",
                        column: x => x.AvatarUploadId,
                        principalTable: "Uploads",
                        principalColumn: "UploadId",
                        onDelete: ReferentialAction.SetNull);
                });

            // Seed system user (Robot Administrator) for automated actions
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "Username", "Email", "CreatedUtc", "Role", "IsHonorary", "AccessPolicy", "Salt", "PasswordHash", "PasswordHashVersion", "RatingDisabled", "QualityRating", "QuantityRating", "IsRemoved", "Gender", "ShowBirthday" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // SystemUser.Id
                    "Робот-Администратор", // SystemUser.Username
                    "system@dm.local", // SystemUser.Email
                    new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), // CreatedUtc
                    6, // UserRole.System
                    false, // IsHonorary
                    0, // AccessPolicy.NotSpecified
                    "", // Salt (not used, cannot login)
                    "", // PasswordHash (not used, cannot login)
                    0, // PasswordHashVersion
                    true, // RatingDisabled
                    0, // QualityRating
                    0, // QuantityRating
                    false, // IsRemoved
                    0, // Gender.NotSpecified
                    false // ShowBirthday
                });

            // Seed global chat (well-known ID for site-wide chat)
            migrationBuilder.InsertData(
                table: "Chats",
                columns: new[] { "ChatId", "Type", "Title", "RoomId", "LastMessageId" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // Chat.GlobalChatId
                    2, // ChatType.Global
                    "Глобальный чат", // Title
                    null, // RoomId
                    null // LastMessageId
                });

            // === UserEndorsements: Positive recommendations of users (BBCode, no likes) ===
            migrationBuilder.CreateTable(
                name: "UserEndorsements",
                columns: table => new
                {
                    UserEndorsementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEndorsements", x => x.UserEndorsementId);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            // === WebsiteTestimonials: Positive website reviews (plain text, no likes) ===
            migrationBuilder.CreateTable(
                name: "WebsiteTestimonials",
                columns: table => new
                {
                    WebsiteTestimonialId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebsiteTestimonials", x => x.WebsiteTestimonialId);
                    table.ForeignKey(
                        name: "FK_WebsiteTestimonials_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WebsiteTestimonials_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WebsiteTestimonials_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            // === GameReviews: Reviews of games by players (BBCode, no likes) ===
            migrationBuilder.CreateTable(
                name: "GameReviews",
                columns: table => new
                {
                    GameReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameReviews", x => x.GameReviewId);
                    table.ForeignKey(
                        name: "FK_GameReviews_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameReviews_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameReviews_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GameReviews_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            // === PostReviews: Post reviews with ratings (BBCode, HAS likes) ===
            migrationBuilder.CreateTable(
                name: "PostReviews",
                columns: table => new
                {
                    PostReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostAuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: true),
                    SignValue = table.Column<short>(type: "smallint", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReviews", x => x.PostReviewId);
                    table.ForeignKey(
                        name: "FK_PostReviews_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostReviews_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "PostId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostReviews_Users_PostAuthorId",
                        column: x => x.PostAuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostReviews_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostReviews_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PostReviews_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserBlacklists",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBlacklists", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_UserBlacklists_Users_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBlacklists_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserChatLinks",
                columns: table => new
                {
                    UserChatLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChatLinks", x => x.UserChatLinkId);
                    table.ForeignKey(
                        name: "FK_UserChatLinks_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChatLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChatLinks_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserContacts",
                columns: table => new
                {
                    UserContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContactValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContacts", x => x.UserContactId);
                    table.ForeignKey(
                        name: "FK_UserContacts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginRecords",
                columns: table => new
                {
                    UserLoginRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LoginUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginRecords", x => x.UserLoginRecordId);
                    table.ForeignKey(
                        name: "FK_UserLoginRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsernameChangeRequests",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedUsername = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovalToken = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalTokenExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolverComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsernameChangeRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_UsernameChangeRequests_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_UsernameChangeRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsernameHistories",
                columns: table => new
                {
                    UsernameHistoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldUsername = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewUsername = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsernameHistories", x => x.UsernameHistoryId);
                    table.ForeignKey(
                        name: "FK_UsernameHistories_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_UsernameHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfileNotes",
                columns: table => new
                {
                    UserProfileNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfileNotes", x => x.UserProfileNoteId);
                    table.ForeignKey(
                        name: "FK_UserProfileNotes_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProfileNotes_Users_SubjectUserId",
                        column: x => x.SubjectUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Warnings",
                columns: table => new
                {
                    WarningId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings", x => x.WarningId);
                    table.ForeignKey(
                        name: "FK_Warnings_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Warnings_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Add FKs for Notepad entities (created before Users table)
            migrationBuilder.AddForeignKey(
                name: "FK_NotepadCategories_Users_DeletedByUserId",
                table: "NotepadCategories",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NotepadEntries_Users_DeletedByUserId",
                table: "NotepadEntries",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_Bans_AuthorId",
                table: "Bans",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Bans_TargetUserId",
                table: "Bans",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogAssistants_BlogId",
                table: "BlogAssistants",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogAssistants_UserId",
                table: "BlogAssistants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogBlacklists_BlockedByUserId",
                table: "BlogBlacklists",
                column: "BlockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogBlacklists_BlockedUserId",
                table: "BlogBlacklists",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogBlacklists_BlogId",
                table: "BlogBlacklists",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_AuthorId",
                table: "Blogs",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_DeletedByUserId",
                table: "Blogs",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_MentorId",
                table: "Blogs",
                column: "MentorId");

            // Performance index for popularity sorting
            migrationBuilder.CreateIndex(
                name: "IX_Blogs_PopularityScore",
                table: "Blogs",
                column: "PopularityScore");

            // Unique index for PublicId lookups
            migrationBuilder.CreateIndex(
                name: "IX_Blogs_PublicId",
                table: "Blogs",
                column: "PublicId",
                unique: true);

            // Unique index for SerialNumber (auto-increment)
            migrationBuilder.CreateIndex(
                name: "IX_Blogs_SerialNumber",
                table: "Blogs",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardModerators_BoardId",
                table: "BoardModerators",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardModerators_UserId",
                table: "BoardModerators",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_LastCommentAuthorId",
                table: "Boards",
                column: "LastCommentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_LastCommentId",
                table: "Boards",
                column: "LastCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_LastTopicAuthorId",
                table: "Boards",
                column: "LastTopicAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_LastTopicId",
                table: "Boards",
                column: "LastTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAttributes_CharacterId",
                table: "CharacterAttributes",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEdits_CharacterId",
                table: "CharacterEdits",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEdits_EditorUserId",
                table: "CharacterEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_AuthorId",
                table: "Characters",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_DeletedByUserId",
                table: "Characters",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_GameId",
                table: "Characters",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_LastMessageId",
                table: "Chats",
                column: "LastMessageId");

            // Unique index for PublicId lookups (only for group chats)
            migrationBuilder.CreateIndex(
                name: "IX_Chats_PublicId",
                table: "Chats",
                column: "PublicId",
                unique: true,
                filter: "\"PublicId\" IS NOT NULL");

            // Unique index for SerialNumber
            migrationBuilder.CreateIndex(
                name: "IX_Chats_SerialNumber",
                table: "Chats",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentEdits_CommentId",
                table: "CommentEdits",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentEdits_EditorUserId",
                table: "CommentEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_DeletedByUserId",
                table: "Comments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_EntityId",
                table: "Comments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_GameAssistants_GameId",
                table: "GameAssistants",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameAssistants_UserId",
                table: "GameAssistants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameBlacklists_BlockedByUserId",
                table: "GameBlacklists",
                column: "BlockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameBlacklists_BlockedUserId",
                table: "GameBlacklists",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameBlacklists_GameId",
                table: "GameBlacklists",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_MasterId",
                table: "Games",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_MentorId",
                table: "Games",
                column: "MentorId");

            // Performance index for popularity sorting
            migrationBuilder.CreateIndex(
                name: "IX_Games_PopularityScore",
                table: "Games",
                column: "PopularityScore");

            // Performance index for status-based queries
            migrationBuilder.CreateIndex(
                name: "IX_Games_Status_IsRemoved",
                table: "Games",
                columns: new[] { "Status", "IsRemoved" });

            // Unique index for PublicId lookups
            migrationBuilder.CreateIndex(
                name: "IX_Games_PublicId",
                table: "Games",
                column: "PublicId",
                unique: true);

            // Unique index for SerialNumber (auto-increment)
            migrationBuilder.CreateIndex(
                name: "IX_Games_SerialNumber",
                table: "Games",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_DeletedByUserId",
                table: "Games",
                column: "DeletedByUserId");

            // Performance index for active characters query (popularity calculation)
            migrationBuilder.CreateIndex(
                name: "IX_Characters_GameId_Status_IsNpc_AuthorId",
                table: "Characters",
                columns: new[] { "GameId", "Status", "IsNpc", "AuthorId" });

            // Performance index for subscriptions lookup (popularity calculation)
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TargetType_TargetId",
                table: "Subscriptions",
                columns: new[] { "TargetType", "TargetId" });

            // Performance index for active users (active readers calculation)
            migrationBuilder.CreateIndex(
                name: "IX_Users_LastActivityUtc",
                table: "Users",
                column: "LastActivityUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GameTags_GameId",
                table: "GameTags",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTags_TagId",
                table: "GameTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalChatEventParticipants_GlobalChatEventId",
                table: "GlobalChatEventParticipants",
                column: "GlobalChatEventId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalChatEventParticipants_UserId",
                table: "GlobalChatEventParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalChatEvents_CreatedByUserId",
                table: "GlobalChatEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_UserId",
                table: "Likes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_DeletedByUserId",
                table: "Likes",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEdits_EditorUserId",
                table: "MessageEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEdits_MessageId",
                table: "MessageEdits",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId",
                table: "Messages",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeletedByUserId",
                table: "Messages",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GlobalChatEventId",
                table: "Messages",
                column: "GlobalChatEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId",
                table: "Messages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedProfileNotes_AuthorId",
                table: "ModeratedProfileNotes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedProfileNotes_UserId",
                table: "ModeratedProfileNotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadCategories_AuthorId",
                table: "NotepadCategories",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadCategories_DeletedByUserId",
                table: "NotepadCategories",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_AuthorId",
                table: "NotepadEntries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_DeletedByUserId",
                table: "NotepadEntries",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_CategoryId",
                table: "NotepadEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_CreatedUtc",
                table: "PendingRegistrations",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_TokenId",
                table: "PendingRegistrations",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostEdits_EditorUserId",
                table: "PostEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostEdits_PostId",
                table: "PostEdits",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_CharacterId",
                table: "PostPendencies",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_CreatedById",
                table: "PostPendencies",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_RoomId",
                table: "PostPendencies",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_WaitingForUserId",
                table: "PostPendencies",
                column: "WaitingForUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId",
                table: "Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CharacterId",
                table: "Posts",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_DeletedByUserId",
                table: "Posts",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_RoomId",
                table: "Posts",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_AuthorId",
                table: "Publications",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_BlogId",
                table: "Publications",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_DeletedByUserId",
                table: "Publications",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_ModifiedByUserId",
                table: "Publications",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_RubricId",
                table: "Publications",
                column: "RubricId");

            // Unique index for PublicationNumber within a Blog
            migrationBuilder.CreateIndex(
                name: "IX_Publications_BlogId_PublicationNumber",
                table: "Publications",
                columns: new[] { "BlogId", "PublicationNumber" },
                unique: true);

            // === UserEndorsements indexes ===
            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_AuthorId_TargetUserId",
                table: "UserEndorsements",
                columns: new[] { "AuthorId", "TargetUserId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_TargetUserId",
                table: "UserEndorsements",
                column: "TargetUserId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_ModifiedByUserId",
                table: "UserEndorsements",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_DeletedByUserId",
                table: "UserEndorsements",
                column: "DeletedByUserId");

            // === WebsiteTestimonials indexes ===
            migrationBuilder.CreateIndex(
                name: "IX_WebsiteTestimonials_AuthorId",
                table: "WebsiteTestimonials",
                column: "AuthorId",
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteTestimonials_ModifiedByUserId",
                table: "WebsiteTestimonials",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteTestimonials_DeletedByUserId",
                table: "WebsiteTestimonials",
                column: "DeletedByUserId");

            // === GameReviews indexes ===
            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_AuthorId_GameId",
                table: "GameReviews",
                columns: new[] { "AuthorId", "GameId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_GameId",
                table: "GameReviews",
                column: "GameId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_ModifiedByUserId",
                table: "GameReviews",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_DeletedByUserId",
                table: "GameReviews",
                column: "DeletedByUserId");

            // === PostReviews indexes ===
            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_AuthorId_PostId",
                table: "PostReviews",
                columns: new[] { "AuthorId", "PostId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_PostAuthorId",
                table: "PostReviews",
                column: "PostAuthorId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_GameId",
                table: "PostReviews",
                column: "GameId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_PostId",
                table: "PostReviews",
                column: "PostId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_ModifiedByUserId",
                table: "PostReviews",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_DeletedByUserId",
                table: "PostReviews",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_CharacterId",
                table: "RoomAccesses",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_ReaderUserId",
                table: "RoomAccesses",
                column: "ReaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_RoomId",
                table: "RoomAccesses",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_GameId",
                table: "Rooms",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_NextRoomId",
                table: "Rooms",
                column: "NextRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_PreviousRoomId",
                table: "Rooms",
                column: "PreviousRoomId");

            // Unique index for RoomNumber within a Game
            migrationBuilder.CreateIndex(
                name: "IX_Rooms_GameId_RoomNumber",
                table: "Rooms",
                columns: new[] { "GameId", "RoomNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_DeletedByUserId",
                table: "Rooms",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricAccesses_RubricId",
                table: "RubricAccesses",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricAccesses_UserId",
                table: "RubricAccesses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rubrics_BlogId",
                table: "Rubrics",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_Rubrics_DeletedByUserId",
                table: "Rubrics",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_SubscriberId",
                table: "Subscriptions",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagGroupId",
                table: "Tags",
                column: "TagGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketResponses_AuthorId",
                table: "TicketResponses",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketResponses_TicketId",
                table: "TicketResponses",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AnswerAuthorId",
                table: "Tickets",
                column: "AnswerAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedModeratorId",
                table: "Tickets",
                column: "AssignedModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BanId",
                table: "Tickets",
                column: "BanId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TargetId",
                table: "Tickets",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UserId",
                table: "Tickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_WarningId",
                table: "Tickets",
                column: "WarningId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_CreatorId",
                table: "Tokens",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_EntityId",
                table: "Tokens",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_UserId_Type",
                table: "Tokens",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_DeletedByUserId",
                table: "Tokens",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicEdits_EditorUserId",
                table: "TopicEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicEdits_TopicId",
                table: "TopicEdits",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_AuthorId",
                table: "Topics",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_BoardId",
                table: "Topics",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_DeletedByUserId",
                table: "Topics",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_LastCommentId",
                table: "Topics",
                column: "LastCommentId");

            // Unique index for TopicNumber within a Board
            migrationBuilder.CreateIndex(
                name: "IX_Topics_BoardId_TopicNumber",
                table: "Topics",
                columns: new[] { "BoardId", "TopicNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_EntityId",
                table: "Uploads",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UserId",
                table: "Uploads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_DeletedByUserId",
                table: "Uploads",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlacklists_BlockedUserId",
                table: "UserBlacklists",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlacklists_OwnerId_BlockedUserId",
                table: "UserBlacklists",
                columns: new[] { "OwnerId", "BlockedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserChatLinks_ChatId",
                table: "UserChatLinks",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatLinks_UserId",
                table: "UserChatLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatLinks_DeletedByUserId",
                table: "UserChatLinks",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserContacts_UserId",
                table: "UserContacts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_user_login_records_ip",
                table: "UserLoginRecords",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "ix_user_login_records_user_date",
                table: "UserLoginRecords",
                columns: new[] { "UserId", "LoginUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsernameChangeRequests_ResolvedByUserId",
                table: "UsernameChangeRequests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameChangeRequests_UserId",
                table: "UsernameChangeRequests",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistories_ApprovedByUserId",
                table: "UsernameHistories",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistories_OldUsername",
                table: "UsernameHistories",
                column: "OldUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistories_UserId",
                table: "UsernameHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileNotes_OwnerId_SubjectUserId",
                table: "UserProfileNotes",
                columns: new[] { "OwnerId", "SubjectUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileNotes_SubjectUserId",
                table: "UserProfileNotes",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AvatarUploadId",
                table: "Users",
                column: "AvatarUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email_Lower",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username_Lower",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_AuthorId",
                table: "Warnings",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_TargetUserId",
                table: "Warnings",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bans_Users_AuthorId",
                table: "Bans",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bans_Users_TargetUserId",
                table: "Bans",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogAssistants_Blogs_BlogId",
                table: "BlogAssistants",
                column: "BlogId",
                principalTable: "Blogs",
                principalColumn: "BlogId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogAssistants_Users_UserId",
                table: "BlogAssistants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogBlacklists_Blogs_BlogId",
                table: "BlogBlacklists",
                column: "BlogId",
                principalTable: "Blogs",
                principalColumn: "BlogId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogBlacklists_Users_BlockedByUserId",
                table: "BlogBlacklists",
                column: "BlockedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogBlacklists_Users_BlockedUserId",
                table: "BlogBlacklists",
                column: "BlockedUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Users_AuthorId",
                table: "Blogs",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Users_DeletedByUserId",
                table: "Blogs",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Users_MentorId",
                table: "Blogs",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardModerators_Boards_BoardId",
                table: "BoardModerators",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardModerators_Users_UserId",
                table: "BoardModerators",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Comments_LastCommentId",
                table: "Boards",
                column: "LastCommentId",
                principalTable: "Comments",
                principalColumn: "CommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Users_LastCommentAuthorId",
                table: "Boards",
                column: "LastCommentAuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Topics_LastTopicId",
                table: "Boards",
                column: "LastTopicId",
                principalTable: "Topics",
                principalColumn: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Users_LastTopicAuthorId",
                table: "Boards",
                column: "LastTopicAuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterAttributes_Characters_CharacterId",
                table: "CharacterAttributes",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterEdits_Characters_CharacterId",
                table: "CharacterEdits",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterEdits_Users_EditorUserId",
                table: "CharacterEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Games_GameId",
                table: "Characters",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_AuthorId",
                table: "Characters",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_DeletedByUserId",
                table: "Characters",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_Messages_LastMessageId",
                table: "Chats",
                column: "LastMessageId",
                principalTable: "Messages",
                principalColumn: "MessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommentEdits_Comments_CommentId",
                table: "CommentEdits",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommentEdits_Users_EditorUserId",
                table: "CommentEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            // NOTE: FK_Comments_Topics_EntityId removed - EntityId is polymorphic (Topics, Games, Blogs, Publications)

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_AuthorId",
                table: "Comments",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_DeletedByUserId",
                table: "Comments",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameAssistants_Games_GameId",
                table: "GameAssistants",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameAssistants_Users_UserId",
                table: "GameAssistants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameBlacklists_Games_GameId",
                table: "GameBlacklists",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameBlacklists_Users_BlockedByUserId",
                table: "GameBlacklists",
                column: "BlockedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameBlacklists_Users_BlockedUserId",
                table: "GameBlacklists",
                column: "BlockedUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_MasterId",
                table: "Games",
                column: "MasterId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_MentorId",
                table: "Games",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_DeletedByUserId",
                table: "Games",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Users_DeletedByUserId",
                table: "Rooms",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalChatEventParticipants_GlobalChatEvents_GlobalChatEven~",
                table: "GlobalChatEventParticipants",
                column: "GlobalChatEventId",
                principalTable: "GlobalChatEvents",
                principalColumn: "GlobalChatEventId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalChatEventParticipants_Users_UserId",
                table: "GlobalChatEventParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalChatEvents_Users_CreatedByUserId",
                table: "GlobalChatEvents",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Users_UserId",
                table: "Likes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Users_DeletedByUserId",
                table: "Likes",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageEdits_Messages_MessageId",
                table: "MessageEdits",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "MessageId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageEdits_Users_EditorUserId",
                table: "MessageEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_DeletedByUserId",
                table: "Messages",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_UserId",
                table: "Messages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModeratedProfileNotes_Users_AuthorId",
                table: "ModeratedProfileNotes",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModeratedProfileNotes_Users_UserId",
                table: "ModeratedProfileNotes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotepadCategories_Users_AuthorId",
                table: "NotepadCategories",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotepadEntries_Users_AuthorId",
                table: "NotepadEntries",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostEdits_Posts_PostId",
                table: "PostEdits",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "PostId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostEdits_Users_EditorUserId",
                table: "PostEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostPendencies_Users_CreatedById",
                table: "PostPendencies",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostPendencies_Users_WaitingForUserId",
                table: "PostPendencies",
                column: "WaitingForUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Users_AuthorId",
                table: "Posts",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Users_DeletedByUserId",
                table: "Posts",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Rubrics_RubricId",
                table: "Publications",
                column: "RubricId",
                principalTable: "Rubrics",
                principalColumn: "RubricId");

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Users_AuthorId",
                table: "Publications",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Users_DeletedByUserId",
                table: "Publications",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Users_ModifiedByUserId",
                table: "Publications",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            // Foreign keys for review tables are added inline during table creation

            migrationBuilder.AddForeignKey(
                name: "FK_RoomAccesses_Users_ReaderUserId",
                table: "RoomAccesses",
                column: "ReaderUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RubricAccesses_Rubrics_RubricId",
                table: "RubricAccesses",
                column: "RubricId",
                principalTable: "Rubrics",
                principalColumn: "RubricId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RubricAccesses_Users_UserId",
                table: "RubricAccesses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rubrics_Users_DeletedByUserId",
                table: "Rubrics",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_SubscriberId",
                table: "Subscriptions",
                column: "SubscriberId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketResponses_Tickets_TicketId",
                table: "TicketResponses",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "TicketId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketResponses_Users_AuthorId",
                table: "TicketResponses",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AnswerAuthorId",
                table: "Tickets",
                column: "AnswerAuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssignedModeratorId",
                table: "Tickets",
                column: "AssignedModeratorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_TargetId",
                table: "Tickets",
                column: "TargetId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_UserId",
                table: "Tickets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Warnings_WarningId",
                table: "Tickets",
                column: "WarningId",
                principalTable: "Warnings",
                principalColumn: "WarningId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tokens_Users_CreatorId",
                table: "Tokens",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tokens_Users_UserId",
                table: "Tokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tokens_Users_DeletedByUserId",
                table: "Tokens",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicEdits_Topics_TopicId",
                table: "TopicEdits",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "TopicId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicEdits_Users_EditorUserId",
                table: "TopicEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Users_AuthorId",
                table: "Topics",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Users_DeletedByUserId",
                table: "Topics",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            // NOTE: FK_Uploads_Users_EntityId is not created because EntityId is polymorphic

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_UserId",
                table: "Uploads",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_DeletedByUserId",
                table: "Uploads",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            // Trigram indexes for fuzzy search
            migrationBuilder.Sql(
                "CREATE INDEX IX_Users_Username_Trgm ON \"Users\" USING gin (\"Username\" gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX IX_UsernameHistories_OldUsername_Trgm ON \"UsernameHistories\" USING gin (\"OldUsername\" gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX IX_Games_Title_Trgm ON \"Games\" USING gin (\"Title\" gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX IX_Blogs_Title_Trgm ON \"Blogs\" USING gin (\"Title\" gin_trgm_ops);");

            // Seed forum topic for website reviews
            migrationBuilder.InsertData(
                table: "Topics",
                columns: new[] { "TopicId", "BoardId", "TopicNumber", "AuthorId", "CreatedUtc", "Title", "Text", "IsAttached", "IsClosed", "CommentCount", "LastCommentId", "IsRemoved", "DeletedByUserId", "DeletedUtc" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // TopicId
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // BoardId (Общий)
                    1, // TopicNumber
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // AuthorId (Robot)
                    new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), // CreatedUtc
                    "Отзывы о ДМ", // Title
                    "Ваши отзывы отсюда попадают (после минимального анализа на нарушения правил) прямиком на главную.", // Text
                    true, // IsAttached (закреплена)
                    false, // IsClosed
                    0, // CommentCount
                    null, // LastCommentId
                    false, // IsRemoved
                    null, // DeletedByUserId
                    null // DeletedUtc
                });

            // Seed forum topic for admin discussion (referenced in helpLinks.ts)
            migrationBuilder.InsertData(
                table: "Topics",
                columns: new[] { "TopicId", "BoardId", "TopicNumber", "AuthorId", "CreatedUtc", "Title", "Text", "IsAttached", "IsClosed", "CommentCount", "LastCommentId", "IsRemoved", "DeletedByUserId", "DeletedUtc" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000100"), // TopicId (well-known ID)
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // BoardId (Общий)
                    2, // TopicNumber
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // AuthorId (Robot)
                    new DateTimeOffset(2020, 1, 1, 0, 0, 1, TimeSpan.Zero), // CreatedUtc (+1 sec to be "newer")
                    "Обсуждение действий администрации", // Title
                    "Здесь можно обсудить решения модераторов и администрации. Конструктивная критика приветствуется.", // Text
                    true, // IsAttached (закреплена)
                    false, // IsClosed
                    0, // CommentCount
                    null, // LastCommentId
                    false, // IsRemoved
                    null, // DeletedByUserId
                    null // DeletedUtc
                });

            // Update Board's LastTopic fields and TopicsCount for seeded topics
            migrationBuilder.UpdateData(
                table: "Boards",
                keyColumn: "BoardId",
                keyValue: Guid.Parse("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "TopicsCount", "LastTopicId", "LastTopicNumber", "LastTopicTitle", "LastTopicAuthorId", "LastTopicCreatedUtc" },
                values: new object[]
                {
                    2, // TopicsCount
                    Guid.Parse("00000000-0000-0000-0000-000000000100"), // LastTopicId (admin discussion)
                    2, // LastTopicNumber
                    "Обсуждение действий администрации", // LastTopicTitle
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // LastTopicAuthorId (Robot)
                    new DateTimeOffset(2020, 1, 1, 0, 0, 1, TimeSpan.Zero) // LastTopicCreatedUtc
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Boards_Users_LastCommentAuthorId",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_AuthorId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_DeletedByUserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_AuthorId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_DeletedByUserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_MasterId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_MentorId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_DeletedByUserId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Users_DeletedByUserId",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_GlobalChatEvents_Users_CreatedByUserId",
                table: "GlobalChatEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_DeletedByUserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_UserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_AuthorId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_DeletedByUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_AuthorId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_DeletedByUserId",
                table: "Topics");

            // NOTE: FK_Uploads_Users_EntityId doesn't exist (EntityId is polymorphic)

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_UserId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_DeletedByUserId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_GameReviews_Users_DeletedByUserId",
                table: "GameReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_PostReviews_Users_DeletedByUserId",
                table: "PostReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Boards_BoardId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Comments_LastCommentId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Chats_Messages_LastMessageId",
                table: "Chats");

            migrationBuilder.DropTable(
                name: "BlogAssistants");

            migrationBuilder.DropTable(
                name: "BlogBlacklists");

            migrationBuilder.DropTable(
                name: "BoardModerators");

            migrationBuilder.DropTable(
                name: "CharacterAttributes");

            migrationBuilder.DropTable(
                name: "CharacterEdits");

            migrationBuilder.DropTable(
                name: "CommentEdits");

            migrationBuilder.DropTable(
                name: "GameAssistants");

            migrationBuilder.DropTable(
                name: "GameBlacklists");

            migrationBuilder.DropTable(
                name: "GameTags");

            migrationBuilder.DropTable(
                name: "GlobalChatEventParticipants");

            migrationBuilder.DropTable(
                name: "Likes");

            migrationBuilder.DropTable(
                name: "MessageEdits");

            migrationBuilder.DropTable(
                name: "ModeratedProfileNotes");

            migrationBuilder.DropTable(
                name: "NotepadEntries");

            migrationBuilder.DropTable(
                name: "OutboxEvents");

            migrationBuilder.DropTable(
                name: "PendingRegistrations");

            migrationBuilder.DropTable(
                name: "PostEdits");

            migrationBuilder.DropTable(
                name: "PostPendencies");

            migrationBuilder.DropTable(
                name: "Publications");

            migrationBuilder.DropTable(
                name: "UserEndorsements");

            migrationBuilder.DropTable(
                name: "GameReviews");

            migrationBuilder.DropTable(
                name: "PostReviews");

            migrationBuilder.DropTable(
                name: "RoomAccesses");

            migrationBuilder.DropTable(
                name: "RubricAccesses");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TicketResponses");

            migrationBuilder.DropTable(
                name: "WebsiteTestimonials");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "TopicEdits");

            migrationBuilder.DropTable(
                name: "UserBlacklists");

            migrationBuilder.DropTable(
                name: "UserChatLinks");

            migrationBuilder.DropTable(
                name: "UserContacts");

            migrationBuilder.DropTable(
                name: "UserLoginRecords");

            migrationBuilder.DropTable(
                name: "UsernameChangeRequests");

            migrationBuilder.DropTable(
                name: "UsernameHistories");

            migrationBuilder.DropTable(
                name: "UserProfileNotes");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "NotepadCategories");

            migrationBuilder.DropTable(
                name: "Rubrics");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "TagGroups");

            migrationBuilder.DropTable(
                name: "Blogs");

            migrationBuilder.DropTable(
                name: "Bans");

            migrationBuilder.DropTable(
                name: "Warnings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Uploads");

            migrationBuilder.DropTable(
                name: "Posts");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Chats");

            migrationBuilder.DropTable(
                name: "GlobalChatEvents");

            // Drop trigram indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Users_Username_Trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_UsernameHistories_OldUsername_Trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Games_Title_Trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Blogs_Title_Trgm;");

            // Drop pg_trgm extension
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS pg_trgm;");
        }
    }
}
