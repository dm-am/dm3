using AutoMapper;
using DM.Infrastructure.Persistence.Shared.Subscriptions;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Subscriptions;

public class SubscriptionMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SubscriptionMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
