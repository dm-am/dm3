using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Authorization;
using DM.Domain.Moderation.Features.Profiles;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Profiles;

public class ModeratedProfileServiceShould : UnitTestBase
{
    private readonly IUserReadRepository userRepository;
    private readonly IIntentionManager intentionManager;
    private readonly ICache cache;
    private readonly IModeratedProfileRepository moderatedProfileRepository;
    private readonly ModeratedProfileService service;
    private readonly Guid userId = Guid.NewGuid();

    public ModeratedProfileServiceShould()
    {
        userRepository = Mock<IUserReadRepository>();
        intentionManager = Mock<IIntentionManager>();
        cache = Mock<ICache>();
        moderatedProfileRepository = Mock<IModeratedProfileRepository>();

        service = new ModeratedProfileService(
            userRepository,
            intentionManager,
            cache,
            moderatedProfileRepository);
    }

    [Fact]
    public async Task ThrowWhenProfileNotFound()
    {
        cache.GetOrCreateAsync(
                CacheKeys.UserDetails("TestUser"),
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns((UserDetails?)null);

        var act = () => service.GetProfile("TestUser");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task AuthorizeModerateUserProfileWhenModeratingProfile()
    {
        var user = new GeneralUser { UserId = userId, Username = "TestUser" };
        userRepository.GetUserAsync("TestUser").Returns(user);
        moderatedProfileRepository.UpdateUserInfo("TestUser", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var updatedDetails = new UserDetails { UserId = userId, Username = "TestUser" };
        cache.GetOrCreateAsync(
                CacheKeys.UserDetails("TestUser"),
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns(updatedDetails);

        await service.ModerateProfile("TestUser", "Updated info");

        intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.ModerateUserProfile);
    }

    [Fact]
    public async Task InvalidateCacheAfterModeratingProfile()
    {
        var user = new GeneralUser { UserId = userId, Username = "TestUser" };
        userRepository.GetUserAsync("TestUser").Returns(user);
        moderatedProfileRepository.UpdateUserInfo("TestUser", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var updatedDetails = new UserDetails { UserId = userId, Username = "TestUser" };
        cache.GetOrCreateAsync(
                CacheKeys.UserDetails("TestUser"),
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns(updatedDetails);

        await service.ModerateProfile("TestUser", "Updated info");

        await cache.Received(1).InvalidateAsync(CacheKeys.UserDetails("TestUser"));
    }

    [Fact]
    public async Task AuthorizeSetUserRoleWhenSettingRole()
    {
        userRepository.GetUserAsync("TestUser")
            .Returns(new GeneralUser { UserId = userId, Username = "TestUser", Role = UserRole.RegularUser });
        moderatedProfileRepository.SetUserRole(Arg.Any<string>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        cache.InvalidateAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

        await service.SetUserRole("TestUser", UserRole.Moderator);

        intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.SetUserRole);
    }

    [Fact]
    public async Task ThrowWhenSettingGuestRole()
    {
        var act = () => service.SetUserRole("TestUser", UserRole.Guest);

        await act.Should().ThrowAsync<HttpBadRequestException>()
            .Where(e => e.ValidationErrors.ContainsKey("role"));
    }

    /// <summary>
    /// A role change moves the person between two listings, and both are cached.
    /// </summary>
    /// <remarks>
    /// The user document is cached by name and by identifier, so both keys go.
    /// </remarks>
    [Fact]
    public async Task InvalidateEveryListingASetRoleMovesTheUserBetween()
    {
        userRepository.GetUserAsync("TestUser")
            .Returns(new GeneralUser { UserId = userId, Username = "TestUser", Role = UserRole.RegularUser });
        moderatedProfileRepository.SetUserRole(Arg.Any<string>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await service.SetUserRole("TestUser", UserRole.Moderator);

        await cache.Received(1).InvalidateAsync(CacheKeys.UserDetails("TestUser"));
        await cache.Received(1).InvalidateAsync(CacheKeys.UserDetails(userId));
        // The role being left keeps listing the person until the hour is out, and the
        // role being taken lists one name short for the same hour.
        await cache.Received(1).InvalidateAsync(CacheKeys.UsersByRole(UserRole.RegularUser));
        await cache.Received(1).InvalidateAsync(CacheKeys.UsersByRole(UserRole.Moderator));
    }

    /// <summary>
    /// The manual watch flag: set it, clear it, and read the profile back.
    /// </summary>
    /// <remarks>
    /// Unlike the violators list it does not lapse, so it is stored and only a moderator moves it.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteTheModerationWatchTheModeratorAsksFor(bool underWatch)
    {
        SetupExistingUser();

        await service.SetModerationWatch("TestUser", underWatch);

        await moderatedProfileRepository.Received(1).SetModerationWatch("TestUser", underWatch, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeSetModerationWatchWhenMovingTheWatch()
    {
        SetupExistingUser();

        await service.SetModerationWatch("TestUser", true);

        intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.SetModerationWatch);
    }

    /// <summary>
    /// Both keys of the user document, as on every other write here.
    /// </summary>
    /// <remarks>
    /// The creation path reads this field to decide whether the next game or blog is premoderated.
    /// </remarks>
    [Fact]
    public async Task InvalidateBothKeysOfTheUserAfterMovingTheWatch()
    {
        SetupExistingUser();

        await service.SetModerationWatch("TestUser", true);

        await cache.Received(1).InvalidateAsync(CacheKeys.UserDetails("TestUser"));
        await cache.Received(1).InvalidateAsync(CacheKeys.UserDetails(userId));
    }

    [Fact]
    public async Task RefuseToMoveTheWatchOfSomebodyWhoIsNotThere()
    {
        userRepository.GetUserAsync("TestUser").Returns((GeneralUser?)null);

        var act = () => service.SetModerationWatch("TestUser", true);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найден"));
        await moderatedProfileRepository.DidNotReceive().SetModerationWatch(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private void SetupExistingUser()
    {
        userRepository.GetUserAsync("TestUser").Returns(new GeneralUser { UserId = userId, Username = "TestUser" });
        moderatedProfileRepository
            .SetModerationWatch(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        cache.GetOrCreateAsync(
                CacheKeys.UserDetails("TestUser"),
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns(new UserDetails { UserId = userId, Username = "TestUser" });
    }

    [Fact]
    public async Task RefuseToSetTheRoleOfSomebodyWhoIsNotThere()
    {
        userRepository.GetUserAsync("TestUser").Returns((GeneralUser?)null);

        var act = () => service.SetUserRole("TestUser", UserRole.Moderator);

        // The repository throws on an unknown name, which the pipeline turns into a
        // 500 on an endpoint whose contract documents a 404.
        await act.Should().ThrowAsync<HttpException>();
        await moderatedProfileRepository.DidNotReceive().SetUserRole(Arg.Any<string>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
    }
}
