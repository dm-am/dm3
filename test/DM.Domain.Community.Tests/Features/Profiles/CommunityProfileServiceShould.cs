using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Profiles;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Profiles;

public class CommunityProfileServiceShould : UnitTestBase
{
    private readonly IUserReadRepository userRepository;
    private readonly ICache cache;
    private readonly CommunityProfileService service;

    public CommunityProfileServiceShould()
    {
        userRepository = Mock<IUserReadRepository>();
        userRepository.GetUserDetailsAsync(Arg.Any<string>()).Returns(new UserDetails());
        userRepository.GetUserDetailsAsync(Arg.Any<Guid>()).Returns(new UserDetails());

        var usernameHistoryReader = Mock<IUsernameHistoryReader>();
        usernameHistoryReader.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UsernameHistoryEntry>());


        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "TestUser", UserRole.RegularUser,
            new UserSettings { PageSize = 20 }));

        cache = Mock<ICache>();
        PassThroughCache<UserDetails>();
        PassThroughCache<IEnumerable<GeneralUser>>();

        service = new CommunityProfileService(
            userRepository,
            usernameHistoryReader,
            identityProvider,
            cache);
    }

    /// <summary>The cache runs the factory it is handed, every time.</summary>
    private void PassThroughCache<T>() =>
        cache.GetOrCreateAsync(Arg.Any<object>(), Arg.Any<Func<Task<T>>>(), Arg.Any<TimeSpan>())
            .Returns(ci => ci.ArgAt<Func<Task<T>>>(1)());

    [Fact]
    public async Task RetrieveUserProfileByUsername()
    {
        var username = "testuser";

        await service.GetProfile(username);

        await userRepository.Received(1).GetUserDetailsAsync(username);
    }

    [Fact]
    public async Task CacheUserProfileByUsername()
    {
        var username = "testuser";

        await service.GetProfile(username);

        await cache.Received(1).GetOrCreateAsync(
            CacheKeys.UserDetails(username),
            Arg.Any<Func<Task<UserDetails>>>(),
            CachePolicy.Medium);
    }

    [Fact]
    public async Task RetrieveUserProfileByUserId()
    {
        var userId = Guid.NewGuid();

        await service.GetProfile(userId);

        await userRepository.Received(1).GetUserDetailsAsync(userId);
    }

    [Fact]
    public async Task CacheUserProfileByUserId()
    {
        var userId = Guid.NewGuid();

        await service.GetProfile(userId);

        await cache.Received(1).GetOrCreateAsync(
            CacheKeys.UserDetails(userId),
            Arg.Any<Func<Task<UserDetails>>>(),
            CachePolicy.Medium);
    }

    /// <summary>
    /// The count and the page are filtered by one filter.
    /// </summary>
    [Fact]
    public async Task FilterTheCountAndThePageByTheSameCriteria()
    {
        UserFilter? countedWith = null;
        UserFilter? pagedWith = null;
        userRepository.CountUsersAsync(Arg.Do<UserFilter>(f => countedWith = f))
            .Returns(0);
        userRepository.GetUsersAsync(Arg.Any<PagingData>(), Arg.Do<UserFilter>(f => pagedWith = f))
            .Returns(Array.Empty<GeneralUser>());

        var filter = new UserFilter
        {
            Activity = UserActivityFilter.Active,
            MinRating = 1,
            MaxRating = 2,
            MinGamesHosting = 3,
            MaxGamesHosting = 4,
            MinGamesPlaying = 5,
            MaxGamesPlaying = 6,
            MinBlogsHosting = 7,
            MaxBlogsHosting = 8
        };

        await service.GetUsers(new PagingQuery(), filter);

        countedWith.Should().BeSameAs(filter);
        pagedWith.Should().BeSameAs(filter);
    }

    [Fact]
    public async Task RetrieveUsersByRole()
    {
        var role = UserRole.Admin;
        userRepository.GetUsersByRoleAsync(role).Returns(Array.Empty<GeneralUser>());

        await service.GetUsersByRole(role);

        await userRepository.Received(1).GetUsersByRoleAsync(role);
    }

    [Fact]
    public async Task CacheUsersByRole()
    {
        var role = UserRole.Admin;
        userRepository.GetUsersByRoleAsync(role).Returns(Array.Empty<GeneralUser>());

        await service.GetUsersByRole(role);

        await cache.Received(1).GetOrCreateAsync(
            CacheKeys.UsersByRole(role),
            Arg.Any<Func<Task<IEnumerable<GeneralUser>>>>(),
            CachePolicy.LongLived);
    }
}
