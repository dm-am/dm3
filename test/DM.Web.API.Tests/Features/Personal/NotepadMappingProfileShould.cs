using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Personal.Notepads;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class NotepadMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<NotepadMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
