namespace DM.Services.Game.Dto.Output;

/// <summary>
/// Response envelope for featured posts
/// </summary>
public class FeaturedPostsEnvelope
{
    /// <summary>
    /// Best post of the week (highest rating sum in last 7 days)
    /// </summary>
    public FeaturedPost? BestOfWeek { get; set; }

    /// <summary>
    /// Last post that received a positive review (+1)
    /// </summary>
    public FeaturedPost? LastWithPlus { get; set; }
}
