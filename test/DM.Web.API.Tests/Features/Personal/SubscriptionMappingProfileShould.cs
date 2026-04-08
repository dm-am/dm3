using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Personal.Subscriptions;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class SubscriptionMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public SubscriptionMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SubscriptionMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

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
