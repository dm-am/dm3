namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Review sign/sentiment for post reviews (formerly VoteSign)
/// </summary>
public enum ReviewSign
{
    /// <summary>
    /// Negative review (user did not like the post)
    /// </summary>
    Negative = -1,

    /// <summary>
    /// Neutral review (comment without rating impact)
    /// </summary>
    Neutral = 0,

    /// <summary>
    /// Positive review (user liked the post)
    /// </summary>
    Positive = 1
}
