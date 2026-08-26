using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.Profiles;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Profiles;

public class CommunityProfileServiceShould : UnitTestBase
{
    private readonly IUserReadRepository _userRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly ICache _cache;
    private readonly CommunityProfileService _service;

    public CommunityProfileServiceShould()
    {
        _userRepository = Mock<IUserReadRepository>();
        _userRepository.GetUserDetailsAsync(Arg.Any<string>()).Returns(new UserDetails());
        _userRepository.GetUserDetailsAsync(Arg.Any<Guid>()).Returns(new UserDetails());

        var usernameHistoryReader = Mock<IUsernameHistoryReader>();
        usernameHistoryReader.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UsernameHistoryEntry>());

        _intentionManager = Mock<IIntentionManager>();

        var identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            new UserSettings { Paging = new PagingSettings { EntitiesPerPage = 20 } },
            "token"
        );
        identityProvider.Current.Returns(identity);

        _cache = Mock<ICache>();
        _cache.GetOrCreateAsync(
                Arg.Any<object>(),
                Arg.Any<Func<Task<UserDetails>>>(),
                Arg.Any<TimeSpan>()).Returns(ci => { var key = ci.ArgAt<object>(0); var factory = ci.ArgAt<Func<Task<UserDetails>>>(1); var ttl = ci.ArgAt<TimeSpan>(2); return factory(); });

        _cache.GetOrCreateAsync(
                Arg.Any<object>(),
                Arg.Any<Func<Task<IEnumerable<GeneralUser>>>>(),
                Arg.Any<TimeSpan>()).Returns(ci => { var key = ci.ArgAt<object>(0); var factory = ci.ArgAt<Func<Task<IEnumerable<GeneralUser>>>>(1); var ttl = ci.ArgAt<TimeSpan>(2); return factory(); });

        _service = new CommunityProfileService(
            _userRepository,
            usernameHistoryReader,
            _intentionManager,
            identityProvider,
            _cache);
    }

    [Fact]
    public async Task RetrieveUserProfileByUsername()
    {
        var username = "testuser";

        var result = await _service.GetProfile(username);

        await _userRepository.Received(1).GetUserDetailsAsync(username);
    }

    [Fact]
    public async Task CacheUserProfileByUsername()
    {
        var username = "testuser";

        await _service.GetProfile(username);

        await _cache.Received(1).GetOrCreateAsync(
            $"user_details_{username.ToLowerInvariant()}",
            Arg.Any<Func<Task<UserDetails>>>(),
            CachePolicy.Medium);
    }

    [Fact]
    public async Task RetrieveUserProfileByUserId()
    {
        var userId = Guid.NewGuid();

        var result = await _service.GetProfile(userId);

        await _userRepository.Received(1).GetUserDetailsAsync(userId);
    }

    [Fact]
    public async Task CacheUserProfileByUserId()
    {
        var userId = Guid.NewGuid();

        await _service.GetProfile(userId);

        await _cache.Received(1).GetOrCreateAsync(
            $"user_details_{userId}",
            Arg.Any<Func<Task<UserDetails>>>(),
            CachePolicy.Medium);
    }

    [Fact]
    public async Task AuthorizeViewPendingUsersAction()
    {
        var query = new PagingQuery();
        _userRepository.CountUsersAsync(Arg.Any<UserFilter>()).Returns(0);
        _userRepository.GetUsersAsync(Arg.Any<PagingData>(), Arg.Any<UserFilter>()).Returns(Array.Empty<GeneralUser>());

        await _service.GetUsers(query, new UserFilter { Activity = UserActivityFilter.Pending });

        _intentionManager.Received(1).ThrowIfForbidden(CommunityIntention.ViewPendingUsers);
    }

    /// <summary>
    /// The count and the page must be told the same thing. They used to be handed
    /// fourteen and sixteen separate arguments, eight of them adjacent nullable
    /// ints, with the two lists three slots out of step.
    /// </summary>
    [Fact]
    public async Task FilterTheCountAndThePageByTheSameCriteria()
    {
        UserFilter? countedWith = null;
        UserFilter? pagedWith = null;
        _userRepository.CountUsersAsync(Arg.Any<UserFilter>())
            .Returns(0)
            .AndDoes(ci =>
            {
                var f = ci.ArgAt<UserFilter>(0);
                countedWith = f;
            });
        _userRepository.GetUsersAsync(Arg.Any<PagingData>(), Arg.Any<UserFilter>())
            .Returns(Array.Empty<GeneralUser>())
            .AndDoes(ci =>
            {
                var f = ci.ArgAt<UserFilter>(1);
                pagedWith = f;
            });

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

        await _service.GetUsers(new PagingQuery(), filter);

        countedWith.Should().BeSameAs(filter);
        pagedWith.Should().BeSameAs(filter);
    }

    [Fact]
    public async Task RetrieveUsersByRole()
    {
        var role = UserRole.Admin;
        _userRepository.GetUsersByRoleAsync(role).Returns(Array.Empty<GeneralUser>());

        await _service.GetUsersByRole(role);

        await _userRepository.Received(1).GetUsersByRoleAsync(role);
    }

    [Fact]
    public async Task CacheUsersByRole()
    {
        var role = UserRole.Admin;
        _userRepository.GetUsersByRoleAsync(role).Returns(Array.Empty<GeneralUser>());

        await _service.GetUsersByRole(role);

        await _cache.Received(1).GetOrCreateAsync(
            $"users_by_role_{role}",
            Arg.Any<Func<Task<IEnumerable<GeneralUser>>>>(),
            CachePolicy.LongLived);
    }
}
