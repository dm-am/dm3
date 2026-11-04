using System;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

public class CreateWebsiteTestimonialEntity
{
    public Guid Id { get; set; }

    public Guid AuthorId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Text { get; set; } = string.Empty;
}
