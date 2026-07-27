using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.General.Tickets;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

public class TicketIntakeMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TicketIntakeMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
