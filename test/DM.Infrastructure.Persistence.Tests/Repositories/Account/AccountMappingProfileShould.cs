using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Repositories.Account;
using DM.Testing;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Account;

public class AccountMappingProfileShould : UnitTestBase
{
    private readonly MapperConfiguration _configuration = new(cfg =>
    {
        cfg.AddProfile<AccountMappingProfile>();
    });

    private readonly IMapper _mapper;

    public AccountMappingProfileShould() => _mapper = _configuration.CreateMapper();

    [Fact]
    public void HaveValidConfiguration() => _configuration.AssertConfigurationIsValid();

    [Fact]
    public void MapSessionEntityToSession()
    {
        var entity = new Entities.Account.Session
        {
            Id = System.Guid.NewGuid(),
            IpAddress = "127.0.0.1",
            UserAgent = "Test Agent",
            CreatedUtc = System.DateTime.UtcNow
        };

        var result = _mapper.Map<Session>(entity);

        result.Should().NotBeNull();
        result.Id.Should().Be(entity.Id);
        result.IpAddress.Should().Be(entity.IpAddress);
        result.CreatedUtc.Should().BeCloseTo(new System.DateTimeOffset(entity.CreatedUtc), System.TimeSpan.FromSeconds(1));
    }
}
