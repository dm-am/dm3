using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Warnings;
using Xunit;

namespace DM.Web.API.Tests.Features.Moderation;

public class WarningMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public WarningMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<WarningMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<WarningMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
