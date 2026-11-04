namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// How long a testimonial text may be.
/// </summary>
/// <remarks>
/// As long as the text of a ticket: a testimonial is shown whole, never cut, so
/// the bound only keeps one entry from filling the page. The client keeps a copy
/// for its counter (TESTIMONIAL_TEXT_MAX_LENGTH), held to this one by
/// ClientFieldLimitsShould.
/// </remarks>
public static class WebsiteTestimonialFieldLimits
{
    public const int TextMaxLength = 10000;

    /// <summary>
    /// The refusal of a length out of bounds; StringLength puts the maximum in {1}
    /// and the minimum in {2}, so the numbers are the bounds themselves.
    /// </summary>
    public const string LengthRefusal = "Отзыв от {2} до {1} символов";
}
