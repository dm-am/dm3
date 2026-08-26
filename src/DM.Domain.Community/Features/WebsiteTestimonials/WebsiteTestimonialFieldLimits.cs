namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// How long a testimonial text may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class WebsiteTestimonialFieldLimits
{
    /// <summary>Longest testimonial text.</summary>
    public const int TextMaxLength = 10000;
}
