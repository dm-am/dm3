using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class CharacterMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public CharacterMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<CharacterMappingProfile>();
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
        });
        configuration.AssertConfigurationIsValid();
    }
}
