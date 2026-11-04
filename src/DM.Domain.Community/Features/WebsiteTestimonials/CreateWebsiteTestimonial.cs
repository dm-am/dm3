namespace DM.Domain.Community.Features.WebsiteTestimonials;

public class CreateWebsiteTestimonial
{
    /// <summary>
    /// Username of the participant the testimonial is signed by
    /// </summary>
    /// <remarks>
    /// Senior moderation posts a testimonial on behalf of the participant named
    /// here, so the entry is not signed by the moderator who typed it.
    /// </remarks>
    public string AuthorUsername { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
