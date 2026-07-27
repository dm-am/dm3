using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Fundraising;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class FundraisingMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<FundraisingMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
