using AutoMapper;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Moderation;

public class ModerationMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ModerationMappingProfile>();
            cfg.AddProfile<GeneralUserMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
