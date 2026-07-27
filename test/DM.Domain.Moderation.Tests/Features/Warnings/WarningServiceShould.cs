using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Warnings;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class WarningServiceShould : UnitTestBase
{
    private readonly Mock<IWarningRepository> _warningRepository;
    private readonly Mock<IBanRepository> _banRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly WarningService _service;
    private readonly Guid _moderatorUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _warningId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public WarningServiceShould()
    {
        _warningRepository = Mock<IWarningRepository>();
        _banRepository = Mock<IBanRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        var moderatorIdentity = Identity.Success(
            new AuthenticatedUser { UserId = _moderatorUserId, Role = UserRole.Moderator, Username = "Moderator" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(moderatorIdentity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_warningId);

        _service = new WarningService(
            new CreateWarningValidator(),
            _warningRepository.Object,
            _banRepository.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task ReturnEmptyListWhenGettingWarningsForNonexistentUser()
    {
        _userLookupService.Setup(s => s.GetAsync("Unknown"))
            .ThrowsAsync(new Exception());

        var result = await _service.GetUserWarnings("Unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewAllWarnings()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var act = () => _service.GetAllWarnings();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only moderators can view all warnings");
    }

    [Fact]
    public async Task AllowModeratorToViewAllWarnings()
    {
        var result = await _service.GetAllWarnings();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToCreateWarning()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var createWarning = new CreateWarning { Username = "Target", Reason = "Bad behavior", Points = 2 };
        var act = () => _service.CreateWarning(createWarning);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only moderators can create warnings");
    }

    [Fact]
    public async Task CreateWarningWithCorrectData()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);

        CreateWarningEntity? capturedEntity = null;
        _warningRepository.Setup(r => r.Create(It.IsAny<CreateWarningEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateWarningEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Warning());

        var entityId = Guid.NewGuid();
        var createWarning = new CreateWarning
        {
            Username = "Target",
            EntityId = entityId,
            EntityType = "Post",
            Reason = "Spam",
            Points = 2
        };

        await _service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.WarningId.Should().Be(_warningId);
        capturedEntity.TargetUserId.Should().Be(_targetUserId);
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
        capturedEntity.EntityId.Should().Be(entityId);
        capturedEntity.EntityType.Should().Be(WarningEntityType.Post);
        capturedEntity.Text.Should().Be("Spam");
        capturedEntity.Points.Should().Be(2);
        capturedEntity.CreatedUtc.Should().Be(_now);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(10)]
    public async Task RejectWarningPointsOutsideZeroToSixRange(int points)
    {
        var createWarning = new CreateWarning
        {
            Username = "Target",
            Reason = "Very bad behavior",
            Points = points
        };

        var act = () => _service.CreateWarning(createWarning);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task CreateVerbalWarningWithZeroPoints()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);

        CreateWarningEntity? capturedEntity = null;
        _warningRepository.Setup(r => r.Create(It.IsAny<CreateWarningEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateWarningEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Warning());

        var createWarning = new CreateWarning
        {
            Username = "Target",
            Reason = "Verbal warning",
            Points = 0
        };

        await _service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Points.Should().Be(0);
    }

    [Fact]
    public async Task ParseEntityTypeCorrectly()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);

        CreateWarningEntity? capturedEntity = null;
        _warningRepository.Setup(r => r.Create(It.IsAny<CreateWarningEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateWarningEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Warning());

        var createWarning = new CreateWarning
        {
            Username = "Target",
            EntityType = "comment",
            Reason = "Inappropriate comment",
            Points = 1
        };

        await _service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntityType.Should().Be(WarningEntityType.Comment);
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToRemoveWarning()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var act = () => _service.RemoveWarning(_warningId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only moderators can remove warnings");
    }

    [Fact]
    public async Task RemoveWarningSuccessfully()
    {
        await _service.RemoveWarning(_warningId);

        _warningRepository.Verify(r => r.Remove(_warningId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserWarningPointsSuccessfully()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _warningRepository.Setup(r => r.GetUserWarningPoints(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _service.GetUserWarningPoints("Target");

        result.Should().Be(5);
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewViolators()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var act = () => _service.GetViolators();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only moderators can view violators");
    }

    [Fact]
    public async Task MergeWarningPointsAndActiveBansIntoViolators()
    {
        var pointsUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "PointsOnly" };
        var bannedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "BannedOnly" };
        var bothUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "Both" };

        _warningRepository.Setup(r => r.GetActiveWarningSummaries(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UserWarningSummary { User = pointsUser, Points = 2, LastWarningUtc = _now.AddDays(-1) },
                new UserWarningSummary { User = bothUser, Points = 5, LastWarningUtc = _now.AddDays(-2) }
            ]);
        _banRepository.Setup(r => r.GetAllActiveBans(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Ban { BanId = Guid.NewGuid(), TargetUser = bannedUser, EndedUtc = _now.AddDays(3) },
                new Ban { BanId = Guid.NewGuid(), TargetUser = bothUser, EndedUtc = _now.AddDays(7) }
            ]);

        var result = (await _service.GetViolators()).ToList();

        result.Should().HaveCount(3);
        // Default sort: points descending, banned user without points goes last
        result[0].User.Username.Should().Be("Both");
        result[0].Points.Should().Be(5);
        result[0].ActiveBan.Should().NotBeNull();
        result[1].User.Username.Should().Be("PointsOnly");
        result[1].Points.Should().Be(2);
        result[1].ActiveBan.Should().BeNull();
        result[2].User.Username.Should().Be("BannedOnly");
        result[2].Points.Should().Be(0);
        result[2].ActiveBan.Should().NotBeNull();
    }

    [Fact]
    public async Task FilterViolatorsByBannedAndPointsOnly()
    {
        var pointsUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "PointsOnly" };
        var bannedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "BannedOnly" };

        _warningRepository.Setup(r => r.GetActiveWarningSummaries(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserWarningSummary { User = pointsUser, Points = 3, LastWarningUtc = _now }]);
        _banRepository.Setup(r => r.GetAllActiveBans(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Ban { BanId = Guid.NewGuid(), TargetUser = bannedUser, EndedUtc = _now.AddDays(1) }]);

        var banned = (await _service.GetViolators(ViolatorsFilter.Banned)).ToList();
        var pointsOnly = (await _service.GetViolators(ViolatorsFilter.PointsOnly)).ToList();

        banned.Should().ContainSingle(v => v.User.Username == "BannedOnly");
        pointsOnly.Should().ContainSingle(v => v.User.Username == "PointsOnly");
    }
}
