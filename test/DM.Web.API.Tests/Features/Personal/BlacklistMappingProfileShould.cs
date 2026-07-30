using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Personal.Blacklists;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class BlacklistMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<BlacklistMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
