using System;
using System.Linq;
using System.Reflection;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Realtime;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Guards the lifetime of the SignalR connection map.
/// </summary>
/// <remarks>
/// The map is registered as a single instance, which Autofac activates in the
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

    private IIdentity AuthenticatedAs(Guid userId)
    {
        var identity = Mock<IIdentity>();
        identity.SetupGet(i => i.User).Returns(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser });
        return identity.Object;
    }

    private IIdentity GuestIdentity()
    {
        var identity = Mock<IIdentity>();
        identity.SetupGet(i => i.User).Returns(AuthenticatedUser.Guest);
        return identity.Object;
    }
}
