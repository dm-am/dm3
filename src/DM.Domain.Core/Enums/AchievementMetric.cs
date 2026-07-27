namespace DM.Domain.Core.Enums;

/// <summary>
/// Metric used to evaluate achievement progress.
/// Each value maps unambiguously to a single field in the user
/// profile (see <c>AchievementMetricResolver</c> in Domain.Community).
/// A new metric = a new enum value + a new case in the resolver + (if
/// needed) a new denormalized field on <c>GeneralUser</c> +
/// a paired case in the FE <c>getMetricValue</c>.
/// </summary>
public enum AchievementMetric
{
    /// <summary>Number of game posts (<c>QuantityRating</c>).</summary>
    GamePostsAuthored = 1,

    /// <summary>Days since registration (now − <c>RegisteredUtc</c>).</summary>
    DaysSinceRegistration = 2,

    /// <summary>Sum of rating points from received PostReviews (<c>QualityRating</c>).</summary>
    PostReviewScoreSum = 3,

    /// <summary>Games where the user is a master or an assistant (<c>GamesHosting</c>).</summary>
    GamesHosted = 4,

    /// <summary>Games where the user is a player (<c>GamesPlaying</c>).</summary>
    GamesPlayed = 5,

    /// <summary>Blogs where the user is an author or an assistant (<c>BlogsHosting</c>).</summary>
    BlogsHosted = 6,

    /// <summary>Number of forum topics created by the user.</summary>
    TopicsAuthored = 7,

    /// <summary>Number of comments left (of any type).</summary>
    CommentsAuthored = 8,

    /// <summary>Number of messages in the global chat.</summary>
    GlobalChatMessages = 9,

    /// <summary>Number of received bans (count(Bans) where TargetUser=user).</summary>
    BansReceived = 10,

    /// <summary>
    /// Number of drops — games the user left voluntarily
    /// (count(Characters) where AuthorId=user AND Status=Retired AND IsPlayerLeft=true).
    /// Exile and death are not drops — those are GM-initiated or in-game events.
    /// </summary>
    GameDrops = 11,

    /// <summary>
    /// Number of publications — blog articles written by the user
    /// (count(Publications) where AuthorId=user AND !IsRemoved).
    /// Drafts (IsPublished=false) also count — the user did the
    /// work even without clicking "Опубликовать".
    /// </summary>
    PublicationsAuthored = 12,

    /// <summary>
    /// Total number of "likes" the user received on their
    /// content: topics, publications, comments, chat messages. Game
    /// posts (rated via PostReviews) and PostReviews themselves are not
    /// included — posts have their own "Рейтинг" chain.
    /// Soft-deleted likes and deleted content are excluded.
    /// </summary>
    LikesReceived = 13,
}
