namespace DM.Services.Community.BusinessProcesses.Reviews;

/// <summary>
/// List of review actions that require authorization
/// </summary>
public enum ReviewIntention
{
    /// <summary>
    /// Create new platform review
    /// </summary>
    Create = 0,

    /// <summary>
    /// Edit existing platform review
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Approve platform review
    /// </summary>
    Approve = 2,

    /// <summary>
    /// Delete platform review
    /// </summary>
    Delete = 3,

    /// <summary>
    /// Read not approved platform reviews
    /// </summary>
    ReadUnapproved = 4,

    /// <summary>
    /// Create user review (requires having played together)
    /// </summary>
    CreateUserReview = 5,

    /// <summary>
    /// Create game review (requires valid character in game)
    /// </summary>
    CreateGameReview = 6,

    /// <summary>
    /// Edit user review (author only)
    /// </summary>
    EditUserReview = 7,

    /// <summary>
    /// Edit game review (author only)
    /// </summary>
    EditGameReview = 8,

    /// <summary>
    /// Delete user review (author or moderator)
    /// </summary>
    DeleteUserReview = 9,

    /// <summary>
    /// Delete game review (author or moderator)
    /// </summary>
    DeleteGameReview = 10,

    /// <summary>
    /// Create post review (rating)
    /// </summary>
    CreatePostReview = 11,

    /// <summary>
    /// Delete post review (author or moderator)
    /// </summary>
    DeletePostReview = 12
}