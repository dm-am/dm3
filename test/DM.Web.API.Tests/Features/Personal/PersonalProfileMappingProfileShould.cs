using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Personal.Profiles;
using DM.Web.API.Shared.BbRendering;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class PersonalProfileMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<PersonalProfileMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
