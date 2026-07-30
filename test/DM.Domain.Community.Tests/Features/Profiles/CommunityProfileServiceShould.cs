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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Profiles;

public class CommunityProfileServiceShould : UnitTestBase
{
    private readonly Mock<IUserReadRepository> _userRepository;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<ICache> _cache;
    private readonly CommunityProfileService _service;

    public CommunityProfileServiceShould()
    {
        _userRepository = Mock<IUserReadRepository>();
        _userRepository.Setup(r => r.GetUserDetailsAsync(It.IsAny<string>()))
            .ReturnsAsync(new UserDetails());
        _userRepository.Setup(r => r.GetUserDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new UserDetails());

        var usernameHistoryReader = Mock<IUsernameHistoryReader>();
        usernameHistoryReader.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UsernameHistoryEntry>());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<CommunityIntention>()));

        var identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            new UserSettings { Paging = new PagingSettings { EntitiesPerPage = 20 } },
            "token"
        );
        identityProvider.Setup(p => p.Current).Returns(identity);

        _cache = Mock<ICache>();
        _cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<object>(),
                It.IsAny<Func<Task<UserDetails>>>(),
                It.IsAny<TimeSpan>()))
            .Returns((object key, Func<Task<UserDetails>> factory, TimeSpan ttl) => factory());

        _cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<object>(),
                It.IsAny<Func<Task<IEnumerable<GeneralUser>>>>(),
                It.IsAny<TimeSpan>()))
            .Returns((object key, Func<Task<IEnumerable<GeneralUser>>> factory, TimeSpan ttl) => factory());

        _service = new CommunityProfileService(
            _userRepository.Object,
            usernameHistoryReader.Object,
            _intentionManager.Object,
            identityProvider.Object,
            _cache.Object);
    }

    [Fact]
    public async Task RetrieveUserProfileByUsername()
    {
        var username = "testuser";

        var result = await _service.GetProfile(username);

        _userRepository.Verify(r => r.GetUserDetailsAsync(username), Times.Once);
    }

    [Fact]
    public async Task CacheUserProfileByUsername()
    {
        var username = "testuser";

        await _service.GetProfile(username);

        _cache.Verify(c => c.GetOrCreateAsync(
            $"user_details_{username.ToLowerInvariant()}",
            It.IsAny<Func<Task<UserDetails>>>(),
            CachePolicy.Medium), Times.Once);
    }

    [Fact]
    public async Task RetrieveUserProfileByUserId()
    {
        var userId = Guid.NewGuid();

        var result = await _service.GetProfile(userId);

        _userRepository.Verify(r => r.GetUserDetailsAsync(userId), Times.Once);
    }

    [Fact]
    public async Task CacheUserProfileByUserId()
    {
        var userId = Guid.NewGuid();

        await _service.GetProfile(userId);

        _cache.Verify(c => c.GetOrCreateAsync(
            $"user_details_{userId}",
            It.IsAny<Func<Task<UserDetails>>>(),
            CachePolicy.Medium), Times.Once);
    }

    [Fact]
    public async Task AuthorizeViewPendingUsersAction()
    {
        var query = new PagingQuery();
        _userRepository.Setup(r => r.CountUsersAsync(It.IsAny<UserFilter>())).ReturnsAsync(0);
        _userRepository.Setup(r => r.GetUsersAsync(It.IsAny<PagingData>(), It.IsAny<UserFilter>()))
            .ReturnsAsync(Array.Empty<GeneralUser>());

        await _service.GetUsers(query, new UserFilter { Activity = UserActivityFilter.Pending });

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommunityIntention.ViewPendingUsers), Times.Once);
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
        _userRepository.Setup(r => r.CountUsersAsync(It.IsAny<UserFilter>()))
            .Callback<UserFilter>(f => countedWith = f)
            .ReturnsAsync(0);
        _userRepository.Setup(r => r.GetUsersAsync(It.IsAny<PagingData>(), It.IsAny<UserFilter>()))
            .Callback<PagingData, UserFilter>((_, f) => pagedWith = f)
            .ReturnsAsync(Array.Empty<GeneralUser>());

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
        _userRepository.Setup(r => r.GetUsersByRoleAsync(role))
            .ReturnsAsync(Array.Empty<GeneralUser>());

        await _service.GetUsersByRole(role);

        _userRepository.Verify(r => r.GetUsersByRoleAsync(role), Times.Once);
    }

    [Fact]
    public async Task CacheUsersByRole()
    {
        var role = UserRole.Admin;
        _userRepository.Setup(r => r.GetUsersByRoleAsync(role))
            .ReturnsAsync(Array.Empty<GeneralUser>());

        await _service.GetUsersByRole(role);

        _cache.Verify(c => c.GetOrCreateAsync(
            $"users_by_role_{role}",
            It.IsAny<Func<Task<IEnumerable<GeneralUser>>>>(),
            CachePolicy.LongLived), Times.Once);
    }
}
