using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.AttributeSchemas;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class AttributeSchemaMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public AttributeSchemaMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<AttributeSchemaMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<AttributeSchemaMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
