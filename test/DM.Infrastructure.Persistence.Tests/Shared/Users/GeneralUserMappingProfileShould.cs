using AutoMapper;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Users;

public class GeneralUserMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<GeneralUserMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
