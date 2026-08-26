using System;
using System.Linq;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbWebsiteTestimonial = DM.Infrastructure.Persistence.Entities.Community.WebsiteTestimonial;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <summary>
/// Projection formula for website testimonials: the author renders through
/// the shared <see cref="GeneralUserProjections"/> formula.
/// </summary>
internal static class WebsiteTestimonialMapper
{
    /// <summary>
    /// EF projection to the domain DTO
    /// </summary>
    public static IQueryable<WebsiteTestimonial> ProjectToWebsiteTestimonial(
        this IQueryable<DbWebsiteTestimonial> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbWebsiteTestimonial, WebsiteTestimonial>>(
            t => new WebsiteTestimonial
            {
                Id = t.WebsiteTestimonialId,
                CreatedUtc = t.CreatedUtc,
                ModifiedUtc = t.ModifiedUtc,
                Text = t.Text,
                Author = GeneralUserProjections.Projection.Splice(t.Author)
            }));
}
