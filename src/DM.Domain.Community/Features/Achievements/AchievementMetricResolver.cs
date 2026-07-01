using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// SSOT-маппинг <see cref="AchievementMetric"/> → значение из профиля.
///
/// Парный <c>getMetricValue</c> существует на фронтенде — обе функции
/// должны меняться вместе при добавлении новой метрики. Это
/// сознательный компромисс: альтернатива (вычислять прогресс ходом
/// запросов из БД) дает N COUNT'ов на просмотр профиля; уже
/// денормализованные счетчики на <c>GeneralUser</c> покрывают все нужное.
/// </summary>
public static class AchievementMetricResolver
{
    /// <summary>
    /// Достать значение метрики для пользователя. <paramref name="now"/>
    /// нужен только для time-based метрик (DaysSinceRegistration);
    /// AchievementService прокидывает <c>_clock.Now</c>. Возвращает 0
    /// для неизвестных значений enum — безопасный фолбэк, никогда не
    /// триггерит порог.
    /// </summary>
    public static int GetValue(AchievementMetric metric, GeneralUser user, DateTimeOffset now)
    {
        return metric switch
        {
            AchievementMetric.GamePostsAuthored      => user.QuantityRating,
            AchievementMetric.DaysSinceRegistration  => DaysSinceRegistration(user, now),
            AchievementMetric.PostReviewScoreSum     => Math.Max(0, user.QualityRating),
            AchievementMetric.GamesHosted            => user.GamesHosting,
            AchievementMetric.GamesPlayed            => user.GamesPlaying,
            AchievementMetric.BlogsHosted            => user.BlogsHosting,
            AchievementMetric.TopicsAuthored         => user.TopicsAuthoredCount,
            AchievementMetric.CommentsAuthored       => user.CommentsAuthoredCount,
            AchievementMetric.GlobalChatMessages     => user.GlobalChatMessagesCount,
            AchievementMetric.BansReceived           => user.BansReceivedCount,
            AchievementMetric.GameDrops              => user.GameDropsCount,
            AchievementMetric.PublicationsAuthored   => user.PublicationsAuthoredCount,
            AchievementMetric.LikesReceived          => user.LikesReceivedCount,
            _                                        => 0,
        };
    }

    /// <summary>Дни между регистрацией и моментом проверки (целое, не отриц.).</summary>
    private static int DaysSinceRegistration(GeneralUser user, DateTimeOffset now)
    {
        if (!user.RegisteredUtc.HasValue) return 0;
        var days = (now - user.RegisteredUtc.Value).TotalDays;
        return days < 0 ? 0 : (int)days;
    }
}
