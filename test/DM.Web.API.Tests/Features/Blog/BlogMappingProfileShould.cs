using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Blog.Blogs;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Xunit;

namespace DM.Web.API.Tests.Features.Blog;

public class BlogMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<UserRefMappingProfile>();
            cfg.AddProfile<BbTextMappingProfile>();
            cfg.AddProfile<BlogMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
