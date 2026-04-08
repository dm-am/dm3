using AutoMapper;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

public class BoardMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<BoardMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
