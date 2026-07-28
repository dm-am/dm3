using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Shared.BbRendering;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class CharacterMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<UserRefMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<CharacterMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
