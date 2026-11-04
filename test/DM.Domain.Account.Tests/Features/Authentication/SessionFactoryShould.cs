using System;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

public class SessionFactoryShould : UnitTestBase
{
    private readonly SessionFactory factory;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;

    public SessionFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        var config = new AuthenticationConfiguration
        {
            SessionExpirationHours = 12,
            PersistentSessionExpirationDays = 30
        };

        factory = new SessionFactory(
            guidFactory,
            dateTimeProvider,
            Options.Create(config));
    }

    /// <summary>
    /// Twelve hours for a session and thirty days for a remembered one, as configured above.
    /// </summary>
    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 30 * 24)]
    public void CreateASessionThatLastsAsLongAsItsKind(bool persistent, int hours)
    {
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        guidFactory.Create().Returns(sessionId);
        dateTimeProvider.Now.Returns(now);

        var result = factory.Create(persistent);

        result.Id.Should().Be(sessionId);
        result.Persistent.Should().Be(persistent);
        result.CreatedUtc.Should().Be(now);
        result.ExpirationUtc.Should().Be(now.AddHours(hours));
    }

    [Fact]
    public void CreateSessionWithContext()
    {
        var sessionId = Guid.NewGuid();
        var context = new SessionContext
        {
            IpAddress = "127.0.0.1",
            UserAgent = "Mozilla/5.0"
        };
        guidFactory.Create().Returns(sessionId);
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = factory.Create(persistent: false, context);

        result.IpAddress.Should().Be(context.IpAddress);
        result.UserAgent.Should().Be(context.UserAgent);
        result.DeviceInfo.Should().NotBeNull();
    }
}
