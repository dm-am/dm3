using AutoMapper;
using DM.Infrastructure.Persistence.Repositories.Game;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

public class AttributeSchemaMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AttributeSchemaMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
