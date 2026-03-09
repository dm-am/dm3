namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// Data for creating a platform review
/// </summary>
public class CreatePlatformReview
{
    /// <summary>
    /// Review text
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Author username (optional, for admin-created reviews)
    /// </summary>
    public string? AuthorUsername { get; init; }
}
