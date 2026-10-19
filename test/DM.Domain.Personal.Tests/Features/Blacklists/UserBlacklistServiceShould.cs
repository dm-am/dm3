using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
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
    private readonly IUserBlacklistRepository repository;
    private readonly IUserRepository userRepository;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly UserBlacklistService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly Guid targetUserId = Guid.NewGuid();
    private readonly Guid entryId = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public UserBlacklistServiceShould()
    {
        repository = Mock<IUserBlacklistRepository>();
        userRepository = Mock<IUserRepository>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identities.User(currentUserId, "CurrentUser", UserRole.RegularUser);
        identityProvider.Current.Returns(identity);
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(entryId);

        service = new UserBlacklistService(
            repository,
            userRepository,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    [Fact]
    public async Task GetMyBlacklistForCurrentUser()
    {
        repository.GetBlacklist(currentUserId, Arg.Any<CancellationToken>()).Returns([]);

        await service.GetMyBlacklist();

        await repository.Received(1).GetBlacklist(currentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenBlockingNonexistentUser()
    {
        userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        var act = () => service.Block(new OperateUserBlacklistLink { Username = "Unknown" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task ThrowWhenUserTriesToBlockThemselves()
    {
        userRepository.FindUserIdAsync("CurrentUser").Returns(currentUserId);

        var act = () => service.Block(new OperateUserBlacklistLink { Username = "CurrentUser" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message == "Нельзя внести себя в черный список");
    }

    [Fact]
    public async Task ReturnExistingEntryWhenAlreadyBlocked()
    {
        var existingEntry = new BlacklistEntry { Id = Guid.NewGuid() };

        userRepository.FindUserIdAsync("Target").Returns(targetUserId);
        repository.Find(currentUserId, targetUserId, Arg.Any<CancellationToken>()).Returns(existingEntry);

        var result = await service.Block(new OperateUserBlacklistLink { Username = "Target" });

        result.Should().Be(existingEntry);
        await repository.DidNotReceive().Create(Arg.Any<CreateBlacklistEntryEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNewBlacklistEntry()
    {
        userRepository.FindUserIdAsync("Target").Returns(targetUserId);
        repository.Find(currentUserId, targetUserId, Arg.Any<CancellationToken>()).Returns((BlacklistEntry?)null);

        CreateBlacklistEntryEntity? capturedEntity = null;
        repository.Create(Arg.Do<CreateBlacklistEntryEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new BlacklistEntry());

        await service.Block(new OperateUserBlacklistLink { Username = "Target" });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntryId.Should().Be(entryId);
        capturedEntity.OwnerId.Should().Be(currentUserId);
        capturedEntity.BlockedUserId.Should().Be(targetUserId);
        capturedEntity.CreatedUtc.Should().Be(now);
    }

    [Fact]
    public async Task UnblockUserSuccessfully()
    {
        var existingEntry = new BlacklistEntry { Id = entryId };

        userRepository.FindUserIdAsync("Target").Returns(targetUserId);
        repository.Find(currentUserId, targetUserId, Arg.Any<CancellationToken>()).Returns(existingEntry);

        await service.Unblock(new OperateUserBlacklistLink { Username = "Target" });

        await repository.Received(1).Delete(entryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoNothingWhenUnblockingNonexistentUser()
    {
        userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        await service.Unblock(new OperateUserBlacklistLink { Username = "Unknown" });

        await repository.DidNotReceive().Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
