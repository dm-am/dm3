using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Posts;
using DM.Web.API.Shared.BbRendering;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class PostMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public PostMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<CharacterMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<PostMappingProfile>();
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
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<PostMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
