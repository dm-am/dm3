using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Entities.Forum;

namespace DM.Infrastructure.Persistence.InitialData;

/// <summary>
/// The boards of the forum and the two topics the site ships with, seeded with
/// the schema.
/// </summary>
/// <remarks>
/// Together, because the first board carries a denormalised account of the topics
/// seeded into it, and the two have to agree.
/// </remarks>
internal static class BoardCatalog
{
    /// <summary>The boards, in the order the forum lists them.</summary>
    internal static Board[] Boards() =>
    [
        new Board
        {
            BoardId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Title = "Общий",
            Alias = "general",
            Description = "Жизнь сообщества и решения администрации",
            Order = 1,
            CreateTopicPolicy = BoardAccessPolicy.RegularUser,
            // The denormalised counters match the two topics seeded below: only a
            // topic write recomputes them. The last topic is the newer of the two.
            TopicsCount = 2,
            LastTopicId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
            LastTopicNumber = 2,
            LastTopicTitle = "Обсуждение действий администрации",
            LastTopicAuthorId = SystemUser.Id,
            LastTopicCreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 1, TimeSpan.Zero)
        },
        Board(0x2, 2, "Игровые системы", "game-systems", "Обсуждение правил и помощь в выборе системы", BoardAccessPolicy.RegularUser),
        Board(0x3, 3, "Поиск мастера и игроков", "looking-for-group", "Набор игроков в игру или поиск мастера", BoardAccessPolicy.RegularUser),
        Board(0x4, 4, "Котел идей", "ideas", "Обкатка задумок и поиск единомышленников", BoardAccessPolicy.RegularUser),
        Board(0x5, 5, "Конкурсы", "contests", "Литературные и творческие состязания", BoardAccessPolicy.Moderator),
        Board(0x6, 6, "Под столом", "off-topic", "Музыка, книги, кино, мемы и все остальное", BoardAccessPolicy.RegularUser),
        Board(0x7, 7, "Неролевые игры", "forum-games", "Словесные игры, ассоциации и прочие развлечения", BoardAccessPolicy.RegularUser),
        Board(0x8, 8, "Улучшение сайта", "improvements", "Идеи и предложения по развитию сайта", BoardAccessPolicy.RegularUser),
        Board(0x9, 9, "Ошибки", "bugs", "Сообщения об ошибках на сайте", BoardAccessPolicy.RegularUser),
        Board(0xa, 10, "Для новичков", "newbies", "Руководства, ответы на вопросы и помощь новичкам", BoardAccessPolicy.RegularUser),
        Board(0xb, 11, "Новости проекта", "news", "Официальные новости, обновления и статистика", BoardAccessPolicy.Moderator)
    ];

    /// <summary>The two topics of the general board.</summary>
    internal static Topic[] SystemTopics() =>
    [
        new Topic
        {
            TopicId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            BoardId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            TopicNumber = 1,
            AuthorId = SystemUser.Id,
            CreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Title = "Отзывы о ДМ",
            Text = "Ваши отзывы отсюда попадают (после минимального анализа на нарушения правил) прямиком на главную.",
            IsAttached = true,
            IsClosed = false,
            IsRemoved = false
        },
        new Topic
        {
            TopicId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
            BoardId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            TopicNumber = 2,
            AuthorId = SystemUser.Id,
            // One second later, so "the newest topic" is unambiguous
            CreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 1, TimeSpan.Zero),
            Title = "Обсуждение действий администрации",
            Text = "Здесь можно обсудить решения модераторов и администрации. Конструктивная критика приветствуется.",
            IsAttached = true,
            IsClosed = false,
            IsRemoved = false
        }
    ];

    /// <summary>A board with no topics in it yet.</summary>
    private static Board Board(
        int id, int order, string title, string alias, string description, BoardAccessPolicy createTopicPolicy) => new()
        {
            BoardId = new Guid($"00000000-0000-0000-0000-{id:x12}"),
            Title = title,
            Alias = alias,
            Description = description,
            Order = order,
            CreateTopicPolicy = createTopicPolicy,
            TopicsCount = 0
        };
}
