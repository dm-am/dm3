using DM.Infrastructure.Persistence.Repositories.Account;
using DM.Testing;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Account;

public class AccountMapperShould : UnitTestBase
{
    [Fact]
    public void MapSessionRowToSession()
    {
        var entity = new Entities.Account.UserSession
        {
            SessionId = System.Guid.NewGuid(),
            UserId = System.Guid.NewGuid(),
            IpAddress = "127.0.0.1",
            UserAgent = "Test Agent",
            CreatedUtc = System.DateTimeOffset.UtcNow
        };

        var result = entity.ToSession();

        result.Should().NotBeNull();
        result.Id.Should().Be(entity.SessionId);
        result.IpAddress.Should().Be(entity.IpAddress);
        result.CreatedUtc.Should().Be(entity.CreatedUtc);
    }
}
