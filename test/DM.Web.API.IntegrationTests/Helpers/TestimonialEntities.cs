using DM.Domain.Community.Features.WebsiteTestimonials;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>A testimonial to write through the repository, for the suites that drive it directly.</summary>
internal static class TestimonialEntities
{
    /// <summary>A new testimonial by <paramref name="authorId"/>, written now.</summary>
    public static CreateWebsiteTestimonialEntity New(Guid authorId, string text) => new()
    {
        Id = Guid.NewGuid(),
        AuthorId = authorId,
        CreatedUtc = DateTime.UtcNow,
        Text = text
    };
}
