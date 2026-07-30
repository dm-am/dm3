using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Xunit;

namespace DM.Web.API.Tests.Shared;

public class DiscussionMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<DiscussionMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
