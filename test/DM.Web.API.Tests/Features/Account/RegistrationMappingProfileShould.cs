using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Account.Registration;
using Xunit;

namespace DM.Web.API.Tests.Features.Account;

public class RegistrationMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<RegistrationMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
