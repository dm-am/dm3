using System;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

public class UpdateWebsiteTestimonialEntity
{
    public Guid Id { get; set; }

    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid ModifiedByUserId { get; set; }

    public string Text { get; set; } = string.Empty;
}
