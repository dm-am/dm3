using AutoMapper;
using DM.Infrastructure.Persistence.Repositories.Personal;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Personal;

public class PersonalMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<GeneralUserMappingProfile>();
            cfg.AddProfile<PersonalMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
