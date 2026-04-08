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

public class BanServiceShould : UnitTestBase
{
    private readonly Mock<IBanRepository> _banRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly BanService _service;
    private readonly Guid _moderatorUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _banId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public BanServiceShould()
    {
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
        _guidFactory.Setup(g => g.Create()).Returns(_banId);

        _service = new BanService(
            _banRepository.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task ReturnEmptyListWhenGettingBansForNonexistentUser()
    {
        _userLookupService.Setup(s => s.GetAsync("Unknown"))
            .ThrowsAsync(new Exception());

        var result = await _service.GetUserBans("Unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewAllBans()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var act = () => _service.GetAllActiveBans();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only moderators can view all bans");
    }

    [Fact]
    public async Task AllowModeratorToViewAllActiveBans()
    {
        _banRepository.Setup(r => r.GetAllActiveBans(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _service.GetAllActiveBans();

        result.Should().NotBeNull();
        _banRepository.Verify(r => r.GetAllActiveBans(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToCreateNonVoluntaryBan()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var createBan = new CreateBan { Username = "Target", IsVoluntary = false, DurationHours = 24 };
        var act = () => _service.CreateBan(createBan);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only moderators can create bans");
    }

    [Fact]
    public async Task ThrowWhenCreatingBanForAlreadyBannedUser()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        var existingBan = new Ban { BanId = Guid.NewGuid(), EndedUtc = _now.AddDays(1) };

        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBan);

        var createBan = new CreateBan { Username = "Target", DurationHours = 24 };
        var act = () => _service.CreateBan(createBan);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("already banned"));
    }

    [Fact]
    public async Task CreateBanWithDurationInHours()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        var createBan = new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Spam",
            IsVoluntary = false
        };

        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.BanId.Should().Be(_banId);
        capturedEntity.TargetUserId.Should().Be(_targetUserId);
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
        capturedEntity.StartedUtc.Should().Be(_now);
        capturedEntity.EndedUtc.Should().Be(_now.AddHours(24));
        capturedEntity.IsVoluntary.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePermanentBanWhenNoDurationProvided()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        var createBan = new CreateBan { Username = "Target", Comment = "Permanent ban" };
        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EndedUtc.Should().Be(_now.AddYears(100));
    }

    [Fact]
    public async Task AllowVoluntaryBanCreatedByUser()
    {
        var targetUser = new GeneralUser { UserId = _moderatorUserId, Username = "Moderator" };
        _userLookupService.Setup(s => s.GetAsync("Moderator")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.GetActiveBan(_moderatorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBanEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ban());

        var createBan = new CreateBan
        {
            Username = "Moderator",
            IsVoluntary = true,
            DurationHours = 48
        };

        await _service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.IsVoluntary.Should().BeTrue();
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
    }

    [Fact]
    public async Task ThrowWhenNonModeratorTriesToLiftBan()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var act = () => _service.LiftBan(_banId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only moderators can lift bans");
    }

    [Fact]
    public async Task LiftBanSuccessfully()
    {
        await _service.LiftBan(_banId);

        _banRepository.Verify(r => r.Remove(_banId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
