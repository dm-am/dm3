using DM.Domain.Community.Features.WebsiteTestimonials;

namespace DM.Web.API.IntegrationTests.Helpers;

internal static class TestimonialEntities
{
    public static CreateWebsiteTestimonialEntity New(Guid authorId, string text) => new()
    {
        Id = Guid.NewGuid(),
        AuthorId = authorId,
        CreatedUtc = DateTime.UtcNow,
        Text = text
    };
}
