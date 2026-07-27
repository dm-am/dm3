using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Moderation.Tags;
using Xunit;

namespace DM.Web.API.Tests.Features.Moderation;

public class TagMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TagMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
