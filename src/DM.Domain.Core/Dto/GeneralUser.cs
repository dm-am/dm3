using System;
using DM.Domain.Core.Enums;

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

    /// <summary>
    /// Honorary goblin status (special title for active users)
    /// </summary>
    public bool IsHonorary { get; set; }

    /// <inheritdoc />
    public AccessPolicy AccessPolicy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// URL of current profile picture original file
    /// </summary>
    public string? OriginalPictureUrl { get; set; }

    /// <summary>
    /// URL of current profile picture M-sized
    /// </summary>
    public string? MediumPictureUrl { get; set; }

    /// <summary>
    /// URL of current profile picture S-sized
    /// </summary>
    public string? SmallPictureUrl { get; set; }

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
    /// Whether user is authenticated or not
    /// </summary>
    public bool IsAuthenticated => Role != UserRole.Guest;

    /// <summary>
    /// Whether user is a newbie (less than 100 posts)
    /// </summary>
    public bool IsNewbie => QuantityRating < 100;
}
