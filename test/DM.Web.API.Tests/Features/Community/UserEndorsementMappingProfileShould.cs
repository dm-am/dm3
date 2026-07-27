using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Endorsements;
using DM.Web.API.Features.Community.Users;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class UserEndorsementMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<UserEndorsementMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
