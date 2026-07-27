using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Awards;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class AwardMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AwardMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
