using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Polls;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class PollMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public PollMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PollMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PollMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
