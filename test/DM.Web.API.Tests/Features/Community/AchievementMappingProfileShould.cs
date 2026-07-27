using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Achievements;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class AchievementMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AchievementMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
