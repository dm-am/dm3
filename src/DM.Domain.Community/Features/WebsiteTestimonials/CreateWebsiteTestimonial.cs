namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// DTO model for website testimonial creation
/// </summary>
public class CreateWebsiteTestimonial
{
    /// <summary>
    /// Username of the participant the testimonial is signed by
    /// </summary>
    /// <remarks>
    /// A testimonial is never written by the person who submits it: only senior
    /// moderation may create one, and it is posted on behalf of the participant
    /// named here. Without this field the entry was signed by the moderator who
    /// typed it, which is a claim about the wrong person.
    /// </remarks>
    public string AuthorUsername { get; set; } = string.Empty;

    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
