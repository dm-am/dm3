using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.ChatRooms;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class ChatRoomMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<ChatRoomMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
