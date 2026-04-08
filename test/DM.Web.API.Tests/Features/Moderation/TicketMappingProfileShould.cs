using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Moderation.Tickets;
using Xunit;

namespace DM.Web.API.Tests.Features.Moderation;

public class TicketMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public TicketMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TicketMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TicketMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
