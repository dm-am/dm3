using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Boards;
using DM.Web.API.Features.Forum.Topics;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Xunit;

namespace DM.Web.API.Tests.Features.Forum;

public class TopicMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<UserRefMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<BoardMappingProfile>();
            cfg.AddProfile<TopicMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
