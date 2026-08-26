using System;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class UserFactoryShould : UnitTestBase
{
    private readonly UserFactory _factory;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _factory = new UserFactory(
            _guidFactory,
            _dateTimeProvider);
    }

    [Fact]
    public void CreateUserFromPendingRegistration()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var pending = new PendingRegistration
        {
            Email = "test@example.com",
            Salt = "test-salt",
            PasswordHash = "test-hash",
            PasswordHashVersion = 1
        };

        _guidFactory.Create().Returns(userId);
        _dateTimeProvider.Now.Returns(now);

        var result = _factory.CreateFromPending(pending, "TestUser");

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.Username.Should().Be("TestUser");
        result.Email.Should().Be(pending.Email);
        result.CreatedUtc.Should().Be(now);
        result.Role.Should().Be(UserRole.RegularUser);
        result.AccessPolicy.Should().Be(AccessPolicy.NotSpecified);
        result.Salt.Should().Be(pending.Salt);
        result.PasswordHash.Should().Be(pending.PasswordHash);
        result.PasswordHashVersion.Should().Be(pending.PasswordHashVersion);
    }

    [Fact]
    public void TrimUsernameWhitespace()
    {
        var pending = new PendingRegistration
        {
            Email = "test@example.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 1
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.CreateFromPending(pending, "  TestUser  ");

        result.Username.Should().Be("TestUser");
    }

    [Fact]
    public void PreserveEmailFromPendingRegistration()
    {
        var pending = new PendingRegistration
        {
            Email = "lowercase@example.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 1
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.CreateFromPending(pending, "User");

        result.Email.Should().Be(pending.Email);
    }
}
