using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Account.Tests.Dsl;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.UsernameChange;

public class UsernameChangeServiceShould : UsernameChangeServiceTestBase
{
    [Fact]
    public async Task ThrowWhenUserIsNotAuthenticated()
    {
        var request = new CreateUsernameChangeRequest
        {
            Reason = "Test reason"
        };

        identityProvider.Current.Returns(Identity.Guest());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CreateAsync(request));

        exception.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThrowWhenPendingRequestAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, "testuser");
        var request = new CreateUsernameChangeRequest
        {
            Reason = "Test reason"
        };

        identityProvider.Current.Returns(identity);
        repository.GetActiveByUserId(userId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new UsernameChangeRequest());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CreateAsync(request));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUsernameChangeRequestSuccessfully()
    {
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, "testuser");
        var request = new CreateUsernameChangeRequest
        {
            Reason = "Want to change username"
        };

        identityProvider.Current.Returns(identity);
        repository.GetActiveByUserId(userId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((UsernameChangeRequest?)null);

        var result = await service.CreateAsync(request);

        result.UserId.Should().Be(userId);
        result.CurrentUsername.Should().Be("testuser");
        result.Reason.Should().Be(request.Reason);
        result.Status.Should().Be(UsernameChangeRequestStatus.Pending);
        await repository.Received(1).Add(Arg.Is<UsernameChangeRequest>(req =>
            req.UserId == userId &&
            req.Reason == request.Reason &&
            req.Status == UsernameChangeRequestStatus.Pending
        ), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveRequestAndSendNotificationEmail()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var approvalToken = Guid.NewGuid();
        var request = new UsernameChangeRequest
        {
            RequestId = requestId,
            UserId = userId,
            Status = UsernameChangeRequestStatus.Pending,
            UserEmail = "user@example.com",
            UserUsername = "testuser"
        };
        var moderator = Identities.User(moderatorId, "moderator", UserRole.Admin);
        var resolve = new ResolveUsernameChangeRequest
        {
            RequestId = requestId,
            Status = UsernameChangeRequestStatus.Approved,
            Comment = "Approved"
        };

        identityProvider.Current.Returns(moderator);
        repository.GetById(requestId, Arg.Any<CancellationToken>()).Returns(request);

        var result = await service.ResolveAsync(resolve);

        result.Status.Should().Be(UsernameChangeRequestStatus.Approved);
        request.ApprovalToken.Should().NotBeNull();
        await repository.Received(1).Update(Arg.Is<UsernameChangeRequest>(req =>
            req.Status == UsernameChangeRequestStatus.Approved &&
            req.ResolvedByUserId == moderatorId &&
            req.ApprovalToken != null
        ), Arg.Any<CancellationToken>());
        await notificationSender.Received(1).SendApprovalAsync(
            request.UserEmail,
            request.ApprovalToken!.Value
        );
    }

    /// <summary>The approval lives the hours the settings give it, the hours its letter names.</summary>
    [Fact]
    public async Task LetTheApprovalLiveTheConfiguredHours()
    {
        var now = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var request = new UsernameChangeRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = UsernameChangeRequestStatus.Pending,
            UserEmail = "user@example.com",
            UserUsername = "testuser"
        };
        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "moderator", UserRole.Admin));
        repository.GetById(request.RequestId, Arg.Any<CancellationToken>()).Returns(request);
        dateTimeProvider.Now.Returns(now);

        await service.ResolveAsync(new ResolveUsernameChangeRequest
        {
            RequestId = request.RequestId,
            Status = UsernameChangeRequestStatus.Approved
        });

        request.ApprovalTokenExpiresUtc.Should().Be(now.AddHours(tokens.UsernameChangeApprovalTokenLifetimeHours));
    }

    /// <summary>
    /// Resolving is approving or rejecting; the two statuses the flow reaches on its
    /// own are not a moderator's to assert.
    /// </summary>
    [Theory]
    [InlineData(UsernameChangeRequestStatus.Pending)]
    [InlineData(UsernameChangeRequestStatus.Completed)]
    [InlineData(UsernameChangeRequestStatus.Expired)]
    public async Task RefuseAStatusThatIsNeitherApprovalNorRejection(UsernameChangeRequestStatus status)
    {
        var requestId = Guid.NewGuid();
        var request = new UsernameChangeRequest
        {
            RequestId = requestId,
            UserId = Guid.NewGuid(),
            Status = UsernameChangeRequestStatus.Pending,
            UserEmail = "user@example.com",
            UserUsername = "testuser"
        };
        var moderator = Identities.User(Guid.NewGuid(), "moderator", UserRole.Admin);

        identityProvider.Current.Returns(moderator);
        repository.GetById(requestId, Arg.Any<CancellationToken>()).Returns(request);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => service.ResolveAsync(new ResolveUsernameChangeRequest
            {
                RequestId = requestId,
                Status = status
            }));

        exception.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        request.Status.Should().Be(UsernameChangeRequestStatus.Pending,
            "a refused resolution leaves the request where the moderator found it");
        await repository.DidNotReceive().Update(Arg.Any<UsernameChangeRequest>(), Arg.Any<CancellationToken>());
        notificationSender.ShouldHaveReceivedNoCalls();
    }

    [Fact]
    public async Task CompleteUsernameChangeWithValidToken()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = new UsernameChangeRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = userId,
            Status = UsernameChangeRequestStatus.Approved,
            ApprovalToken = tokenId,
            ApprovalTokenExpiresUtc = now.AddDays(1),
            UserUsername = "oldusername"
        };

        repository.GetByApprovalToken(tokenId, Arg.Any<CancellationToken>()).Returns(request);
        repository.IsUsernameAvailable("newusername", Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);
        historyRepository.IsUsernameReservedForOthers("newusername", userId, Arg.Any<CancellationToken>())
            .Returns(false);
        dateTimeProvider.Now.Returns(now);

        var result = await service.CompleteWithTokenAsync(tokenId, "newusername");

        result.RequestedUsername.Should().Be("newusername");
        result.Status.Should().Be(UsernameChangeRequestStatus.Completed);
        // One call, because the three rows go in together: separately, a refusal in
        // between could leave the user renamed with the request still pending, and
        // one approval then buys a second rename.
        await repository.Received(1).ApplyRename(
            Arg.Is<UsernameChangeRequest>(req =>
                req.UserId == userId &&
                req.Status == UsernameChangeRequestStatus.Completed &&
                // The token stays on the row, so a spent link is told apart from one nobody was issued.
                req.ApprovalToken == tokenId),
            Arg.Is<CreateUsernameHistory>(hist =>
                hist.UserId == userId &&
                hist.OldUsername == "oldusername" &&
                hist.NewUsername == "newusername"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Puts a request behind an approval token and hands the token back.
    /// </summary>
    /// <remarks>
    /// The expiry stays on the row after the request leaves Approved, so the order of the reads decides the answer.
    /// </remarks>
    private Guid IssuedApproval(UsernameChangeRequestStatus status, DateTimeOffset expiresUtc, Guid? userId = null)
    {
        var token = Guid.NewGuid();
        repository.GetByApprovalToken(token, Arg.Any<CancellationToken>()).Returns(new UsernameChangeRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Status = status,
            ApprovalToken = token,
            ApprovalTokenExpiresUtc = expiresUtc,
            UserUsername = "reader"
        });
        return token;
    }

    /// <summary>
    /// The state the form opens in, and the only one that names the account.
    /// </summary>
    [Fact]
    public async Task ReportTheApprovalAsReadyWhileItStands()
    {
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Approved, now.AddHours(1));

        var info = await service.GetApprovalInfoAsync(token);

        info!.Status.Should().Be("ready");
        info.CurrentUsername.Should().Be("reader", "the form says which name is being changed");
    }

    /// <summary>
    /// Both ways of running out, and one answer for them: the hourly pass has moved
    /// the request to Expired, or it has not run yet and only the stored moment
    /// says so.
    /// </summary>
    [Theory]
    [InlineData(UsernameChangeRequestStatus.Approved)]
    [InlineData(UsernameChangeRequestStatus.Expired)]
    public async Task ReportTheApprovalAsExpiredOnceItsWindowClosed(UsernameChangeRequestStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);
        var token = IssuedApproval(status, now.AddHours(-1));

        var info = await service.GetApprovalInfoAsync(token);

        info!.Status.Should().Be("expired");
        info.CurrentUsername.Should().BeNull("a dead link says nothing about the account");
    }

    /// <summary>
    /// A spent link, opened after its 48 hours are up — which is the ordinary case,
    /// since a letter is reread days later.
    /// </summary>
    [Fact]
    public async Task ReportTheApprovalAsUsedWhenTheNameWasAlreadyChangedThroughIt()
    {
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Completed, now.AddHours(-1));

        var info = await service.GetApprovalInfoAsync(token);

        info!.Status.Should().Be("used");
        info.CurrentUsername.Should().BeNull();
    }

    [Fact]
    public async Task ReportNothingForATokenNoRequestWasIssuedFor()
    {
        var token = Guid.NewGuid();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        repository.GetByApprovalToken(token, Arg.Any<CancellationToken>()).Returns((UsernameChangeRequest?)null);

        (await service.GetApprovalInfoAsync(token)).Should().BeNull();
    }

    /// <summary>
    /// Approval and choice are days apart, so the name can be gone in between —
    /// taken by another account, or reserved by someone's history.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RefuseANameTakenSinceTheApproval(bool available, bool reservedForOthers)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        dateTimeProvider.Now.Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Approved, now.AddHours(1), userId);
        repository.IsUsernameAvailable("taken", Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(available);
        historyRepository.IsUsernameReservedForOthers("taken", userId, Arg.Any<CancellationToken>())
            .Returns(reservedForOthers);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CompleteWithTokenAsync(token, "taken"));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Contain("занято");
        await repository.DidNotReceive().ApplyRename(
            Arg.Any<UsernameChangeRequest>(), Arg.Any<CreateUsernameHistory>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A name the storage refuses is refused the way a name found taken is, and
    /// not as a failure of the storage engine.
    /// </summary>
    /// <remarks>
    /// The checks are reads, so either unique structure over the rename may still refuse the write.
    /// </remarks>
    [Fact]
    public async Task RefuseARenameTheStorageWouldNotTake()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        dateTimeProvider.Now.Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Approved, now.AddHours(1), userId);
        repository.IsUsernameAvailable("newusername", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        historyRepository.IsUsernameReservedForOthers("newusername", userId, Arg.Any<CancellationToken>())
            .Returns(false);
        repository.ApplyRename(
                Arg.Any<UsernameChangeRequest>(), Arg.Any<CreateUsernameHistory>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Duplicate username or username history entry"));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CompleteWithTokenAsync(token, "newusername"));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Contain("занято");
    }

    /// <summary>
    /// The same letter followed twice. One approval buys one rename, and the second
    /// attempt has to say which of the two refusals it is.
    /// </summary>
    [Fact]
    public async Task RefuseALinkWhoseNameWasAlreadyChosen()
    {
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Completed, now.AddHours(-1));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CompleteWithTokenAsync(token, "newusername"));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Contain("уже изменено");
        await repository.DidNotReceive().ApplyRename(
            Arg.Any<UsernameChangeRequest>(), Arg.Any<CreateUsernameHistory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseALinkThePassAlreadyWithdrew()
    {
        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Now.Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Expired, now.AddHours(-1));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CompleteWithTokenAsync(token, "newusername"));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Message.Should().Contain("истек");
    }

    [Fact]
    public async Task ThrowWhenCompletingWithExpiredToken()
    {
        var tokenId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = new UsernameChangeRequest
        {
            Status = UsernameChangeRequestStatus.Approved,
            ApprovalToken = tokenId,
            ApprovalTokenExpiresUtc = now.AddDays(-1)
        };

        repository.GetByApprovalToken(tokenId, Arg.Any<CancellationToken>()).Returns(request);
        dateTimeProvider.Now.Returns(now);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CompleteWithTokenAsync(tokenId, "newusername"));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Message.Should().Contain("истек");
    }

    /// <summary>
    /// An approval the requester has not spent is a change already granted, so
    /// asking for a second one must be refused.
    /// </summary>
    [Fact]
    public async Task RefuseASecondRequestWhileAnApprovalIsStillUnspent()
    {
        var userId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        repository.GetActiveByUserId(userId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new UsernameChangeRequest { Status = UsernameChangeRequestStatus.Approved });

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.CreateAsync(new CreateUsernameChangeRequest { Reason = "Test reason" }));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Be(RefusalMessage.UsernameChangeAlreadyApproved);
    }

    /// <summary>
    /// Both terminal states hold nothing: expiry granted no name and completion
    /// spent its grant. Asking again is the only way forward from either, and it
    /// is what the queue already accepts.
    /// </summary>
    [Theory]
    [InlineData(UsernameChangeRequestStatus.Expired)]
    [InlineData(UsernameChangeRequestStatus.Completed)]
    [InlineData(UsernameChangeRequestStatus.Rejected)]
    public async Task AcceptANewRequestAfterAFinishedOne(UsernameChangeRequestStatus finished)
    {
        var userId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        // A finished request is not active, so the guard never sees it.
        repository.GetActiveByUserId(userId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((UsernameChangeRequest?)null);
        repository.GetLatestByUserId(userId, Arg.Any<CancellationToken>())
            .Returns(new UsernameChangeRequest { Status = finished });

        var result = await service.CreateAsync(new CreateUsernameChangeRequest { Reason = "Test reason" });

        result.Status.Should().Be(UsernameChangeRequestStatus.Pending);
    }

    /// <summary>
    /// Expiry has two roads with one status, and the requester is owed the
    /// difference: nobody read the request, or the approval went unused.
    /// </summary>
    [Fact]
    public async Task TellAnUnreviewedExpiryFromALapsedApproval()
    {
        var userId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));

        repository.GetLatestByUserId(userId, Arg.Any<CancellationToken>()).Returns(new UsernameChangeRequest
        {
            Status = UsernameChangeRequestStatus.Expired,
            ApprovalTokenExpiresUtc = null
        });
        var unreviewed = await service.GetCurrentUserRequestAsync();
        unreviewed!.ExpiryReason.Should().Be(UsernameChangeExpiryReason.Unreviewed);

        repository.GetLatestByUserId(userId, Arg.Any<CancellationToken>()).Returns(new UsernameChangeRequest
        {
            Status = UsernameChangeRequestStatus.Expired,
            ApprovalTokenExpiresUtc = DateTimeOffset.UnixEpoch
        });
        var lapsed = await service.GetCurrentUserRequestAsync();
        lapsed!.ExpiryReason.Should().Be(UsernameChangeExpiryReason.ApprovalLapsed);
    }

    /// <summary>
    /// The reason is about expiry alone: a live approval carries the same token
    /// expiry field and must not be labelled with it.
    /// </summary>
    [Fact]
    public async Task LeaveTheExpiryReasonUnsetOnEveryOtherStatus()
    {
        var userId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        repository.GetLatestByUserId(userId, Arg.Any<CancellationToken>()).Returns(new UsernameChangeRequest
        {
            Status = UsernameChangeRequestStatus.Approved,
            ApprovalTokenExpiresUtc = DateTimeOffset.UnixEpoch
        });

        var result = await service.GetCurrentUserRequestAsync();

        result!.ExpiryReason.Should().BeNull();
    }

    /// <summary>
    /// The moment handed to the repository is the service's clock, so the gate and
    /// the rest of the flow judge the same deadline by the same time.
    /// </summary>
    [Fact]
    public async Task JudgeTheApprovalDeadlineByTheServiceClock()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        dateTimeProvider.Now.Returns(now);
        repository.GetActiveByUserId(
                userId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns((UsernameChangeRequest?)null);

        await service.CreateAsync(new CreateUsernameChangeRequest { Reason = "Test reason" });

        await repository.Received(1).GetActiveByUserId(
            userId, now, Arg.Any<CancellationToken>());
    }
}
