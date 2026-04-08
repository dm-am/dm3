using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Personal.Notepads;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class NotepadMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public NotepadMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<NotepadMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

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
