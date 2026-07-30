using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Blacklists;

public class UserBlacklistServiceShould : UnitTestBase
{
    private readonly Mock<IUserBlacklistRepository> _repository;
    private readonly Mock<IUserRepository> _userRepository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly UserBlacklistService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _entryId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public UserBlacklistServiceShould()
    {
        _repository = Mock<IUserBlacklistRepository>();
        _userRepository = Mock<IUserRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identities.User(_currentUserId, "CurrentUser", UserRole.RegularUser);
        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_entryId);

        _service = new UserBlacklistService(
            _repository.Object,
            _userRepository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task GetMyBlacklistForCurrentUser()
    {
        _repository.Setup(r => r.GetBlacklist(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.GetMyBlacklist();

        _repository.Verify(r => r.GetBlacklist(_currentUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenBlockingNonexistentUser()
    {
        _userRepository.Setup(r => r.FindUserIdAsync("Unknown"))
            .ReturnsAsync((Guid?)null);

        var act = () => _service.Block(new OperateUserBlacklistLink { Username = "Unknown" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("not found"));
    }

    [Fact]
    public async Task ThrowWhenUserTriesToBlockThemselves()
    {
        _userRepository.Setup(r => r.FindUserIdAsync("CurrentUser"))
            .ReturnsAsync(_currentUserId);

        var act = () => _service.Block(new OperateUserBlacklistLink { Username = "CurrentUser" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Cannot block yourself"));
    }

    [Fact]
    public async Task ReturnExistingEntryWhenAlreadyBlocked()
    {
        var existingEntry = new BlacklistEntry { Id = Guid.NewGuid() };

        _userRepository.Setup(r => r.FindUserIdAsync("Target")).ReturnsAsync(_targetUserId);
        _repository.Setup(r => r.Find(_currentUserId, _targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        var result = await _service.Block(new OperateUserBlacklistLink { Username = "Target" });

        result.Should().Be(existingEntry);
        _repository.Verify(r => r.Create(It.IsAny<CreateBlacklistEntryEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateNewBlacklistEntry()
    {
        _userRepository.Setup(r => r.FindUserIdAsync("Target")).ReturnsAsync(_targetUserId);
        _repository.Setup(r => r.Find(_currentUserId, _targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlacklistEntry?)null);

        CreateBlacklistEntryEntity? capturedEntity = null;
        _repository.Setup(r => r.Create(It.IsAny<CreateBlacklistEntryEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBlacklistEntryEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new BlacklistEntry());

        await _service.Block(new OperateUserBlacklistLink { Username = "Target" });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntryId.Should().Be(_entryId);
        capturedEntity.OwnerId.Should().Be(_currentUserId);
        capturedEntity.BlockedUserId.Should().Be(_targetUserId);
        capturedEntity.CreatedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task UnblockUserSuccessfully()
    {
        var existingEntry = new BlacklistEntry { Id = _entryId };

        _userRepository.Setup(r => r.FindUserIdAsync("Target")).ReturnsAsync(_targetUserId);
        _repository.Setup(r => r.Find(_currentUserId, _targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        await _service.Unblock(new OperateUserBlacklistLink { Username = "Target" });

        _repository.Verify(r => r.Delete(_entryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoNothingWhenUnblockingNonexistentUser()
    {
        _userRepository.Setup(r => r.FindUserIdAsync("Unknown"))
            .ReturnsAsync((Guid?)null);

        await _service.Unblock(new OperateUserBlacklistLink { Username = "Unknown" });

        _repository.Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckIfUserIsBlocked()
    {
        _userRepository.Setup(r => r.FindUserIdAsync("Target")).ReturnsAsync(_targetUserId);
        _repository.Setup(r => r.IsBlockedAsync(_currentUserId, _targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.IsBlocked("Target");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnCannotCommunicateWhenYouBlockedThem()
    {
        _repository.Setup(r => r.IsBlockedAsync(_currentUserId, _targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.GetBlockStatus(_targetUserId);

        result.CanCommunicate.Should().BeFalse();
        result.Reason.Should().Be("YouBlockedThem");
    }

    [Fact]
    public async Task ReturnCannotCommunicateWhenTheyBlockedYou()
    {
        _repository.Setup(r => r.IsBlockedAsync(_currentUserId, _targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository.Setup(r => r.IsBlockedAsync(_targetUserId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.GetBlockStatus(_targetUserId);

        result.CanCommunicate.Should().BeFalse();
        result.Reason.Should().Be("CannotCommunicate");
    }

    [Fact]
    public async Task ReturnCanCommunicateWhenNotBlocked()
    {
        _repository.Setup(r => r.IsBlockedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetBlockStatus(_targetUserId);

        result.CanCommunicate.Should().BeTrue();
        result.Reason.Should().BeNull();
    }
}
