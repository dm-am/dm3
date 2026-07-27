using AutoMapper;
using DM.Infrastructure.Persistence.Repositories.Community;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Community;

public class FundraisingGoalMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<FundraisingGoalMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
