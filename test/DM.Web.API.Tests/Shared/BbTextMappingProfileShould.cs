using AutoMapper;
using DM.Testing;
using DM.Web.API.Shared.BbRendering;
using Xunit;

namespace DM.Web.API.Tests.Shared;

public class BbTextMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<BbTextMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
