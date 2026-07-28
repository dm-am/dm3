using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class GameMappingProfileShould : UnitTestBase
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
            cfg.AddProfile<AttributeSchemaMappingProfile>();
            cfg.AddProfile<GameMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
