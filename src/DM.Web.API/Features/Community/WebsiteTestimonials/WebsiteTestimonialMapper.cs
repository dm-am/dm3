using DM.Web.API.Features.Community.Users;
using Riok.Mapperly.Abstractions;
using DomainWebsiteTestimonial = DM.Domain.Community.Features.WebsiteTestimonials.WebsiteTestimonial;
using DomainCreateWebsiteTestimonial = DM.Domain.Community.Features.WebsiteTestimonials.CreateWebsiteTestimonial;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

/// <summary>
/// Compile-time mapper for website testimonials. The author renders through
/// the shared <see cref="UserMapper"/> - single source of truth for avatar
/// URLs.
/// </summary>
[Mapper]
public partial class WebsiteTestimonialMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public WebsiteTestimonialMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain testimonial to its response DTO
    /// </summary>
    public partial WebsiteTestimonialDto ToTestimonial(DomainWebsiteTestimonial testimonial);

    /// <summary>
    /// Create request to the domain command
    /// </summary>
    public partial DomainCreateWebsiteTestimonial ToCreateTestimonial(CreateWebsiteTestimonialRequest request);
}
