using System;
using DM.Domain.Community.Features.Catalogs;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Community;

namespace DM.Infrastructure.Persistence.InitialData;

internal static class AwardCatalog
{
    internal static AwardType[] AwardTypes() =>
    [
        Award(1, "contest_first", "Литконкурс", "Первое место в конкурсе", "trophy-cup", TierScale.Gold),
        Award(2, "contest_second", "Литконкурс", "Второе место в конкурсе", "trophy-cup", TierScale.Silver),
        Award(3, "contest_third", "Литконкурс", "Третье место в конкурсе", "trophy-cup", TierScale.Bronze),
        Award(4, "popular_vote", "Народное признание, например",
            "Лучшая работа по голосованию участников", "ribbon-medal", TierScale.Gold),
        Award(5, "best_critic", "Лучший критик", "Лучшие рецензии конкурса по решению жюри", "quill-ink", TierScale.Gold),
        Award(6, "guesser", "Угадайка", "Больше всех угаданных авторов конкурсных работ", "magnifying-glass", TierScale.Gold),
        Award(7, "honorary_goblin", "Почетный гоблин", "Годы службы сообществу в команде гоблинов", "goblin", TierScale.Platinum)
    ];

    internal static ContestSeries[] ContestSeries() =>
    [
        Series(1, ContestType.Literary, 23, 2026, "/forum/contests/13232"),
        Series(2, ContestType.Literary, 22, 2026, "/forum/contests/13089"),
        Series(3, ContestType.Literary, 21, 2025, "/forum/contests/12926"),
        Series(4, ContestType.Literary, 20, 2025, "/forum/contests/12729"),
        Series(5, ContestType.Art, 2, 2016, "/forum/contests/6397"),
        Series(6, ContestType.Art, 1, 2013, "/forum/contests/3764")
    ];

    // One award type; its place in the profile is its number.
    private static AwardType Award(
        int number, string code, string title, string description, string iconName, int tier) => new()
        {
            AwardTypeId = new Guid($"00000000-0000-0000-0001-{number:x12}"),
            Code = code,
            Title = title,
            Description = description,
            IconName = iconName,
            Tier = tier,
            SortOrder = number,
            IsActive = true
        };

    private static ContestSeries Series(int number, ContestType type, int contestNumber, int year, string topicUrl) => new()
    {
        ContestSeriesId = new Guid($"00000000-0000-0000-0004-{number:x12}"),
        ContestType = type,
        Number = contestNumber,
        Year = year,
        TopicUrl = topicUrl,
        IsActive = true
    };
}
