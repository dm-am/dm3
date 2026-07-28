using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class UserMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
