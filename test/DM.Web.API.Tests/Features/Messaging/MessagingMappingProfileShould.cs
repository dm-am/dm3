using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Messaging;
using DM.Web.API.Shared.BbRendering;
using Xunit;

namespace DM.Web.API.Tests.Features.Messaging;

public class MessagingMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<MessagingMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
