using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Personal.Preferences;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class PreferencesMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PreferencesMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
