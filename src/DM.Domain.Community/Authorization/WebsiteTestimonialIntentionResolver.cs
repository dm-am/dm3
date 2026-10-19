using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Authorization;

/// <inheritdoc />
internal class WebsiteTestimonialIntentionResolver :
    IIntentionResolver<WebsiteTestimonialIntention>,
    IIntentionResolver<WebsiteTestimonialIntention, WebsiteTestimonial>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, WebsiteTestimonialIntention intention) => intention switch
    {
        // Moderation creates testimonials on behalf of users
        WebsiteTestimonialIntention.Create => user.Role >= UserRole.SeniorModerator,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, WebsiteTestimonialIntention intention, WebsiteTestimonial testimonial) =>
        intention switch
        {
            WebsiteTestimonialIntention.Edit =>
                user.UserId == testimonial.Author.UserId || user.Role >= UserRole.SeniorModerator,
            WebsiteTestimonialIntention.Delete => user.Role >= UserRole.SeniorModerator,
            _ => false
        };
}
