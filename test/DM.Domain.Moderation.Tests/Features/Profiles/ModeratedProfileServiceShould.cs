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
    private readonly IUserReadRepository _userRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly ICache _cache;
    private readonly IModeratedProfileRepository _moderatedProfileRepository;
    private readonly ModeratedProfileService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public ModeratedProfileServiceShould()
    {
        _userRepository = Mock<IUserReadRepository>();
        _intentionManager = Mock<IIntentionManager>();
        _cache = Mock<ICache>();
        _moderatedProfileRepository = Mock<IModeratedProfileRepository>();

        _service = new ModeratedProfileService(
            _userRepository,
            _intentionManager,
            _cache,
            _moderatedProfileRepository);
    }

    [Fact]
    public async Task GetProfileFromCache()
    {
        var expectedProfile = new UserDetails { UserId = _userId, Username = "TestUser" };
        _cache.GetOrCreateAsync(
                "user_details_testuser",
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns(expectedProfile);

        var result = await _service.GetProfile("TestUser");

        result.Should().Be(expectedProfile);
    }

    [Fact]
    public async Task ThrowWhenProfileNotFound()
    {
        _cache.GetOrCreateAsync(
                "user_details_testuser",
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns((UserDetails?)null);

        var act = () => _service.GetProfile("TestUser");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task AuthorizeModerateUserProfileWhenModeratingProfile()
    {
        var user = new GeneralUser { UserId = _userId, Username = "TestUser" };
        _userRepository.GetUserAsync("TestUser").Returns(user);
        _moderatedProfileRepository.UpdateUserInfo("TestUser", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var updatedDetails = new UserDetails { UserId = _userId, Username = "TestUser" };
        _cache.GetOrCreateAsync(
                "user_details_testuser",
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns(updatedDetails);

        await _service.ModerateProfile("TestUser", "Updated info");

        _intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.ModerateUserProfile);
    }

    [Fact]
    public async Task InvalidateCacheAfterModeratingProfile()
    {
        var user = new GeneralUser { UserId = _userId, Username = "TestUser" };
        _userRepository.GetUserAsync("TestUser").Returns(user);
        _moderatedProfileRepository.UpdateUserInfo("TestUser", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var updatedDetails = new UserDetails { UserId = _userId, Username = "TestUser" };
        _cache.GetOrCreateAsync(
                "user_details_testuser",
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns(updatedDetails);

        await _service.ModerateProfile("TestUser", "Updated info");

        await _cache.Received(1).InvalidateAsync("user_details_testuser");
    }

    [Fact]
    public async Task AuthorizeSetUserRoleWhenSettingRole()
    {
        _userRepository.GetUserAsync("TestUser")
            .Returns(new GeneralUser { UserId = _userId, Username = "TestUser", Role = UserRole.RegularUser });
        _moderatedProfileRepository.SetUserRole(Arg.Any<string>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _cache.InvalidateAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

        await _service.SetUserRole("TestUser", UserRole.Moderator);

        _intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.SetUserRole);
    }

    [Fact]
    public async Task ThrowWhenSettingGuestRole()
    {
        var act = () => _service.SetUserRole("TestUser", UserRole.Guest);

        await act.Should().ThrowAsync<HttpBadRequestException>()
            .Where(e => e.ValidationErrors.ContainsKey("role"));
    }

    /// <summary>
    /// A role change moves the person between two listings, and both are cached.
    /// </summary>
    /// <remarks>
    /// The listing by role lives an hour and was invalidated by nothing: for that
    /// hour the staff page showed the person under the role they no longer hold, and
    /// the role they now hold one name short. The user document is read back by two
    /// keys, by name on the profile page and by identifier wherever a link to that
    /// person is built, so both go.
    /// </remarks>
    [Fact]
    public async Task InvalidateEveryListingASetRoleMovesTheUserBetween()
    {
        _userRepository.GetUserAsync("TestUser")
            .Returns(new GeneralUser { UserId = _userId, Username = "TestUser", Role = UserRole.RegularUser });
        _moderatedProfileRepository.SetUserRole(Arg.Any<string>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _service.SetUserRole("TestUser", UserRole.Moderator);

        await _cache.Received(1).InvalidateAsync("user_details_testuser");
        await _cache.Received(1).InvalidateAsync($"user_details_{_userId}");
        // The role being left keeps listing the person until the hour is out, and the
        // role being taken lists one name short for the same hour.
        await _cache.Received(1).InvalidateAsync("users_by_role_RegularUser");
        await _cache.Received(1).InvalidateAsync("users_by_role_Moderator");
    }

    #region Moderation watch

    /// <summary>
    /// The manual watch flag: set it, clear it, and read the profile back.
    /// </summary>
    /// <remarks>
    /// It is not the violators list. That one is recomputed from active warnings
    /// and bans on every read and lapses when they expire; this is the case where
    /// moderation decided the lapse should not happen yet, so it is stored and
    /// only a moderator moves it.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteTheModerationWatchTheModeratorAsksFor(bool underWatch)
    {
        SetupExistingUser();

        await _service.SetModerationWatch("TestUser", underWatch);

        await _moderatedProfileRepository.Received(1).SetModerationWatch("TestUser", underWatch, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeSetModerationWatchWhenMovingTheWatch()
    {
        SetupExistingUser();

        await _service.SetModerationWatch("TestUser", true);

        _intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.SetModerationWatch);
    }

    /// <summary>
    /// Both keys of the user document, as on every other write here.
    /// </summary>
    /// <remarks>
    /// A stale copy of this field is not cosmetic: it is what the creation path
    /// reads to decide whether the user's next game or blog is premoderated.
    /// </remarks>
    [Fact]
    public async Task InvalidateBothKeysOfTheUserAfterMovingTheWatch()
    {
        SetupExistingUser();

        await _service.SetModerationWatch("TestUser", true);

        await _cache.Received(1).InvalidateAsync("user_details_testuser");
        await _cache.Received(1).InvalidateAsync($"user_details_{_userId}");
    }

    [Fact]
    public async Task RefuseToMoveTheWatchOfSomebodyWhoIsNotThere()
    {
        _userRepository.GetUserAsync("TestUser").Returns((GeneralUser?)null);

        var act = () => _service.SetModerationWatch("TestUser", true);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найден"));
        await _moderatedProfileRepository.DidNotReceive().SetModerationWatch(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private void SetupExistingUser()
    {
        _userRepository.GetUserAsync("TestUser").Returns(new GeneralUser { UserId = _userId, Username = "TestUser" });
        _moderatedProfileRepository
            .SetModerationWatch(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _cache.GetOrCreateAsync(
                "user_details_testuser",
                Arg.Any<Func<Task<UserDetails?>>>(),
                Arg.Any<TimeSpan>()).Returns(new UserDetails { UserId = _userId, Username = "TestUser" });
    }

    #endregion

    [Fact]
    public async Task RefuseToSetTheRoleOfSomebodyWhoIsNotThere()
    {
        _userRepository.GetUserAsync("TestUser").Returns((GeneralUser?)null);

        var act = () => _service.SetUserRole("TestUser", UserRole.Moderator);

        // The repository throws on an unknown name, which the pipeline turns into a
        // 500 on an endpoint whose contract documents a 404.
        await act.Should().ThrowAsync<HttpException>();
        await _moderatedProfileRepository.DidNotReceive().SetUserRole(Arg.Any<string>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
    }
}
