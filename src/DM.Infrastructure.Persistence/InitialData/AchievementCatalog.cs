using System;
using DM.Domain.Community.Features.Catalogs;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Community;

namespace DM.Infrastructure.Persistence.InitialData;

/// <summary>
/// The achievement chains the site ships with, a category per metric and the
/// tiers along it, seeded with the schema.
/// </summary>
/// <remarks>
/// A row helper per kind, so a category and a tier each read as one row of the
/// chain. Every category ships active.
/// </remarks>
internal static class AchievementCatalog
{
    /// <summary>The categories, one per metric, in the order the profile shows them.</summary>
    internal static AchievementCategory[] Categories() =>
    [
        Category(0x1, "days_since_registration", "Выслуга лет",
            "Время с момента регистрации на сайте", "hourglass", AchievementMetric.DaysSinceRegistration, 1),
        Category(0x2, "game_posts_authored", "Игровые посты",
            "Игровые посты во всех играх, включая удаленные", "scroll-quill", AchievementMetric.GamePostsAuthored, 2),
        Category(0x3, "post_review_score_sum", "Рейтинг",
            "Сумма оценок игровых постов с учетом минусов", "laurels", AchievementMetric.PostReviewScoreSum, 3),
        Category(0x4, "games_hosted", "Игры в роли ведущего",
            "Игры в роли мастера или ассистента", "scepter", AchievementMetric.GamesHosted, 4),
        Category(0x5, "games_played", "Игры в роли игрока",
            "Игры с активным или бывшим персонажем", "sword", AchievementMetric.GamesPlayed, 5),
        Category(0x6, "blogs_hosted", "Блоги в роли ведущего",
            "Блоги в роли автора или ассистента", "book", AchievementMetric.BlogsHosted, 6),
        Category(0x7, "publications_authored", "Публикации",
            "Статьи в блогах", "papers", AchievementMetric.PublicationsAuthored, 7),
        Category(0x8, "topics_authored", "Топики",
            "Топики, созданные на форуме", "stabbed-note", AchievementMetric.TopicsAuthored, 8),
        Category(0x9, "comments_authored", "Комментарии",
            "Комментарии на форуме, в блогах, играх и публикациях", "discussion", AchievementMetric.CommentsAuthored, 9),
        Category(0xa, "global_chat_messages", "Глобальный чат",
            "Сообщения в глобальном чате", "talk", AchievementMetric.GlobalChatMessages, 10),
        Category(0xb, "likes_received", "Нравится",
            "Отметки Нравится на топиках, публикациях, комментариях и сообщениях чата", "heart-organ", AchievementMetric.LikesReceived, 11),
        Category(0xc, "game_drops", "Дропы",
            "Игры, покинутые добровольно (смерть персонажа и изгнание мастером не считаются)", "walking-boot", AchievementMetric.GameDrops, 12),
        Category(0xd, "bans_received", "Баны",
            "Баны, полученные от модерации", "plastic-duck", AchievementMetric.BansReceived, 13)
    ];

    /// <summary>The tiers, chain by chain.</summary>
    internal static AchievementType[] Types() =>
    [
        Tier(0x5, "DAYS_366", "Поселенец", 366, TierScale.Bronze, 0x1),
        Tier(0x6, "DAYS_1827", "Старожил", 1827, TierScale.Silver, 0x1),
        Tier(0x7, "DAYS_3653", "Ветеран", 3653, TierScale.Gold, 0x1),
        Tier(0x8, "DAYS_5479", "Древний", 5479, TierScale.Platinum, 0x1),

        Tier(0x1, "POSTS_100", "Простые начала", 100, TierScale.Bronze, 0x2),
        Tier(0x2, "POSTS_500", "Продолжение следует", 500, TierScale.Silver, 0x2),
        Tier(0x3, "POSTS_2000", "Долгая партия", 2000, TierScale.Gold, 0x2),
        Tier(0x4, "POSTS_5000", "Приключение в жизнь", 5000, TierScale.Platinum, 0x2),

        Tier(0x9, "RATING_100", "Подающий надежды", 100, TierScale.Bronze, 0x3),
        Tier(0xa, "RATING_250", "Заслуженный мэтр", 250, TierScale.Silver, 0x3),
        Tier(0xb, "RATING_500", "Опытный зубр", 500, TierScale.Gold, 0x3),
        Tier(0xc, "RATING_1000", "Мастодонт ремесла", 1000, TierScale.Platinum, 0x3),

        Tier(0xd, "HOST_3", "Подмастерье", 3, TierScale.Bronze, 0x4),
        Tier(0xe, "HOST_10", "Мастер", 10, TierScale.Silver, 0x4),
        Tier(0xf, "HOST_30", "Грандмастер", 30, TierScale.Gold, 0x4),
        Tier(0x10, "HOST_100", "Архитектор миров", 100, TierScale.Platinum, 0x4),

        Tier(0x11, "PLAY_5", "Искатель", 5, TierScale.Bronze, 0x5),
        Tier(0x12, "PLAY_20", "Авантюрист", 20, TierScale.Silver, 0x5),
        Tier(0x13, "PLAY_100", "Герой", 100, TierScale.Gold, 0x5),
        Tier(0x14, "PLAY_500", "Легенда", 500, TierScale.Platinum, 0x5),

        Tier(0x15, "BLOGS_1", "Свежий взгляд", 1, TierScale.Bronze, 0x6),
        Tier(0x16, "BLOGS_5", "Небольшая подборка", 5, TierScale.Silver, 0x6),
        Tier(0x17, "BLOGS_15", "Именная коллекция", 15, TierScale.Gold, 0x6),
        Tier(0x18, "BLOGS_50", "Великая библиотека", 50, TierScale.Platinum, 0x6),

        Tier(0x2d, "PUBS_5", "Проба пера", 5, TierScale.Bronze, 0x7),
        Tier(0x2e, "PUBS_25", "Мысли вслух", 25, TierScale.Silver, 0x7),
        Tier(0x2f, "PUBS_100", "Постоянная рубрика", 100, TierScale.Gold, 0x7),
        Tier(0x30, "PUBS_500", "Без строчки ни дня", 500, TierScale.Platinum, 0x7),

        Tier(0x19, "TOPICS_5", "Повод для обсуждения", 5, TierScale.Bronze, 0x8),
        Tier(0x1a, "TOPICS_25", "Занятные темы", 25, TierScale.Silver, 0x8),
        Tier(0x1b, "TOPICS_100", "Дневная повестка", 100, TierScale.Gold, 0x8),
        Tier(0x1c, "TOPICS_500", "На целый раздел", 500, TierScale.Platinum, 0x8),

        Tier(0x1d, "COMMENTS_100", "Свои пять копеек", 100, TierScale.Bronze, 0x9),
        Tier(0x1e, "COMMENTS_500", "Живое участие", 500, TierScale.Silver, 0x9),
        Tier(0x1f, "COMMENTS_2000", "В гуще событий", 2000, TierScale.Gold, 0x9),
        Tier(0x20, "COMMENTS_10000", "Всегда есть что сказать", 10000, TierScale.Platinum, 0x9),

        Tier(0x21, "CHAT_500", "Прохожий", 500, TierScale.Bronze, 0xa),
        Tier(0x22, "CHAT_5000", "Свой человек", 5000, TierScale.Silver, 0xa),
        Tier(0x23, "CHAT_25000", "Чат-завсегдатай", 25000, TierScale.Gold, 0xa),
        Tier(0x24, "CHAT_100000", "Вечный онлайн", 100000, TierScale.Platinum, 0xa),

        Tier(0x31, "LIKES_25", "В узких кругах", 25, TierScale.Bronze, 0xb),
        Tier(0x32, "LIKES_100", "Душа компании", 100, TierScale.Silver, 0xb),
        Tier(0x33, "LIKES_500", "Народный любимец", 500, TierScale.Gold, 0xb),
        Tier(0x34, "LIKES_2000", "Первый в сердцах", 2000, TierScale.Platinum, 0xb),

        Tier(0x29, "DROPS_1", "Покинувший строй", 1, TierScale.Bronze, 0xc),
        Tier(0x2a, "DROPS_3", "Беглец", 3, TierScale.Silver, 0xc),
        Tier(0x2b, "DROPS_10", "Дезертир", 10, TierScale.Gold, 0xc),
        Tier(0x2c, "DROPS_30", "Пропавший без вести", 30, TierScale.Platinum, 0xc),

        Tier(0x25, "BANS_1", "Выпавший из гнезда", 1, TierScale.Bronze, 0xd),
        Tier(0x26, "BANS_3", "Крякнувший лишнего", 3, TierScale.Silver, 0xd),
        Tier(0x27, "BANS_10", "Утенок-террорист", 10, TierScale.Gold, 0xd),
        Tier(0x28, "BANS_30", "Селезень-Рецидивист", 30, TierScale.Platinum, 0xd)
    ];

    private static AchievementCategory Category(
        int id, string code, string title, string description, string iconName, AchievementMetric metric, int sortOrder) => new()
        {
            AchievementCategoryId = CategoryId(id),
            Code = code,
            Title = title,
            Description = description,
            IconName = iconName,
            Metric = metric,
            SortOrder = sortOrder,
            IsActive = true
        };

    private static AchievementType Tier(int id, string code, string title, int threshold, int tier, int category) => new()
    {
        AchievementTypeId = new Guid($"00000000-0000-0000-0002-{id:x12}"),
        Code = code,
        Title = title,
        Threshold = threshold,
        Tier = tier,
        AchievementCategoryId = CategoryId(category)
    };

    private static Guid CategoryId(int number) => new($"00000000-0000-0000-0003-{number:x12}");
}
