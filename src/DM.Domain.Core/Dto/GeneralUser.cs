using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Number of post reviews given by this user
    /// </summary>
    public int PostReviewsGivenCount { get; set; }

    /// <summary>
    /// Number of post reviews received by this user (on their posts)
    /// </summary>
    public int PostReviewsReceivedCount { get; set; }

    /// <summary>
    /// Number of endorsements (user recommendations) received by this user.
    /// Denormalized aggregate populated by UserRepository.GetCommonRelatedData.
    /// </summary>
    public int EndorsementsReceivedCount { get; set; }

    /// <summary>
    /// Number of endorsements written by this user (about other users).
    /// </summary>
    public int EndorsementsGivenCount { get; set; }

    /// <summary>
    /// Forum topics authored by this user. Denormalized aggregate
    /// populated by UserRepository.GetCommonRelatedData via batched COUNT.
    /// Drives the <c>TopicsAuthored</c> achievement metric.
    /// </summary>
    public int TopicsAuthoredCount { get; set; }

    /// <summary>
    /// Comments authored by this user (forum + blog + game + publication —
    /// all polymorphic Comments rows). Drives the <c>CommentsAuthored</c>
    /// achievement metric.
    /// </summary>
    public int CommentsAuthoredCount { get; set; }

    /// <summary>
    /// Messages this user has posted in the global chat. Drives the
    /// <c>GlobalChatMessages</c> achievement metric.
    /// </summary>
    public int GlobalChatMessagesCount { get; set; }

    /// <summary>
    /// Bans received by this user (count of <c>Bans</c> rows where this user
    /// is the target). Drives the <c>BansReceived</c> achievement metric —
    /// the "резиновая уточка" chain (an easter egg for the duckling-terrorists meme).
    /// </summary>
    public int BansReceivedCount { get; set; }

    /// <summary>
    /// Game drops — count of retired characters this user authored where
    /// <c>IsPlayerLeft</c> is true (voluntary exit by the player). Excludes
    /// deaths and GM exiles; those aren't drops. Drives the <c>GameDrops</c>
    /// achievement metric.
    /// </summary>
    public int GameDropsCount { get; set; }

    /// <summary>
    /// Publications authored — articles in blogs written by this user.
    /// Drives the <c>PublicationsAuthored</c> achievement metric.
    /// Drafts count too (the work was done).
    /// </summary>
    public int PublicationsAuthoredCount { get; set; }

    /// <summary>
    /// Total likes received on user's authored content (topics +
    /// publications + comments + chat messages). Drives the
    /// <c>LikesReceived</c> achievement metric. Game posts have their
    /// own quality signal (PostReview score sum → "Рейтинг"), so they
    /// are NOT counted here to avoid double-counting recognition.
    /// </summary>
    public int LikesReceivedCount { get; set; }

    /// <summary>
    /// Registration date (UTC)
    /// </summary>
    public DateTimeOffset? RegisteredUtc { get; set; }

    /// <summary>
    /// Number of games where user is master or assistant
    /// </summary>
    public int GamesHosting { get; set; }

    /// <summary>
    /// Games hosting breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? GamesHostingByStatus { get; set; }

    /// <summary>
    /// Number of games where user is a player (has active character)
    /// </summary>
    public int GamesPlaying { get; set; }

    /// <summary>
    /// Games playing breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? GamesPlayingByStatus { get; set; }

    /// <summary>
    /// Number of blogs where user is owner or assistant
    /// </summary>
    public int BlogsHosting { get; set; }

    /// <summary>
    /// Blogs hosting breakdown by status (for tooltips)
    /// </summary>
    public ModuleStatusCounts? BlogsHostingByStatus { get; set; }

    /// <summary>
    /// Number of subscribers following this user
    /// </summary>
    public int SubscribersCount { get; set; }

    /// <summary>
    /// Subscriber usernames for tooltip display (limited to first 20)
    /// </summary>
    public IReadOnlyCollection<string> SubscriberUsernames { get; set; } = [];

    /// <summary>
    /// Richer subscriber refs (username + last activity) for profile-page
    /// display where the UI styles inactive subscribers differently.
    /// Limited to first 20 — same budget as <see cref="SubscriberUsernames"/>.
    /// </summary>
    public IReadOnlyCollection<SubscriberInfo> Subscribers { get; set; } = [];

    /// <summary>
    /// Username change history (for tooltip display)
    /// </summary>
    public IReadOnlyCollection<UsernameHistoryEntry> UsernameHistory { get; set; } = Array.Empty<UsernameHistoryEntry>();

    /// <summary>
    /// Whether user is authenticated or not
    /// </summary>
    public bool IsAuthenticated => Role != UserRole.Guest;

    /// <summary>
    /// Whether user is a newbie (less than 100 posts)
    /// </summary>
    public bool IsNewbie => QuantityRating < 100;
}
