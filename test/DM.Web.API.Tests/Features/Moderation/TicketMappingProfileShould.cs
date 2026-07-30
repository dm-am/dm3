using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Moderation.Tickets;
using DM.Web.API.Shared.Dto;
using Xunit;

namespace DM.Web.API.Tests.Features.Moderation;

public class TicketMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserRefMappingProfile>();
            cfg.AddProfile<TicketMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
