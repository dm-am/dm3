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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Profiles;

public class ModeratedProfileServiceShould : UnitTestBase
{
    private readonly Mock<IUserReadRepository> _userRepository;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<ICache> _cache;
    private readonly Mock<IModeratedProfileRepository> _moderatedProfileRepository;
    private readonly ModeratedProfileService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public ModeratedProfileServiceShould()
    {
        _userRepository = Mock<IUserReadRepository>();
        _intentionManager = Mock<IIntentionManager>();
        _cache = Mock<ICache>();
        _moderatedProfileRepository = Mock<IModeratedProfileRepository>();

        _service = new ModeratedProfileService(
            _userRepository.Object,
            _intentionManager.Object,
            _cache.Object,
            _moderatedProfileRepository.Object);
    }

    [Fact]
    public async Task GetProfileFromCache()
    {
        var expectedProfile = new UserDetails { UserId = _userId, Username = "TestUser" };
        _cache.Setup(c => c.GetOrCreateAsync(
                "user_details_testuser",
                It.IsAny<Func<Task<UserDetails?>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync(expectedProfile);

        var result = await _service.GetProfile("TestUser");

        result.Should().Be(expectedProfile);
    }

    [Fact]
    public async Task ThrowWhenProfileNotFound()
    {
        _cache.Setup(c => c.GetOrCreateAsync(
                "user_details_testuser",
                It.IsAny<Func<Task<UserDetails?>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync((UserDetails?)null);

        var act = () => _service.GetProfile("TestUser");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task AuthorizeModerateUserProfileWhenModeratingProfile()
    {
        var user = new GeneralUser { UserId = _userId, Username = "TestUser" };
        _userRepository.Setup(r => r.GetUserAsync("TestUser")).ReturnsAsync(user);
        _moderatedProfileRepository.Setup(r => r.UpdateUserInfo("TestUser", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var updatedDetails = new UserDetails { UserId = _userId, Username = "TestUser" };
        _cache.Setup(c => c.GetOrCreateAsync(
                "user_details_testuser",
                It.IsAny<Func<Task<UserDetails?>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync(updatedDetails);

        await _service.ModerateProfile("TestUser", "Updated info");

        _intentionManager.Verify(m => m.ThrowIfForbidden(ModerationIntention.ModerateUserProfile), Times.Once);
    }

    [Fact]
    public async Task InvalidateCacheAfterModeratingProfile()
    {
        var user = new GeneralUser { UserId = _userId, Username = "TestUser" };
        _userRepository.Setup(r => r.GetUserAsync("TestUser")).ReturnsAsync(user);
        _moderatedProfileRepository.Setup(r => r.UpdateUserInfo("TestUser", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var updatedDetails = new UserDetails { UserId = _userId, Username = "TestUser" };
        _cache.Setup(c => c.GetOrCreateAsync(
                "user_details_testuser",
                It.IsAny<Func<Task<UserDetails?>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync(updatedDetails);

        await _service.ModerateProfile("TestUser", "Updated info");

        _cache.Verify(c => c.InvalidateAsync("user_details_testuser"), Times.Once);
    }

    [Fact]
    public async Task AuthorizeSetUserRoleWhenSettingRole()
    {
        _userRepository.Setup(r => r.GetUserAsync("TestUser"))
            .ReturnsAsync(new GeneralUser { UserId = _userId, Username = "TestUser", Role = UserRole.RegularUser });
        _moderatedProfileRepository.Setup(r => r.SetUserRole(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(c => c.InvalidateAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _service.SetUserRole("TestUser", UserRole.Moderator);

        _intentionManager.Verify(m => m.ThrowIfForbidden(ModerationIntention.SetUserRole), Times.Once);
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
        _userRepository.Setup(r => r.GetUserAsync("TestUser"))
            .ReturnsAsync(new GeneralUser { UserId = _userId, Username = "TestUser", Role = UserRole.RegularUser });
        _moderatedProfileRepository.Setup(r => r.SetUserRole(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.SetUserRole("TestUser", UserRole.Moderator);

        _cache.Verify(c => c.InvalidateAsync("user_details_testuser"), Times.Once);
        _cache.Verify(c => c.InvalidateAsync($"user_details_{_userId}"), Times.Once);
        _cache.Verify(c => c.InvalidateAsync("users_by_role_RegularUser"), Times.Once,
            "the role being left keeps listing the person until the hour is out");
        _cache.Verify(c => c.InvalidateAsync("users_by_role_Moderator"), Times.Once,
            "and the role being taken lists one name short for the same hour");
    }

    [Fact]
    public async Task RefuseToSetTheRoleOfSomebodyWhoIsNotThere()
    {
        _userRepository.Setup(r => r.GetUserAsync("TestUser")).ReturnsAsync((GeneralUser?)null);

        var act = () => _service.SetUserRole("TestUser", UserRole.Moderator);

        // The repository throws on an unknown name, which the pipeline turns into a
        // 500 on an endpoint whose contract documents a 404.
        await act.Should().ThrowAsync<HttpException>();
        _moderatedProfileRepository.Verify(
            r => r.SetUserRole(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
