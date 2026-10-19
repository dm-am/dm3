using System;
using DM.Infrastructure.Persistence.Entities.Shared;

namespace DM.Infrastructure.Persistence.InitialData;

/// <summary>
/// The tag groups and the tags the site ships with, seeded with the schema.
/// </summary>
/// <remarks>
/// A tag is one row: its identifier, the number a /games link is written in, its
/// group, its title and description, and its place in the group. The rows go in
/// the order of the groups. The sequence new tags draw their numbers from starts
/// one past the highest number here (ModerationModel), so a tag appended to this
/// catalogue moves that start with it.
/// </remarks>
internal static class TagCatalog
{
    private static readonly Guid SystemGroup = GroupId(1);
    private static readonly Guid GenreGroup = GroupId(2);
    private static readonly Guid GameFormatGroup = GroupId(3);
    private static readonly Guid PostFormatGroup = GroupId(4);
    private static readonly Guid PaceGroup = GroupId(5);
    private static readonly Guid RestrictionsGroup = GroupId(6);
    private static readonly Guid NewbiesGroup = GroupId(7);
    private static readonly Guid SensitiveContentGroup = GroupId(8);

    // MaxTagsPerGame is left out of a group that limits nothing: the column is
    // nullable and null is the absence of a limit, not a value forgotten. The
    // groups that carry one are the ones where a game answering "both" says
    // less than a game answering one thing.
    /// <summary>The groups, in the order the filter shows them.</summary>
    internal static TagGroup[] Groups() =>
    [
        new TagGroup
        {
            TagGroupId = SystemGroup,
            Title = "Система",
            Description = "Ролевая система или набор правил, по которым ведется игра",
            SortOrder = 0,
            MaxTagsPerGame = 2
        },
        new TagGroup
        {
            TagGroupId = GenreGroup,
            Title = "Жанр",
            Description = "Жанр и сеттинг игрового мира",
            SortOrder = 1,
            MaxTagsPerGame = 3
        },
        new TagGroup
        {
            TagGroupId = GameFormatGroup,
            Title = "Формат игры",
            Description = "Тип игрового процесса и взаимодействия между участниками",
            SortOrder = 2,
            MaxTagsPerGame = 2
        },
        new TagGroup
        {
            TagGroupId = PostFormatGroup,
            Title = "Формат постов",
            Description = "Стиль и объем игровых постов",
            SortOrder = 3,
            MaxTagsPerGame = 1
        },
        new TagGroup
        {
            TagGroupId = PaceGroup,
            Title = "Темп",
            Description = "Ожидаемая скорость игры и частота постов",
            SortOrder = 4,
            MaxTagsPerGame = 1
        },
        new TagGroup
        {
            TagGroupId = RestrictionsGroup,
            Title = "Ограничения",
            Description = "Особые требования и ограничения для участников",
            SortOrder = 5
        },
        new TagGroup
        {
            TagGroupId = NewbiesGroup,
            Title = "Новички",
            Description = "Игры от новичков и для новичков",
            SortOrder = 6
        },
        new TagGroup
        {
            TagGroupId = SensitiveContentGroup,
            Title = "Деликатный контент",
            Description = "Контент, требующий осознанного согласия участников",
            SortOrder = 7
        }
    ];

    /// <summary>The tags, group by group.</summary>
    internal static Tag[] Tags() =>
    [
        Tag(0x42, 66, SystemGroup, "2d20", "Движок Modiphius: пул из двух d20 против характеристики", 0),
        Tag(0x43, 67, SystemGroup, "AD&D", "Первая и вторая редакции Dungeons & Dragons", 1),
        Tag(0x1, 1, SystemGroup, "Black Bird Pie", "Простая система с кубиком d6", 2),
        Tag(0x44, 68, SystemGroup, "Cypher", "Движок Monte Cook Games: сложность против броска d20", 3),
        Tag(0x2, 2, SystemGroup, "D&D", "Семейство Dungeons & Dragons", 4),
        Tag(0x45, 69, SystemGroup, "D&D 3.5", "Третья редакция Dungeons & Dragons, включая 3.0", 5),
        Tag(0x46, 70, SystemGroup, "D&D 4e", "Четвертая редакция Dungeons & Dragons", 6),
        Tag(0x3, 3, SystemGroup, "D&D 5e", "Пятая редакция Dungeons & Dragons", 7),
        Tag(0x4, 4, SystemGroup, "d100", "Системы на основе процентного броска", 8),
        Tag(0x5, 5, SystemGroup, "Dawn of Worlds", "Система для совместного создания мира", 9),
        Tag(0x6, 6, SystemGroup, "Fallout", "Адаптация сеттинга Fallout", 10),
        Tag(0x7, 7, SystemGroup, "FATAL", "Без комментариев", 11),
        Tag(0x8, 8, SystemGroup, "Fate", "Нарративная система с аспектами и фейт-пойнтами", 12),
        Tag(0x47, 71, SystemGroup, "FitD", "Forged in the Dark: системы на движке Blades in the Dark", 13),
        Tag(0x9, 9, SystemGroup, "FUDGE", "Универсальный движок для реализации практически любого концепта", 14),
        Tag(0x48, 72, SystemGroup, "GUMSHOE", "Детективный движок Robin Laws: улики не пропускаются на броске", 15),
        Tag(0xa, 10, SystemGroup, "GURPS", "Универсальная система на базе броска 3d6 vs Сложность", 16),
        Tag(0xb, 11, SystemGroup, "Interlock", "Система от R. Talsorian Games (Cyberpunk 2020 и другие)", 17),
        Tag(0xc, 12, SystemGroup, "Microscope", "Система для создания эпических историй", 18),
        Tag(0xd, 13, SystemGroup, "Pathfinder 1e", "Pathfinder первой редакции", 19),
        Tag(0xe, 14, SystemGroup, "Pathfinder 2e", "Pathfinder второй редакции", 20),
        Tag(0xf, 15, SystemGroup, "PbtA", "Нарративные системы на базе 2d6 vs Сложность", 21),
        Tag(0x10, 16, SystemGroup, "Risus", "Минималистичная комедийная система", 22),
        Tag(0x11, 17, SystemGroup, "Savage Worlds", "Легковесная универсальная система — Fast! Furious! Fun!", 23),
        Tag(0x12, 18, SystemGroup, "Starfinder 1e", "Sci-fi спин-офф Pathfinder", 24),
        Tag(0x13, 19, SystemGroup, "Starfinder 2e", "Starfinder второй редакции", 25),
        Tag(0x14, 20, SystemGroup, "Warhammer", "Системы по вселенной Warhammer", 26),
        Tag(0x15, 21, SystemGroup, "World of Darkness", "Мир Тьмы: Storyteller, Storytelling и Chronicles of Darkness", 27),
        Tag(0x16, 22, SystemGroup, "Авторская", "Оригинальная система от мастера игры", 28),
        Tag(0x17, 23, SystemGroup, "Мафия", "Психологическая детективная командная игра", 29),
        Tag(0x18, 24, SystemGroup, "Словеска", "Игра без формальной системы правил", 30),
        Tag(0x19, 25, SystemGroup, "Эра Водолея", "Отечественная система ролевых игр", 31),
        Tag(0x1a, 26, GenreGroup, "Альтернативная история", "Переосмысление исторических событий", 0),
        Tag(0x1b, 27, GenreGroup, "Боевик", "Акцент на экшн и сражениях", 1),
        Tag(0x1c, 28, GenreGroup, "Детектив", "Расследования и разгадывание тайн", 2),
        Tag(0x1d, 29, GenreGroup, "Зомби", "Зомби-апокалипсис и выживание", 3),
        Tag(0x1e, 30, GenreGroup, "Историческое", "Действие в реальную историческую эпоху", 4),
        Tag(0x1f, 31, GenreGroup, "Киберпанк", "Высокие технологии, низкий уровень жизни", 5),
        Tag(0x20, 32, GenreGroup, "Комедия", "Юмор и абсурдные ситуации", 6),
        Tag(0x21, 33, GenreGroup, "Космоопера", "Эпические приключения в космосе", 7),
        Tag(0x22, 34, GenreGroup, "Мистика", "Сверхъестественные элементы и тайны", 8),
        Tag(0x23, 35, GenreGroup, "Наши дни", "Современный реалистичный сеттинг", 9),
        Tag(0x24, 36, GenreGroup, "Постапокалипсис", "Мир после катастрофы", 10),
        Tag(0x25, 37, GenreGroup, "Психоделика", "Сюрреалистичные и необычные миры", 11),
        Tag(0x26, 38, GenreGroup, "Стимпанк", "Паровые технологии и викторианская эстетика", 12),
        Tag(0x27, 39, GenreGroup, "Триллер", "Напряжение и саспенс", 13),
        Tag(0x28, 40, GenreGroup, "Трэш", "Нарочито нелепый и провокационный контент", 14),
        Tag(0x29, 41, GenreGroup, "Ужасы", "Хоррор и атмосфера страха", 15),
        Tag(0x2a, 42, GenreGroup, "Фантастика", "Научная фантастика и будущее", 16),
        Tag(0x2b, 43, GenreGroup, "Фэнтези", "Магия, мечи и волшебные миры", 17),
        Tag(0x2c, 44, GameFormatGroup, "Dungeon Crawl", "Исследование подземелий и сражения с монстрами", 0),
        Tag(0x2d, 45, GameFormatGroup, "PvP", "Противостояние между игроками", 1),
        Tag(0x2e, 46, GameFormatGroup, "Выживание", "Борьба за выживание в суровых условиях", 2),
        Tag(0x2f, 47, GameFormatGroup, "Песочница", "Открытый мир без сюжетных ограничений", 3),
        Tag(0x30, 48, GameFormatGroup, "Стратегия", "Управление ресурсами и принятие глобальных решений", 4),
        Tag(0x31, 49, GameFormatGroup, "Сюжетная", "Фокус на развитии истории", 5),
        Tag(0x32, 50, GameFormatGroup, "Тактика", "Тактические бои и позиционирование", 6),
        Tag(0x33, 51, PostFormatGroup, "Короткопост", "Короткие посты в 1-3 абзаца", 0),
        Tag(0x34, 52, PostFormatGroup, "Литературная", "Развернутые литературные посты", 1),
        Tag(0x35, 53, PaceGroup, "Неторопливый", "Посты раз в несколько дней", 1),
        Tag(0x36, 54, PaceGroup, "Скоростной", "Несколько постов в день", 0),
        Tag(0x3e, 62, PaceGroup, "Сухие сезоны", "Возможны продолжительные периоды без постов", 2),
        Tag(0x37, 55, RestrictionsGroup, "Без мата", "Нецензурная лексика запрещена", 1),
        Tag(0x38, 56, RestrictionsGroup, "Без насилия", "Минимум жестокости и крови", 2),
        Tag(0x39, 57, RestrictionsGroup, "Grammar Nazi", "Повышенные требования к грамотности", 0),
        Tag(0x3a, 58, RestrictionsGroup, "Для своих", "Игра для знакомой компании", 3),
        Tag(0x3b, 59, RestrictionsGroup, "Обязателен мессенджер", "Обсуждение игровых вопросов во внешнем мессенджере", 4),
        Tag(0x3c, 60, NewbiesGroup, "Для новичков", "Игра подходит для начинающих", 0),
        Tag(0x3d, 61, NewbiesGroup, "Мастер-новичок", "Мастер игры — начинающий", 1),
        Tag(0x3f, 63, SensitiveContentGroup, "ERP", "Erotic Role-Play: [tip img=\"/images/erp-tooltip.gif\"]эротические сцены[/tip] как основа игрового процесса", 0),
        Tag(0x40, 64, SensitiveContentGroup, "Шок-контент", "Чернуха, максимально шокирующий и отталкивающий контент без ограничений", 2),
        Tag(0x41, 65, SensitiveContentGroup, "Острые темы", "Игра затрагивает спорные или чувствительные социальные темы", 1)
    ];

    private static Tag Tag(int id, int shortId, Guid group, string title, string description, int sortOrder) => new()
    {
        TagId = new Guid($"00000000-0000-0000-0000-{id:x12}"),
        ShortId = shortId,
        TagGroupId = group,
        Title = title,
        Description = description,
        SortOrder = sortOrder
    };

    private static Guid GroupId(int number) => new($"00000000-0000-0000-0005-{number:x12}");
}
