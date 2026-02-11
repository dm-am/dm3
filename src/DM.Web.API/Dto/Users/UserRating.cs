namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user rating information
/// </summary>
public class UserRating
{
    /// <summary>
    /// Whether the user participates in rating system
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Total quality rating (sum of positive/negative reviews received)
    /// </summary>
    public int TotalRating { get; set; }

    /// <summary>
    /// Total posts count (quantity rating)
    /// </summary>
    public int TotalPosts { get; set; }

    /// <summary>
    /// Total post reviews given by the user
    /// </summary>
    public int TotalPostReviewsGiven { get; set; }
}
