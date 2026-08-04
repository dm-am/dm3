using System;
using System.Collections.Generic;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Internal service-layer DTO for user data.
/// </summary>
/// <remarks>
/// Used for passing user data between services internally.
/// NOT exposed via API - use User, UserProfile, or SelfProfile DTOs instead.
/// Maps from User entity via UserReadingRepository.
/// </remarks>
public class GeneralUser : IUser
{
    /// <inheritdoc />
    public Guid UserId { get; set; }

    /// <inheritdoc />
    public string Username { get; set; } = null!;

    /// <summary>
    /// For internal usage only
    /// </summary>
    public string? Email { get; set; }

    /// <inheritdoc />
    public UserRole Role { get; set; }

    /// <inheritdoc />
    public AccessPolicy AccessPolicy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// Avatar variants (original/medium/small). SSOT — a nested object instead of
    /// three flat properties. Populated via
    /// <c>AvatarProjections.From(u.AvatarUpload)</c> in EF Selects.
    /// </summary>
    public AvatarPicture Picture { get; set; } = new();

    /// <summary>
    /// Status
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gender
    /// </summary>
    public Gender Gender { get; set; }

    /// <summary>
    /// Birthday date (full date, year may be 1900 if hidden)
    /// </summary>
    public DateOnly? BirthdayDate { get; set; }

    /// <summary>
    /// Whether to show birthday to other users
    /// </summary>
    public bool ShowBirthday { get; set; } = true;

    /// <inheritdoc />
    public bool RatingDisabled { get; set; }

    /// <inheritdoc />
    public int QualityRating { get; set; }

    /// <inheritdoc />
    public int QuantityRating { get; set; }

    // ---- Aggregates, and null is one of their values ----
    //
    // Everything from here down is filled by a batch of COUNTs the user
    // repository runs after the projection, and by nothing else. A user that
    // arrived nested in another resource — the author of a comment, the people
    // who liked it — has been through no such batch, so the honest answer for
    // these members is "not computed". Hence nullable: as plain ints they
    // defaulted to zero, the API layer copied the zero onto the wire, and a
    // counted zero became indistinguishable from an uncounted one. Read them
    // with an explicit fallback; do not assume the caller hydrated.

    /// <summary>
    /// Number of post reviews given by this user
    /// </summary>
    public int? PostReviewsGivenCount { get; set; }

    /// <summary>
    /// Number of post reviews received by this user (on their posts)
    /// </summary>
    public int? PostReviewsReceivedCount { get; set; }

    /// <summary>
    /// Number of endorsements (user recommendations) received by this user.
    /// Denormalized aggregate populated by UserRepository.GetCommonRelatedData.
    /// </summary>
    public int? EndorsementsReceivedCount { get; set; }

    /// <summary>
    /// Number of endorsements written by this user (about other users).
    /// </summary>
    public int? EndorsementsGivenCount { get; set; }

    /// <summary>
    /// Number of game reviews received by this user: reviews written about the
    /// games they master. A game review is about a game, and the game's master
    /// is who it lands on — the same relation GameReviewFilter already spells
    /// as GmId.
    /// </summary>
    public int? GameReviewsReceivedCount { get; set; }

    /// <summary>
    /// Number of game reviews written by this user (about other people's games
    /// and their own alike).
    /// </summary>
    public int? GameReviewsGivenCount { get; set; }

    /// <summary>
    /// Forum topics authored by this user. Denormalized aggregate
    /// populated by UserRepository.GetCommonRelatedData via batched COUNT.
    /// Drives the <c>TopicsAuthored</c> achievement metric.
    /// </summary>
    public int? TopicsAuthoredCount { get; set; }

    /// <summary>
    /// Comments authored by this user (forum + blog + game + publication —
    /// all polymorphic Comments rows). Drives the <c>CommentsAuthored</c>
    /// achievement metric.
    /// </summary>
    public int? CommentsAuthoredCount { get; set; }

    /// <summary>
    /// Messages this user has posted in the global chat. Drives the
    /// <c>GlobalChatMessages</c> achievement metric.
    /// </summary>
    public int? GlobalChatMessagesCount { get; set; }

    /// <summary>
    /// Bans received by this user (count of <c>Bans</c> rows where this user
    /// is the target). Drives the <c>BansReceived</c> achievement metric —
    /// the "резиновая уточка" chain (an easter egg for the duckling-terrorists meme).
    /// </summary>
    public int? BansReceivedCount { get; set; }

    /// <summary>
    /// Game drops — count of retired characters this user authored where
    /// <c>IsPlayerLeft</c> is true (voluntary exit by the player). Excludes
    /// deaths and GM exiles; those aren't drops. Drives the <c>GameDrops</c>
    /// achievement metric.
    /// </summary>
    public int? GameDropsCount { get; set; }

    /// <summary>
    /// Publications authored — articles in blogs written by this user.
    /// Drives the <c>PublicationsAuthored</c> achievement metric.
    /// Drafts count too (the work was done).
    /// </summary>
    public int? PublicationsAuthoredCount { get; set; }

    /// <summary>
    /// Total likes received on user's authored content (topics +
    /// publications + comments + chat messages). Drives the
    /// <c>LikesReceived</c> achievement metric. Game posts have their
    /// own quality signal (PostReview score sum → "Рейтинг"), so they
    /// are NOT counted here to avoid double-counting recognition.
    /// </summary>
    public int? LikesReceivedCount { get; set; }

    /// <summary>
    /// Registration date (UTC)
    /// </summary>
    public DateTimeOffset? RegisteredUtc { get; set; }

    /// <summary>
    /// Number of games where user is master or assistant
    /// </summary>
    public int? GamesHosting { get; set; }

    /// <summary>
    /// Games hosting breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? GamesHostingByStatus { get; set; }

    /// <summary>
    /// Number of games where user is a player (has active character)
    /// </summary>
    public int? GamesPlaying { get; set; }

    /// <summary>
    /// Games playing breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? GamesPlayingByStatus { get; set; }

    /// <summary>
    /// Number of blogs where user is owner or assistant
    /// </summary>
    public int? BlogsHosting { get; set; }

    /// <summary>
    /// Blogs hosting breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? BlogsHostingByStatus { get; set; }

    /// <summary>
    /// How many subscribers each of the three profile categories really has.
    /// Null when nobody counted them, which is not the same as three zeroes.
    /// </summary>
    public SubscribersByCategory? SubscribersByCategory { get; set; }

    /// <summary>
    /// Subscriber refs for profile-page display: at most
    /// <see cref="SubscriptionPolicy.PreviewCap" />, most recently active first.
    /// </summary>
    /// <remarks>
    /// The cap is taken before the categories are considered, so this is a sample
    /// of the subscribers and not the members of any one line —
    /// <see cref="SubscribersByCategory" /> is what says how many there are.
    /// Null means the preview was never fetched; an empty list means it was and
    /// the user has none.
    /// </remarks>
    public IReadOnlyCollection<SubscriberInfo>? Subscribers { get; set; }

    /// <summary>
    /// Username change history (for tooltip display). Null where the projection
    /// could not order it — the ordering is not translatable inside a ProjectTo,
    /// so only the paths that fetch it separately have one.
    /// </summary>
    public IReadOnlyCollection<UsernameHistoryEntry>? UsernameHistory { get; set; }

    /// <summary>
    /// Whether user is authenticated or not
    /// </summary>
    public bool IsAuthenticated => Role != UserRole.Guest;

    /// <summary>
    /// Whether the user is still a newbie: fewer game posts than
    /// <see cref="ProbationPolicy.NewbiePostThreshold" />. The predicate every
    /// caller asks, over the counter every other reader uses. The rule used to be
    /// written out seven times over two sources - this counter and a COUNT over
    /// posts - so removing a post was enough to make the badge on the profile and
    /// the right to write a review disagree about one person.
    /// </summary>
    public bool IsNewbie => ProbationPolicy.IsNewbie(QuantityRating);
}
