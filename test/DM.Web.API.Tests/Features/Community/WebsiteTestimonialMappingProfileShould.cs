using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Community.WebsiteTestimonials;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class WebsiteTestimonialMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<WebsiteTestimonialMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
