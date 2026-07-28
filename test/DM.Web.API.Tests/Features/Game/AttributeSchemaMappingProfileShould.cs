using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.AttributeSchemas;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

public class AttributeSchemaMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<UserRefMappingProfile>();
            cfg.AddProfile<AttributeSchemaMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
