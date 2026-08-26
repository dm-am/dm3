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
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Blacklists;

public class UserBlacklistServiceShould : UnitTestBase
{
    private readonly IUserBlacklistRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
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
        _identityProvider.Current.Returns(identity);
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(_entryId);

        _service = new UserBlacklistService(
            _repository,
            _userRepository,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider);
    }

    [Fact]
    public async Task GetMyBlacklistForCurrentUser()
    {
        _repository.GetBlacklist(_currentUserId, Arg.Any<CancellationToken>()).Returns([]);

        await _service.GetMyBlacklist();

        await _repository.Received(1).GetBlacklist(_currentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenBlockingNonexistentUser()
    {
        _userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        var act = () => _service.Block(new OperateUserBlacklistLink { Username = "Unknown" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task ThrowWhenUserTriesToBlockThemselves()
    {
        _userRepository.FindUserIdAsync("CurrentUser").Returns(_currentUserId);

        var act = () => _service.Block(new OperateUserBlacklistLink { Username = "CurrentUser" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Нельзя заблокировать самого себя"));
    }

    [Fact]
    public async Task ReturnExistingEntryWhenAlreadyBlocked()
    {
        var existingEntry = new BlacklistEntry { Id = Guid.NewGuid() };

        _userRepository.FindUserIdAsync("Target").Returns(_targetUserId);
        _repository.Find(_currentUserId, _targetUserId, Arg.Any<CancellationToken>()).Returns(existingEntry);

        var result = await _service.Block(new OperateUserBlacklistLink { Username = "Target" });

        result.Should().Be(existingEntry);
        await _repository.DidNotReceive().Create(Arg.Any<CreateBlacklistEntryEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNewBlacklistEntry()
    {
        _userRepository.FindUserIdAsync("Target").Returns(_targetUserId);
        _repository.Find(_currentUserId, _targetUserId, Arg.Any<CancellationToken>()).Returns((BlacklistEntry?)null);

        CreateBlacklistEntryEntity? capturedEntity = null;
        _repository.Create(Arg.Any<CreateBlacklistEntryEntity>(), Arg.Any<CancellationToken>())
            .Returns(new BlacklistEntry())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateBlacklistEntryEntity>(0);
                capturedEntity = e;
            });

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

        _userRepository.FindUserIdAsync("Target").Returns(_targetUserId);
        _repository.Find(_currentUserId, _targetUserId, Arg.Any<CancellationToken>()).Returns(existingEntry);

        await _service.Unblock(new OperateUserBlacklistLink { Username = "Target" });

        await _repository.Received(1).Delete(_entryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoNothingWhenUnblockingNonexistentUser()
    {
        _userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        await _service.Unblock(new OperateUserBlacklistLink { Username = "Unknown" });

        await _repository.DidNotReceive().Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckIfUserIsBlocked()
    {
        _userRepository.FindUserIdAsync("Target").Returns(_targetUserId);
        _repository.IsBlockedAsync(_currentUserId, _targetUserId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.IsBlocked("Target");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnCannotCommunicateWhenYouBlockedThem()
    {
        _repository.IsBlockedAsync(_currentUserId, _targetUserId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.GetBlockStatus(_targetUserId);

        result.CanCommunicate.Should().BeFalse();
        result.Reason.Should().Be("YouBlockedThem");
    }

    [Fact]
    public async Task ReturnCannotCommunicateWhenTheyBlockedYou()
    {
        _repository.IsBlockedAsync(_currentUserId, _targetUserId, Arg.Any<CancellationToken>()).Returns(false);
        _repository.IsBlockedAsync(_targetUserId, _currentUserId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.GetBlockStatus(_targetUserId);

        result.CanCommunicate.Should().BeFalse();
        result.Reason.Should().Be("CannotCommunicate");
    }

    [Fact]
    public async Task ReturnCanCommunicateWhenNotBlocked()
    {
        _repository.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.GetBlockStatus(_targetUserId);

        result.CanCommunicate.Should().BeTrue();
        result.Reason.Should().BeNull();
    }
}
