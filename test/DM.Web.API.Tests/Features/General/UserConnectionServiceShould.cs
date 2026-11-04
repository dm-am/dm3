using System;
using System.Linq;
using System.Threading;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Realtime;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Guards the lifetime of the SignalR connection map.
/// </summary>
/// <remarks>
/// The map is a singleton activated in the root scope, so a dependency of it lives for the process. It takes
/// none: authentication is the hub's, per invocation.
/// </remarks>
public class UserConnectionServiceShould : UnitTestBase
{
    private readonly UserConnectionService _service = new();

    [Fact]
    public void RegisterAConnectionOfAnAuthenticatedIdentity()
    {
        var userId = Guid.NewGuid();

        _service.Add(AuthenticatedAs(userId), "connection-1");

        var connected = _service.GetConnectedUsers();
        connected.Keys.Should().Equal(userId);
        connected[userId].Should().Equal("connection-1");
    }

    [Fact]
    public void IgnoreAConnectionOfAGuest()
    {
        _service.Add(GuestIdentity(), "connection-1");

        _service.GetConnectedUsers().Keys.Should().BeEmpty(
            "an anonymous connection must never become a per-user target");
    }

    /// <summary>
    /// A connection that arrives while the last one of the same user leaves has
    /// to end up in the map.
    /// </summary>
    /// <remarks>
    /// An Add that read the set before the last connection's removal dropped it would write into an orphan.
    /// The window is a lock acquisition wide, so the assertion runs over rounds.
    /// </remarks>
    [Fact]
    public void KeepAConnectionThatArrivesWhileTheLastOneLeaves()
    {
        const int rounds = 2000;
        var users = new Guid[rounds];
        var identities = new IIdentity[rounds];
        for (var i = 0; i < rounds; i++)
        {
            users[i] = Guid.NewGuid();
            identities[i] = AuthenticatedAs(users[i]);
            _service.Add(identities[i], $"leaving-{i}");
        }

        using var round = new Barrier(2);
        var arriving = new Thread(() =>
        {
            for (var i = 0; i < rounds; i++)
            {
                round.SignalAndWait();
                _service.Add(identities[i], $"arriving-{i}");
            }
        });
        var leaving = new Thread(() =>
        {
            for (var i = 0; i < rounds; i++)
            {
                round.SignalAndWait();
                _service.Remove($"leaving-{i}");
            }
        });

        arriving.Start();
        leaving.Start();
        arriving.Join();
        leaving.Join();

        var connected = _service.GetConnectedUsers();
        Enumerable.Range(0, rounds)
            .Where(i => !connected.TryGetValue(users[i], out var ids) ||
                        !ids.Contains($"arriving-{i}"))
            .Should().BeEmpty(
                "a connection the map lost receives no per-user notification at all, " +
                "and nothing anywhere reports that it is missing");
    }

    private static IIdentity AuthenticatedAs(Guid userId)
    {
        var identity = Mock<IIdentity>();
        identity.User.Returns(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser });
        return identity;
    }

    private static IIdentity GuestIdentity()
    {
        var identity = Mock<IIdentity>();
        identity.User.Returns(AuthenticatedUser.Guest);
        return identity;
    }
}
