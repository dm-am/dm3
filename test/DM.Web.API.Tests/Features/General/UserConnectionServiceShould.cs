using System;
using System.Linq;
using System.Reflection;
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
/// The map is registered as a single instance, which the container activates in the
/// root scope together with everything it asks for. It used to ask for the
/// authentication service, and behind that sat a repository holding a pooled
/// DbContext: one context leased for the life of the process and used by every
/// connecting client. Two clients connecting at the same moment then ran two
/// operations on one context, the losing connection never entered the map and
/// silently stopped receiving per-user events, the change tracker grew by one
/// tracked user per connect, and nothing ever returned the lease to the pool.
///
/// The shape of the fix is the assertion: a singleton that takes nothing can
/// capture nothing. Authentication moved to the hub, which is created per
/// invocation from its own scope, and the map is handed an identity that is
/// already resolved. The two behavior tests hold the other half — the guarantee
/// that a guest is never registered still lives here, where it always was.
/// </remarks>
public class UserConnectionServiceShould : UnitTestBase
{
    private readonly UserConnectionService _service = new();

    [Fact]
    public void TakeNoInjectedDependencies() =>
        typeof(UserConnectionService)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .Should().BeEmpty(
                "the registration is SingleInstance, so whatever is taken here is " +
                "activated once in the root scope and held for the life of the " +
                "process, and the chain behind authentication ends in a pooled " +
                "DbContext that every connection would then be using at once");

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
    /// Add took the set out of the dictionary and locked it as a second step,
    /// while removal of the last connection ran under that same lock and dropped
    /// the set from the dictionary. An Add that had already read the set waited
    /// on the lock and then wrote into an orphan: the connection was in the
    /// owners map, absent from GetConnectedUsers, and every per-user push for
    /// that user went nowhere until the client reconnected. One page reload with
    /// a single tab open reaches it, because the server notices the old socket is
    /// gone while the new one is registering.
    ///
    /// Every round is a separate user, so the two threads contend on one set and
    /// nothing else. The window is a lock acquisition wide, which is why the
    /// assertion is made over rounds rather than over one.
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

    private IIdentity AuthenticatedAs(Guid userId)
    {
        var identity = Mock<IIdentity>();
        identity.User.Returns(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser });
        return identity;
    }

    private IIdentity GuestIdentity()
    {
        var identity = Mock<IIdentity>();
        identity.User.Returns(AuthenticatedUser.Guest);
        return identity;
    }
}
