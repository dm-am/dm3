using DM.Domain.Core.Chats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    private static readonly Dictionary<string, (string Title, string Text)[]> BoardTopicTemplates = new()
    {
        ["Общий"] = new[]
        {
            ("Добро пожаловать на форум!", "Здесь вы можете обсудить все, что связано с текстовыми ролевыми играми. Соблюдайте правила и уважайте друг друга!"),
            ("Правила форума", "Основные правила: 1) Уважайте собеседников. 2) Не флудите. 3) Пишите по теме. 4) Не рекламируйте."),
            // "Обсуждение действий администрации" comes with the InitialCreate migration
            ("История банов за {month:-1}", "За месяц выдано три предупреждения и один бан за спам."),
        },
        ["Игровые системы"] = new[]
        {
            ("Обсуждение системы D&D 5e", "Кто что думает о последних дополнениях? Мне нравится новый подкласс для варвара."),
            ("GURPS для новичков — с чего начать?", "Хочу попробовать GURPS, но система кажется сложной. Посоветуйте, какие книги читать в первую очередь?"),
            ("Сравнение Pathfinder 1e и 2e", "Играл долго в первую редакцию, теперь думаю перейти на вторую. Кто уже перешел — как впечатления?"),
        },
        ["Поиск мастера и игроков"] = new[]
        {
            ("Ищу игру в жанре фэнтези", "Привет всем! Ищу игру в классическом фэнтези-сеттинге. Предпочитаю D&D или словески. Опыт 5+ лет."),
            ("[Набор] Кампания по Forgotten Realms", "Набираю 4-5 игроков в длительную кампанию. Система D&D 5e. Начало — через 2 недели."),
            ("Ищу мастера для one-shot horror", "Хотим с друзьями (3 человека) сыграть короткую хоррор-игру. Система любая."),
        },
        ["Котел идей"] = new[]
        {
            ("Идея: Стимпанк в викторианской Англии", "Думаю запустить игру в стимпанк-сеттинге. Есть наработки по лору и системе. Интересно ли это кому-нибудь?"),
            ("Концепт: Космическая станция на краю галактики", "Хочу создать sci-fi игру про жизнь на изолированной станции. Ищу фидбек по идее."),
        },
        ["Конкурсы"] = new[]
        {
            ("Конкурс рассказов", "Открываем ежегодный конкурс рассказов. Тема: \"Новые горизонты\". Рассказы принимаем до конца месяца, голосование в следующем."),
            ("Итоги конкурса персонажей", "Подводим итоги! Победитель — персонаж 'Эльвира Теневая' от Player_One. Поздравляем!"),
        },
        ["Под столом"] = new[]
        {
            ("Музыка для атмосферы игр", "Делитесь плейлистами! Для фэнтези использую саундтреки из Ведьмака и Скайрима."),
            ("Мемы Dungeon Master — подборка за месяц", "Собрал лучшие мемы из наших игр. Осторожно, много смешного!"),
            ("Интервью после полуночи #42", "Новый выпуск! На этот раз гость — TestMentor, ветеран с 10-летним стажем."),
        },
        ["Неролевые игры"] = new[]
        {
            ("Игра в ассоциации", "Правила простые: пишем слово, ассоциирующееся с предыдущим. Начинаю: ДРАКОН"),
            ("Угадай персонажа", "Загадываю персонажа из популярной игры. Задавайте вопросы, на которые можно ответить да/нет."),
        },
        ["Улучшение сайта"] = new[]
        {
            ("Предложение: темная тема сайта", "Было бы здорово добавить темную тему. Глаза устают от светлого фона при ночном чтении."),
            ("Просьба: уведомления в Telegram", "Хотелось бы получать уведомления о новых постах в Telegram. Есть ли такие планы?"),
        },
        ["Ошибки"] = new[]
        {
            ("Ошибка при загрузке аватара", "При попытке загрузить аватар выдает ошибку 500. Файл PNG, размер 200x200. Кто-нибудь сталкивался?"),
            ("Не работает поиск по играм", "При вводе названия в поиск ничего не находится, хотя игра точно существует."),
        },
        ["Для новичков"] = new[]
        {
            ("Помогите разобраться с интерфейсом", "Новичок на сайте, не могу понять, как создать персонажа. Где кнопка?"),
            ("Как найти игру для начинающих?", "Только зарегистрировался, опыта нет. Как найти игру, где примут новичка?"),
            ("FAQ для новых пользователей", "Собрал ответы на частые вопросы. Читайте, прежде чем создавать топик!"),
        },
        ["Новости проекта"] = new[]
        {
            ("Запуск новой версии Dungeon Master!", """
Новая версия сайта открыта. Старые игры, посты и профили переехали вместе с ней.

[b]Что изменилось:[/b]
[ul][li]Другое оформление, есть темная тема[/li][li]Редактор постов с предпросмотром разметки[/li][li]Фильтры игр по тегам, жанрам и статусу набора[/li][/ul]
[b]Для мастеров:[/b]
[ul][li]Настройки игры собраны на одной странице[/li][li]Персонажи и комнаты правятся там же[/li][li]Игроков можно пригласить по имени[/li][/ul]
Нашли ошибку, пишите в раздел "Ошибки", с адресом страницы и тем, что нажимали.
"""),
            ("Фильтры и теги игр", """
У игр появились теги: жанр, система, стиль игры.

[b]Теги[/b]
Мастер ставит своей игре до 10 тегов в настройках. Облако тегов в боковой колонке показывает самые частые.

[b]Фильтры[/b]
[ul][li]По статусу: идет, набор, завершена[/li][li]По тегам: с тегом или без него[/li][li]Сортировка по активности, числу игроков и дате создания[/li][li]Поиск по названию и описанию[/li][/ul]
[b]Как пользоваться:[/b]
1. Откройте страницу "Игры"
2. Выберите теги в панели фильтров
3. Список обновится сам

Теги ставит мастер, так что если вашей игры нет в выдаче, проверьте ее теги.
"""),
            ("Планы на ближайшие месяцы", """
Что делаем дальше.

[b]{Month:+1}[/b]
[ul][li]Версия сайта для телефона[/li][li]Уведомления в браузере о новых постах[/li][/ul]
[b]{Month:+2}[/b]
[ul][li]Бот Discord для уведомлений из игр[/li][li]Достижения игроков и мастеров[/li][/ul]
[b]{Month:+3}[/b]
[ul][li]Больше разметки в блогах[/li][li]Галерея картинок игры[/li][/ul]
Сроки примерные. Предложения пишите в раздел "Улучшение сайта".
"""),
        },
    };

    private static readonly string[] MonthNames =
    [
        "январь", "февраль", "март", "апрель", "май", "июнь",
        "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"
    ];

    private static readonly Regex MonthToken = new(@"\{(?<word>[Mm]onth):(?<shift>[+-]\d+)\}");

    // The template with its months counted from the epoch of the seed, as "октябрь 2026": a month
    // written as a fixed date goes stale with the stand.
    private string Dated(string template) => MonthToken.Replace(template, token =>
    {
        var moment = _now.AddMonths(int.Parse(token.Groups["shift"].Value, CultureInfo.InvariantCulture));
        var name = MonthNames[moment.Month - 1];
        if (token.Groups["word"].Value[0] == 'M')
        {
            name = char.ToUpperInvariant(name[0]) + name[1..];
        }

        return $"{name} {moment.Year}";
    });

    private static readonly (string Title, string Text)[] GenericBoardTopics =
    [
        ("Обсуждение", "Общий топик для обсуждения."),
        ("Вопросы и ответы", "Задавайте вопросы здесь."),
    ];

    private static readonly string[] TopicCommentTexts =
    [
        "Отличный топик, поддерживаю!",
        "Согласен с автором на 100%.",
        "Интересная мысль, но я бы добавил...",
        "Спасибо за информацию!",
        "У меня был похожий опыт.",
        "Не согласен, но уважаю мнение.",
        "Кто-нибудь уже пробовал это?",
        "Жду продолжения обсуждения.",
        "+1 к этому предложению",
        "Хорошая инициатива!",
    ];

    // The remark about the look of the site among the FAQ questions, which a warning names.
    private const string OffTopicFaqComment = "Темная тема — это супер!";

    // The comments of the FAQ topic, in the order they are written: fifty of them, enough for the
    // topic to run over several pages.
    private static readonly string[] FaqCommentTexts =
    [
        "Отличный FAQ! Очень помогло разобраться с основами.",
        "Подскажите, как создать персонажа? Не могу найти кнопку.",
        "Спасибо за подробные инструкции по регистрации.",
        "А где можно посмотреть список активных игр?",
        "Как связаться с мастером игры напрямую?",
        "Отличное сообщество! Всем рекомендую.",
        "Не понял момент про рейтинг. Можете объяснить подробнее?",
        "Когда откроют новый раздел форума?",
        "Прочитал правила, все понятно. Спасибо!",
        "Как изменить аватар? Не нашел настройку.",
        "Подскажите хорошую игру для новичка.",
        "Есть ли ограничения по количеству персонажей?",
        "Как удалить свой комментарий?",
        "Отличная платформа для ролевых игр!",
        "Не работает поиск, это баг или фича?",
        "Когда будет мобильная версия?",
        "Спасибо за быстрый ответ в предыдущем вопросе.",
        "Как вступить в закрытую игру?",
        "Можно ли создать свой форум?",
        "Где посмотреть историю изменений?",
        "Отличный дизайн сайта! Кто делал?",
        "Как настроить уведомления?",
        "Есть ли Discord сервер сообщества?",
        "Подскажите, как форматировать текст.",
        "Можно ли использовать картинки в постах?",
        "Как сменить никнейм?",
        "Где находится раздел с правилами?",
        "Как пожаловаться на нарушение?",
        "Отличная идея с рейтингом постов!",
        OffTopicFaqComment,
        "Как посмотреть свою статистику?",
        "Подскажите, как работает система тегов.",
        "Можно ли экспортировать свои посты?",
        "Как добавить друга в игру?",
        "Что означает значок рядом с ником?",
        "Спасибо за FAQ, очень полезно!",
        "Как отредактировать старый пост?",
        "Есть ли лимит на длину сообщения?",
        "Когда обновят интерфейс?",
        "Как создать опрос в топике?",
        "Подскажите правила оформления постов.",
        "Можно ли привязать Telegram?",
        "Как работает система модерации?",
        "Отличное сообщество, всем привет!",
        "Где найти архив старых игр?",
        "Как восстановить удаленный пост?",
        "Спасибо администрации за работу!",
        "Есть ли мобильное приложение?",
        "Как поменять тему оформления?",
        "Очень удобный интерфейс, молодцы!",
    ];

    private async Task CreateForumContent(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // System topics created by the migration, left out of the count
        var systemTopicIds = new[]
        {
            FixedGuid(0, 0x001), // "Отзывы о ДМ"
            FixedGuid(0, 0x100), // "Обсуждение действий администрации"
        };

        // System topics do not count: the migration's and the period digests the API creates at start.
        var existingTopicsCount = await _dbContext.Set<Topic>()
            .CountAsync(t => !systemTopicIds.Contains(t.TopicId)
                && !_dbContext.PeriodDigestTopics.Any(p => p.TopicId == t.TopicId));
        if (existingTopicsCount > 0)
        {
            result.Details.Add($"Forum topics already exist ({existingTopicsCount}), skipping");
            return;
        }

        var boards = await _dbContext.Set<Board>().OrderBy(b => b.Order).ToListAsync();
        if (boards.Count == 0)
        {
            result.Details.Add("No boards found, skipping forum content");
            return;
        }

        // Numbered after the topics a board already holds, the system topic among them
        var maxTopicNumbers = await _dbContext.Set<Topic>()
            .GroupBy(t => t.BoardId)
            .Select(g => new { BoardId = g.Key, MaxNumber = g.Max(t => t.TopicNumber) })
            .ToDictionaryAsync(x => x.BoardId, x => x.MaxNumber);

        foreach (var board in boards)
        {
            SeedBoardTopics(board, maxTopicNumbers.GetValueOrDefault(board.BoardId, 0), users, result);
        }

        // Save topics and comments first to avoid circular dependency
        await _dbContext.SaveChangesAsync();

        var topicsWithComments = await _dbContext.Set<Topic>()
            .Where(t => t.LastCommentId == null &&
                        _dbContext.Set<DbComment>().Any(c => c.EntityId == t.TopicId))
            .ToListAsync();

        foreach (var topic in topicsWithComments)
        {
            var lastComment = await _dbContext.Set<DbComment>()
                .Where(c => c.EntityId == topic.TopicId)
                .OrderByDescending(c => c.CreatedUtc)
                .FirstOrDefaultAsync();

            if (lastComment != null)
            {
                topic.LastCommentId = lastComment.CommentId;
            }
        }

        result.Details.Add($"Created {result.TopicsCreated} topics with {result.CommentsCreated} comments");
    }

    /// <summary>
    /// The topics of one board, numbered on from the ones it already holds, each
    /// with its comments.
    /// </summary>
    private void SeedBoardTopics(
        Board board, int startNumber, List<DbUser> users, ComprehensiveSeedResult result)
    {
        var topics = BoardTopicTemplates.GetValueOrDefault(board.Title) ?? GenericBoardTopics;

        var canPost = (board.CreateTopicPolicy & (BoardAccessPolicy.RegularUser | BoardAccessPolicy.Guest)) != BoardAccessPolicy.None;

        for (var i = 0; i < topics.Length; i++)
        {
            var template = topics[i];
            var author = canPost
                ? users[_random.Next(users.Count)]
                : users.First(u => u.Role >= UserRole.Mentor);

            var topicId = _guidFactory.Create();

            // News topics: first 2 are recent (within last week), rest are older
            var topicAge = board.Title == "Новости проекта" && i < 2
                ? _random.Next(1, 7) // 1-6 days ago for recent news
                : _random.Next(7, 30); // 7-29 days ago for older content

            var topic = new Topic
            {
                TopicId = topicId,
                BoardId = board.BoardId,
                AuthorId = author.UserId,
                TopicNumber = startNumber + i + 1, // Unique within board, starting after existing topics
                CreatedUtc = _now.AddDays(-topicAge),
                Title = Dated(template.Title),
                Text = Dated(template.Text),
                IsAttached = i == 0 && board.Title == "Новости проекта", // Pin first topic in News
            };

            _dbContext.Set<Topic>().Add(topic);
            result.TopicsCreated++;

            SeedTopicComments(topic, template.Title, users, result);

            board.TopicsCount++;
        }
    }

    private void SeedTopicComments(Topic topic, string topicTitle, List<DbUser> users, ComprehensiveSeedResult result)
    {
        var commentsToCreate = topicTitle == "FAQ для новых пользователей" ? 50 : _random.Next(3, 8);

        for (var j = 0; j < commentsToCreate; j++)
        {
            var commentAuthor = users[_random.Next(users.Count)];
            var commentText = topicTitle == "FAQ для новых пользователей"
                ? FaqCommentTexts[j % FaqCommentTexts.Length]
                : TopicCommentTexts[_random.Next(TopicCommentTexts.Length)];

            var comment = new DbComment
            {
                CommentId = _guidFactory.Create(),
                EntityId = topic.TopicId,
                AuthorId = commentAuthor.UserId,
                CreatedUtc = topic.CreatedUtc.AddHours(j * 12 + _random.Next(1, 12)),
                Text = commentText,
            };

            _dbContext.Set<DbComment>().Add(comment);
            result.CommentsCreated++;
        }
    }

    private async Task AssignBoardModerators(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var existingModeratorsCount = await _dbContext.Set<BoardModerator>().CountAsync();
        if (existingModeratorsCount > 0)
        {
            result.Details.Add($"Board moderators already assigned ({existingModeratorsCount}), skipping");
            return;
        }

        var boards = await _dbContext.Set<Board>().ToListAsync();
        if (boards.Count == 0)
        {
            result.Details.Add("No boards found, skipping moderator assignment");
            return;
        }

        var moderatorUsers = users
            .Where(u => u.Role >= UserRole.Moderator)
            .ToList();

        if (moderatorUsers.Count == 0)
        {
            result.Details.Add("No moderator users found, skipping board moderator assignment");
            return;
        }

        // Every board gets at least one moderator
        var assignments = new Dictionary<string, string[]>
        {
            ["Общий"] = new[] { "TestModerator", "TestSeniorMod" },
            ["Игровые системы"] = new[] { "TestModerator" },
            ["Поиск мастера и игроков"] = new[] { "TestModerator", "TestSeniorMod" },
            ["Котел идей"] = new[] { "TestModerator" },
            ["Конкурсы"] = new[] { "TestSeniorMod" },
            ["Под столом"] = new[] { "TestModerator", "TestMentor" },
            ["Неролевые игры"] = new[] { "TestModerator" },
            ["Улучшение сайта"] = new[] { "TestSeniorMod", PrimaryLogin },
            ["Ошибки"] = new[] { "TestSeniorMod" },
            ["Для новичков"] = new[] { "TestMentor" },
            ["Новости проекта"] = new[] { PrimaryLogin },
        };

        foreach (var (boardTitle, usernames) in assignments)
        {
            var board = boards.FirstOrDefault(b => b.Title == boardTitle);
            if (board == null) continue;

            foreach (var username in usernames)
            {
                var user = users.FirstOrDefault(u =>
                    string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
                if (user == null) continue;

                _dbContext.Set<BoardModerator>().Add(new BoardModerator
                {
                    BoardModeratorId = _guidFactory.Create(),
                    BoardId = board.BoardId,
                    UserId = user.UserId
                });
                result.BoardModeratorsAssigned++;
            }
        }

        result.Details.Add($"Assigned {result.BoardModeratorsAssigned} board moderators");
    }

    private async Task UpdateGlobalChatLastMessage()
    {
        var globalChatId = WellKnownChats.GlobalChatId;
        var globalChat = await _dbContext.Set<Chat>().FirstOrDefaultAsync(c => c.ChatId == globalChatId);
        if (globalChat != null && globalChat.LastMessageId == null)
        {
            var lastMessage = await _dbContext.Set<Message>()
                .Where(m => m.ChatId == globalChatId)
                .OrderByDescending(m => m.CreatedUtc)
                .FirstOrDefaultAsync();

            if (lastMessage != null)
            {
                globalChat.LastMessageId = lastMessage.MessageId;
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}
