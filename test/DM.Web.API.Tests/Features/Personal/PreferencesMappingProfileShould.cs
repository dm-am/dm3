using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Personal.Preferences;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class PreferencesMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public PreferencesMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PreferencesMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

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
