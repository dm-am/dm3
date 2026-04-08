using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Game.Unread;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class UnreadMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public UnreadMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UnreadMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UnreadMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
