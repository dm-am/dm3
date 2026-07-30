using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Boards;
using DM.Web.API.Shared.Dto;
using Xunit;

namespace DM.Web.API.Tests.Features.Forum;

public class BoardMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<UserRefMappingProfile>();
            cfg.AddProfile<BoardMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
