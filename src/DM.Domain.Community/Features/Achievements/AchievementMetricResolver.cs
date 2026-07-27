using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// SSOT mapping of <see cref="AchievementMetric"/> → value from the profile.
///
/// A paired <c>getMetricValue</c> exists on the frontend — both functions
/// must change together when a new metric is added. This is
/// a deliberate trade-off: the alternative (computing progress with
/// DB queries) costs N COUNTs per profile view; the already
/// denormalized counters on <c>GeneralUser</c> cover everything needed.
/// </summary>
public static class AchievementMetricResolver
{
    /// <summary>
    /// Get the metric value for a user. <paramref name="now"/>
    /// is only needed for time-based metrics (DaysSinceRegistration);
    /// AchievementService passes <c>_clock.Now</c>. Returns 0
    /// for unknown enum values — a safe fallback that never
    /// triggers a threshold.
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

    /// <summary>Days between registration and the evaluation moment (integer, non-negative).</summary>
    private static int DaysSinceRegistration(GeneralUser user, DateTimeOffset now)
    {
        if (!user.RegisteredUtc.HasValue) return 0;
        var days = (now - user.RegisteredUtc.Value).TotalDays;
        return days < 0 ? 0 : (int)days;
    }
}
