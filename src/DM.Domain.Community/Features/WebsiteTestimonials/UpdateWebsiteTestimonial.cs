using System;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

public class UpdateWebsiteTestimonial
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;
}
