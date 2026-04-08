using System;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Testing;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

public class SessionFactoryShould : UnitTestBase
{
    private readonly SessionFactory _factory;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly AuthenticationConfiguration _config;

    public SessionFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _config = new AuthenticationConfiguration
        {
            SessionExpirationHours = 12,
            PersistentSessionExpirationDays = 30
        };

        _factory = new SessionFactory(
            _guidFactory.Object,
            _dateTimeProvider.Object,
            Options.Create(_config));
    }

    [Fact]
    public void CreateNonPersistentSessionWithValidExpiration()
    {
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _guidFactory.Setup(f => f.Create()).Returns(sessionId);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var result = _factory.Create(persistent: false, invisible: false);

        result.Should().NotBeNull();
        result.Id.Should().Be(sessionId);
        result.Persistent.Should().BeFalse();
        result.Invisible.Should().BeFalse();
        result.CreatedUtc.Should().Be(now.UtcDateTime);
        result.ExpirationUtc.Should().Be(now.UtcDateTime.AddHours(_config.SessionExpirationHours));
    }

    [Fact]
    public void CreatePersistentSessionWithValidExpiration()
    {
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _guidFactory.Setup(f => f.Create()).Returns(sessionId);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var result = _factory.Create(persistent: true, invisible: false);

        result.Should().NotBeNull();
        result.Id.Should().Be(sessionId);
        result.Persistent.Should().BeTrue();
        result.CreatedUtc.Should().Be(now.UtcDateTime);
        result.ExpirationUtc.Should().Be(now.UtcDateTime.AddDays(_config.PersistentSessionExpirationDays));
    }

    [Fact]
    public void CreateInvisibleSession()
    {
        var sessionId = Guid.NewGuid();
        _guidFactory.Setup(f => f.Create()).Returns(sessionId);
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(persistent: false, invisible: true);

        result.Should().NotBeNull();
        result.Invisible.Should().BeTrue();
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
        _guidFactory.Setup(f => f.Create()).Returns(sessionId);
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(persistent: false, invisible: false, context);

        result.Should().NotBeNull();
        result.IpAddress.Should().Be(context.IpAddress);
        result.UserAgent.Should().Be(context.UserAgent);
        result.DeviceInfo.Should().NotBeNull();
    }
}
