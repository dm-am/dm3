using AutoMapper;
using DM.Infrastructure.Persistence.Repositories.Messaging;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Messaging;

public class MessagingMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MessagingMappingProfile>();
            cfg.AddProfile<GeneralUserMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
