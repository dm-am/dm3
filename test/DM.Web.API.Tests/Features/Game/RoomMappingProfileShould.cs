using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Rooms;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class RoomMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public RoomMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<CharacterMappingProfile>();
            cfg.AddProfile<RoomMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<CharacterMappingProfile>();
            cfg.AddProfile<RoomMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
