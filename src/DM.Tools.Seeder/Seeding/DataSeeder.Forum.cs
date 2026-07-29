using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    private async Task CreateForumContent(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Well-known IDs for system topics created in migration (must be excluded from count)
        var systemTopicIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"), // "Отзывы о ДМ"
            Guid.Parse("00000000-0000-0000-0000-000000000100"), // "Обсуждение действий администрации"
        };

        // Check if user-created topics already exist. System topics don't
        // count: the fixed migration-seeded ones and the auto-created period
        // digests (PeriodDigestService starts creating those right at app
        // start, before this seed can run).
        var existingTopicsCount = await _dbContext.Set<Topic>()
            .CountAsync(t => !systemTopicIds.Contains(t.TopicId)
                && !_dbContext.PeriodDigestTopics.Any(p => p.TopicId == t.TopicId));
        if (existingTopicsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Forum topics already exist ({existingTopicsCount}), skipping");
            return;
        }

        var boards = await _dbContext.Set<Board>().OrderBy(b => b.Order).ToListAsync();
        if (boards.Count == 0)
        {
            result.Details.Add("No boards found, skipping forum content");
            return;
        }

        // Board-specific topic templates (keyed by board title)
        var boardTopics = new Dictionary<string, (string Title, string Text)[]>
        {
            ["Общий"] = new[]
            {
                ("Добро пожаловать на форум!", "Здесь вы можете обсудить все, что связано с текстовыми ролевыми играми. Соблюдайте правила и уважайте друг друга!"),
                ("Правила форума", "Основные правила: 1) Уважайте собеседников. 2) Не флудите. 3) Пишите по теме. 4) Не рекламируйте."),
                // Note: "Обсуждение действий администрации" is seeded in InitialCreate migration
                ("История банов — январь 2026", "Отчет о нарушителях за прошлый месяц. Всего выдано 3 предупреждения и 1 бан за спам."),
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
                ("Конкурс рассказов — весна 2026", "Объявляем ежегодный конкурс рассказов! Тема: 'Новые горизонты'. Дедлайн: 1 апреля."),
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
                ("FAQ для новых пользователей", "Собрал ответы на частые вопросы. Читайте, прежде чем создавать тему!"),
            },
            ["Новости проекта"] = new[]
            {
                ("Запуск новой версии Dungeon Master!", """
Рады представить полностью переписанную версию сайта Dungeon Master!

[b]Что нового:[/b]
[ul][li]Полностью новый дизайн — современный, чистый, адаптивный[/li][li]Улучшенный редактор постов с поддержкой BBCode и предпросмотром[/li][li]Система фильтров игр — ищите по жанрам, тегам, статусу набора[/li][li]Оптимизированная производительность — страницы загружаются в 3 раза быстрее[/li][li]Темная тема для ночных сов[/li][/ul]
[b]Для мастеров:[/b]
[ul][li]Новая панель управления игрой[/li][li]Улучшенное управление персонажами и комнатами[/li][li]Система приглашений игроков[/li][/ul]
Мы работали над этим обновлением больше года и надеемся, что вам понравится! Если найдете баги — пишите в раздел "Ошибки".
"""),
                ("Обновление март 2026: фильтры и теги", """
Большое обновление системы поиска игр!

[b]Теги игр[/b]
Теперь каждая игра может иметь теги — жанры, сеттинги, особенности. Мастера могут добавлять до 10 тегов к своей игре. Облако тегов в сайдбаре показывает популярные теги.

[b]Расширенные фильтры[/b]
[ul][li]Фильтр по статусу: активные, на наборе, завершенные[/li][li]Фильтр по тегам: включить или исключить[/li][li]Сортировка: по активности, популярности, дате создания[/li][li]Поиск по названию и описанию[/li][/ul]
[b]Как пользоваться:[/b]
1. Зайдите на страницу "Игры"
2. Используйте панель фильтров слева
3. Выбирайте теги кликом
4. Результаты обновляются мгновенно

Теперь найти идеальную игру для себя стало намного проще. Не забудьте добавить теги к своим играм!
"""),
                ("Планы на весну 2026", """
Делимся планами на ближайшие месяцы!

[b]Апрель 2026[/b]
[ul][li]Мобильная версия сайта — полностью адаптивный дизайн для телефонов[/li][li]Push-уведомления в браузере о новых постах[/li][/ul]
[b]Май 2026[/b]
[ul][li]Интеграция с Discord — бот для уведомлений о событиях в играх[/li][li]Система достижений для игроков и мастеров[/li][/ul]
[b]Июнь 2026[/b]
[ul][li]Улучшенный блог — расширенные возможности форматирования[/li][li]Галерея изображений для игр[/li][/ul]
[b]В разработке:[/b]
[ul][li]Система рекомендаций игр на основе ваших предпочтений[/li][li]Календарь событий для игр[/li][li]Экспорт истории игры в PDF[/li][/ul]
Следите за новостями! Если у вас есть предложения — пишите в раздел "Улучшение сайта".
"""),
            },
        };

        var commentTemplates = new[]
        {
            "Отличная тема, поддерживаю!",
            "Согласен с автором на 100%.",
            "Интересная мысль, но я бы добавил...",
            "Спасибо за информацию!",
            "У меня был похожий опыт.",
            "Не согласен, но уважаю мнение.",
            "Кто-нибудь уже пробовал это?",
            "Жду продолжения обсуждения.",
            "+1 к этому предложению",
            "Хорошая инициатива!",
        };

        // Get max TopicNumber per board to avoid conflicts with existing topics (like system topic)
        var maxTopicNumbers = await _dbContext.Set<Topic>()
            .GroupBy(t => t.BoardId)
            .Select(g => new { BoardId = g.Key, MaxNumber = g.Max(t => t.TopicNumber) })
            .ToDictionaryAsync(x => x.BoardId, x => x.MaxNumber);

        foreach (var board in boards)
        {
            // Get topics for this board, or use generic ones
            var topics = boardTopics.GetValueOrDefault(board.Title) ?? new[]
            {
                ("Обсуждение", "Общая тема для обсуждения."),
                ("Вопросы и ответы", "Задавайте вопросы здесь."),
            };

            // Check if regular users can post
            var canPost = (board.CreateTopicPolicy & (BoardAccessPolicy.RegularUser | BoardAccessPolicy.Guest)) != BoardAccessPolicy.None;

            // Start numbering after any existing topics in this board
            var startNumber = maxTopicNumbers.GetValueOrDefault(board.BoardId, 0);

            for (var i = 0; i < topics.Length; i++)
            {
                var template = topics[i];
                var author = canPost
                    ? users[Random.Shared.Next(users.Count)]
                    : users.First(u => u.Role >= UserRole.Mentor);

                var topicId = _guidFactory.Create();

                // News topics: first 2 are recent (within last week), rest are older
                var topicAge = board.Title == "Новости проекта" && i < 2
                    ? Random.Shared.Next(1, 7) // 1-6 days ago for recent news
                    : Random.Shared.Next(7, 30); // 7-29 days ago for older content

                var topic = new Topic
                {
                    TopicId = topicId,
                    BoardId = board.BoardId,
                    AuthorId = author.UserId,
                    TopicNumber = startNumber + i + 1, // Unique within board, starting after existing topics
                    CreatedUtc = now.AddDays(-topicAge),
                    Title = template.Title,
                    Text = template.Text,
                    IsAttached = i == 0 && board.Title == "Новости проекта", // Pin first topic in News
                    IsClosed = false,
                    IsRemoved = false
                };

                _dbContext.Set<Topic>().Add(topic);
                result.TopicsCreated++;

                // Add comments (track them to update LastCommentId later)
                // FAQ topic gets 50 comments for pagination testing
                var commentsToCreate = template.Title == "FAQ для новых пользователей" ? 50 : Random.Shared.Next(3, 8);
                DbComment? lastComment = null;

                // Extended comment templates for FAQ topic
                var faqCommentTemplates = new[]
                {
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
                    "Темная тема — это супер!",
                    "Как посмотреть свою статистику?",
                    "Подскажите, как работает система тегов.",
                    "Можно ли экспортировать свои посты?",
                    "Как добавить друга в игру?",
                    "Что означает значок рядом с ником?",
                    "Спасибо за FAQ, очень полезно!",
                    "Как отредактировать старый пост?",
                    "Есть ли лимит на длину сообщения?",
                    "Когда обновят интерфейс?",
                    "Как создать опрос в теме?",
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
                };

                for (var j = 0; j < commentsToCreate; j++)
                {
                    var commentAuthor = users[Random.Shared.Next(users.Count)];
                    var commentText = template.Title == "FAQ для новых пользователей"
                        ? faqCommentTemplates[j % faqCommentTemplates.Length]
                        : commentTemplates[Random.Shared.Next(commentTemplates.Length)];

                    var comment = new DbComment
                    {
                        CommentId = _guidFactory.Create(),
                        EntityId = topic.TopicId,
                        AuthorId = commentAuthor.UserId,
                        CreatedUtc = topic.CreatedUtc.AddHours(j * 12 + Random.Shared.Next(1, 12)),
                        Text = commentText,
                        IsRemoved = false
                    };

                    _dbContext.Set<DbComment>().Add(comment);
                    result.CommentsCreated++;
                    lastComment = comment;
                }

                // Update board stats
                board.TopicsCount++;
            }
        }

        // Save topics and comments first to avoid circular dependency
        await _dbContext.SaveChangesAsync();

        // Now update LastCommentId references for topics that have comments
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

                // Update board
                var board = boards.FirstOrDefault(b => b.BoardId == topic.BoardId);
                if (board != null)
                {
                }
            }
        }

        result.Details.Add($"Created {result.TopicsCreated} topics with {result.CommentsCreated} comments");
    }

    private async Task AssignBoardModerators(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Check if moderators already assigned
        var existingModeratorsCount = await _dbContext.Set<BoardModerator>().CountAsync();
        if (existingModeratorsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Board moderators already assigned ({existingModeratorsCount}), skipping");
            return;
        }

        var boards = await _dbContext.Set<Board>().ToListAsync();
        if (boards.Count == 0)
        {
            result.Details.Add("No boards found, skipping moderator assignment");
            return;
        }

        // Get users who can be moderators (Moderator+ role)
        var moderatorUsers = users
            .Where(u => u.Role >= UserRole.Moderator)
            .ToList();

        if (moderatorUsers.Count == 0)
        {
            result.Details.Add("No moderator users found, skipping board moderator assignment");
            return;
        }

        // Board moderator assignments (realistic distribution)
        // All boards should have at least one moderator assigned
        var assignments = new Dictionary<string, string[]>
        {
            ["Общий"] = new[] { "TestModerator", "TestSeniorMod" },
            ["Игровые системы"] = new[] { "TestModerator" },
            ["Поиск мастера и игроков"] = new[] { "TestModerator", "TestSeniorMod" },
            ["Котел идей"] = new[] { "TestModerator" },
            ["Конкурсы"] = new[] { "TestSeniorMod" },
            ["Под столом"] = new[] { "TestModerator", "TestMentor" },
            ["Неролевые игры"] = new[] { "TestModerator" },
            ["Улучшение сайта"] = new[] { "TestSeniorMod", "SolohinLex" },
            ["Ошибки"] = new[] { "TestSeniorMod" },
            ["Для новичков"] = new[] { "TestMentor" },
            ["Новости проекта"] = new[] { "SolohinLex" },
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

                // Check if already assigned
                var exists = await _dbContext.Set<BoardModerator>()
                    .AnyAsync(bm => bm.BoardId == board.BoardId && bm.UserId == user.UserId);
                if (exists) continue;

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

    private async Task UpdateLastCommentReferences()
    {
        // NOTE: Games and Publications comments are skipped because Comment.EntityId has FK to Topics only
        // Only Topics have comments in the current schema

        // Update Chats with messages (Global Chat)
        var globalChatId = Chat.GlobalChatId;
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
