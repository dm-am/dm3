using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Authorization;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Account.Tests.Dsl;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.UsernameChange;

// Taking back a completed username change: a rename of somebody else, done by a moderator, so it is
// the one path of the service that asks for a permission.
public class UsernameChangeRollbackShould : UsernameChangeServiceTestBase
{
    private void SetupCompletedChange(Guid requestId, Guid userId, UserRole role)
    {
        var request = new UsernameChangeRequest
        {
            RequestId = requestId,
            UserId = userId,
            Status = UsernameChangeRequestStatus.Completed,
            UserUsername = "newusername"
        };
        var history = new UsernameHistoryEntry
        {
            OldUsername = "oldusername",
            NewUsername = "newusername"
        };
        var moderator = Identities.User(Guid.NewGuid(), "moderator", role);

        identityProvider.Current.Returns(moderator);
        repository.GetById(requestId, Arg.Any<CancellationToken>()).Returns(request);
        historyRepository.GetLatestByUserId(userId, Arg.Any<CancellationToken>()).Returns(history);
        repository.IsUsernameAvailable("oldusername", userId, Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public async Task RollbackUsernameChange()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupCompletedChange(requestId, userId, UserRole.Admin);

        var result = await service.RollbackAsync(requestId);

        result.Status.Should().Be(UsernameChangeRequestStatus.Rejected);
        intentionManager.Received(1).ThrowIfForbidden(AccountIntention.RollbackUsernameChange);
        await repository.Received(1).ApplyRename(
            Arg.Is<UsernameChangeRequest>(req =>
                req.UserId == userId &&
                req.Status == UsernameChangeRequestStatus.Rejected),
            Arg.Is<CreateUsernameHistory>(hist =>
                hist.UserId == userId &&
                hist.OldUsername == "newusername" &&
                hist.NewUsername == "oldusername"),
            Arg.Any<CancellationToken>());
    }

    // The method sat behind no check of its own, so any caller wired to it would rename a user back
    // on behalf of whoever asked.
    [Fact]
    public async Task RefuseARollbackToWhoeverMayNotTakeItBack()
    {
        var requestId = Guid.NewGuid();
        SetupCompletedChange(requestId, Guid.NewGuid(), UserRole.RegularUser);
        intentionManager
            .When(manager => manager.ThrowIfForbidden(AccountIntention.RollbackUsernameChange))
            .Do(_ => throw new IntentionManagerException(
                AuthenticatedUser.Guest, AccountIntention.RollbackUsernameChange));

        var exception = await Assert.ThrowsAsync<IntentionManagerException>(
            () => service.RollbackAsync(requestId));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().ApplyRename(
            Arg.Any<UsernameChangeRequest>(), Arg.Any<CreateUsernameHistory>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The rollback under the answer the rename already gives when the storage
    /// refuses the write.
    /// </summary>
    [Fact]
    public async Task RefuseARollbackTheStorageWouldNotTake()
    {
        var requestId = Guid.NewGuid();
        SetupCompletedChange(requestId, Guid.NewGuid(), UserRole.Admin);
        repository.ApplyRename(
                Arg.Any<UsernameChangeRequest>(), Arg.Any<CreateUsernameHistory>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Duplicate username or username history entry"));

        var exception = await Assert.ThrowsAsync<HttpException>(() => service.RollbackAsync(requestId));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Contain("Откат невозможен");
    }
}
