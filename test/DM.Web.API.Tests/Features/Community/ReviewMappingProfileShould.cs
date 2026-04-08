using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Reviews;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

public class ReviewMappingProfileShould : UnitTestBase
{
    private readonly IMapper _mapper;

    public ReviewMappingProfileShould()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ReviewMappingProfile>();
        });
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ReviewMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
