using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Personal.Profiles;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class PersonalProfileMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public PersonalProfileMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PersonalProfileMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PersonalProfileMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
