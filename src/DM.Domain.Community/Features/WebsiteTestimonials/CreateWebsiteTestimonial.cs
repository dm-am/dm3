namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <summary>
/// DTO model for website testimonial creation
/// </summary>
public class CreateWebsiteTestimonial
{
    /// <summary>
    /// Testimonial content (plain text, NO BBCode)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
