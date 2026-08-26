using System;
using DM.Domain.Account;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Personal.Features.Profiles;
using DM.Infrastructure.Core;
using DM.Infrastructure.Persistence;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// Guards the shared-instance contract of every explicit forwarding group the
/// modules declare: several interfaces, one object per scope.
/// </summary>
/// <remarks>
/// Autofac's AsSelf+AsImplementedInterfaces gave one instance per scope; two
/// naive AddScoped registrations give two. The scan-side half of the port is
/// already pinned (ForwardInterfacesToTheSelfRegistration); this pins the
/// module-side half, where every group breaks silently. The identity pair is
/// the loud one - authentication writes through the setter and every consumer
/// reads null through the provider - but the correlation pair loses nothing
/// visible at all: the token quietly vanishes from the logs, and the paired
/// repository faces stop seeing each other's writes within a request.
///
/// Built out of the real module extensions rather than restated registrations,
/// because the registrations are the thing under test: replace a forwarding
/// factory in AddDmAccount, AddDmCore or AddDmPersistence with a plain
/// AddScoped and the corresponding pair here resolves two objects and fails.
/// </remarks>
public class SharedInstanceForwardingShould : UnitTestBase, IDisposable
{
    private readonly ServiceProvider _provider;

    public SharedInstanceForwardingShould()
    {
        // The same preconditions the hosts give the modules: the context the
        // repositories share arrives from the host, never from the module.
        var services = new ServiceCollection();
        services.AddDbContextPool<DmDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=di-probe;Username=probe;Password=probe"));
        services.AddDmAccount();
        services.AddDmCore();
        services.AddDmPersistence();
        services.AddSingleton<IGuidFactory>(Mock<IGuidFactory>());
        services.AddSingleton<IDateTimeProvider>(Mock<IDateTimeProvider>());

        _provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Every declared group, as pairs. The identity trio is covered by two
    /// pairs through its setter, which is the face authentication writes.
    /// </summary>
    public static TheoryData<Type, Type> ForwardedPairs() => new()
    {
        { typeof(IIdentitySetter), typeof(IIdentityProvider) },
        { typeof(IIdentitySetter), typeof(IAuthorizationContextProvider) },
        { typeof(ICorrelationTokenProvider), typeof(ICorrelationTokenSetter) },
        { typeof(IUserRepository), typeof(IUserReadRepository) },
        { typeof(IUserBlacklistRepository), typeof(IUserBlacklistChecker) },
        { typeof(IUsernameHistoryRepository), typeof(IUsernameHistoryReader) },
    };

    [Theory]
    [MemberData(nameof(ForwardedPairs))]
    public void ResolveBothFacesOfAGroupToOneInstancePerScope(Type first, Type second)
    {
        using var scope = _provider.CreateScope();

        var one = scope.ServiceProvider.GetRequiredService(first);
        scope.ServiceProvider.GetRequiredService(second).Should().BeSameAs(one,
            $"{first.Name} and {second.Name} are two faces of one scoped object: what one " +
            "face writes the other must see, and naive per-interface registrations " +
            "construct a copy per face");

        // The control half: shared within the scope, not process-wide. A group
        // accidentally promoted to a singleton would pass the assert above for
        // the life of the process while leaking one request's state into the next.
        using var another = _provider.CreateScope();
        another.ServiceProvider.GetRequiredService(first).Should().NotBeSameAs(one,
            $"{first.Name} is scoped, not a singleton");
    }

    public void Dispose() => _provider.Dispose();
}
