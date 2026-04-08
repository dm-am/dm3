using System;
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
            _warningRepository.Object,
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

    [Fact]
    public async Task ClampWarningPointsBetween1And3()
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
            Reason = "Very bad behavior",
            Points = 10
        };

        await _service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Points.Should().Be(3);
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
}
