using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Messaging.GlobalChatEvents;
using DM.Web.API.Shared.BbRendering;
using Xunit;

namespace DM.Web.API.Tests.Features.Messaging;

public class GlobalChatEventMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public GlobalChatEventMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<GlobalChatEventMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<GlobalChatEventMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
