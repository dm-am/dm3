using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Profiles;
using Xunit;

namespace DM.Web.API.Tests.Features.Moderation;

public class ModeratedProfileNoteMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<ModeratedProfileNoteMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
