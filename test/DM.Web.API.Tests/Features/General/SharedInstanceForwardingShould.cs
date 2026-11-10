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

// Two plain AddScoped registrations give two objects per scope.
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

    // The identity trio is covered by two pairs through its setter, which is the face
    // authentication writes.
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
