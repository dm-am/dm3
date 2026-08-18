using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.UsernameChange;

public class UsernameChangeServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateUsernameChangeRequest>> _validator;
    private readonly Mock<IUsernameChangeRepository> _repository;
    private readonly Mock<IUsernameHistoryRepository> _historyRepository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IUsernameChangeMailSender> _notificationSender;
    private readonly UsernameChangeService _service;

    public UsernameChangeServiceShould()
    {
        _validator = Mock<IValidator<CreateUsernameChangeRequest>>();
        _repository = Mock<IUsernameChangeRepository>();
        _historyRepository = Mock<IUsernameHistoryRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _notificationSender = Mock<IUsernameChangeMailSender>();

        _validator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateUsernameChangeRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);
        // A distinct value per call: the approval token and the history id are issued
        // in one and the same flow, and a single fixed guid would hide a swap of them.
        _guidFactory.Setup(g => g.Create()).Returns(() => Guid.NewGuid());

        _service = new UsernameChangeService(
            _validator.Object,
            _repository.Object,
            _historyRepository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _notificationSender.Object);
    }

    [Fact]
    public async Task ThrowWhenUserIsNotAuthenticated()
    {
        var request = new CreateUsernameChangeRequest
        {
            Reason = "Test reason"
        };

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.CreateAsync(request));

        exception.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThrowWhenPendingRequestAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Username = "testuser", Role = UserRole.RegularUser },
            new Session(),
            UserSettings.Default,
            "token");
        var request = new CreateUsernameChangeRequest
        {
            Reason = "Test reason"
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetActiveByUserId(userId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsernameChangeRequest());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.CreateAsync(request));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUsernameChangeRequestSuccessfully()
    {
        var userId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Username = "testuser", Role = UserRole.RegularUser },
            new Session(),
            UserSettings.Default,
            "token");
        var request = new CreateUsernameChangeRequest
        {
            Reason = "Want to change username"
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetActiveByUserId(userId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync((UsernameChangeRequest?)null);

        var result = await _service.CreateAsync(request);

        result.UserId.Should().Be(userId);
        result.CurrentUsername.Should().Be("testuser");
        result.Reason.Should().Be(request.Reason);
        result.Status.Should().Be(UsernameChangeRequestStatus.Pending);
        _repository.Verify(r => r.Add(It.Is<UsernameChangeRequest>(req =>
            req.UserId == userId &&
            req.Reason == request.Reason &&
            req.Status == UsernameChangeRequestStatus.Pending
        ), It.IsAny<CancellationToken>()), Times.Once);
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
        var moderator = Identity.Success(
            new AuthenticatedUser { UserId = moderatorId, Username = "moderator", Role = UserRole.Admin },
            new Session(),
            UserSettings.Default,
            "token");
        var resolve = new ResolveUsernameChangeRequest
        {
            RequestId = requestId,
            Status = UsernameChangeRequestStatus.Approved,
            Comment = "Approved"
        };

        _identityProvider.Setup(p => p.Current).Returns(moderator);
        _repository.Setup(r => r.GetById(requestId, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.ResolveAsync(resolve);

        result.Status.Should().Be(UsernameChangeRequestStatus.Approved);
        request.ApprovalToken.Should().NotBeNull();
        _repository.Verify(r => r.Update(It.Is<UsernameChangeRequest>(req =>
            req.Status == UsernameChangeRequestStatus.Approved &&
            req.ResolvedByUserId == moderatorId &&
            req.ApprovalToken != null
        ), It.IsAny<CancellationToken>()), Times.Once);
        _notificationSender.Verify(n => n.SendApprovalAsync(
            request.UserEmail,
            request.UserUsername!,
            request.ApprovalToken!.Value
        ), Times.Once);
    }

    /// <summary>
    /// Resolving is approving or rejecting; the two statuses the flow reaches on its
    /// own are not a moderator's to assert.
    /// </summary>
    /// <remarks>
    /// The status arrives in the body and used to be written through unchecked. A
    /// request filed as Completed left the moderation queue looking finished while
    /// the name stayed as it was and no approval link had been issued - and the user
    /// could only find out by submitting another request.
    /// </remarks>
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
        var moderator = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Username = "moderator", Role = UserRole.Admin },
            new Session(),
            UserSettings.Default,
            "token");

        _identityProvider.Setup(p => p.Current).Returns(moderator);
        _repository.Setup(r => r.GetById(requestId, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.ResolveAsync(new ResolveUsernameChangeRequest
            {
                RequestId = requestId,
                Status = status
            }));

        exception.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        request.Status.Should().Be(UsernameChangeRequestStatus.Pending,
            "a refused resolution leaves the request where the moderator found it");
        _repository.Verify(r => r.Update(It.IsAny<UsernameChangeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notificationSender.VerifyNoOtherCalls();
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

        _repository.Setup(r => r.GetByApprovalToken(tokenId, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repository.Setup(r => r.IsUsernameAvailable("newusername", It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _historyRepository.Setup(h => h.IsUsernameReservedForOthers("newusername", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var result = await _service.CompleteWithTokenAsync(tokenId, "newusername");

        result.RequestedUsername.Should().Be("newusername");
        result.Status.Should().Be(UsernameChangeRequestStatus.Completed);
        // One call, because the three rows go in together: separately, a refusal in
        // between could leave the user renamed with the request still pending, and
        // one approval then buys a second rename.
        _repository.Verify(r => r.ApplyRename(
            It.Is<UsernameChangeRequest>(req =>
                req.UserId == userId &&
                req.Status == UsernameChangeRequestStatus.Completed &&
                // The token stays on the row. Completed is what spends the approval,
                // and erasing the value the letter carries is what made a spent link
                // and a link nobody was issued the same row-less lookup - so the
                // person whose name had just changed was told the link was bad.
                req.ApprovalToken == tokenId),
            It.Is<CreateUsernameHistory>(hist =>
                hist.UserId == userId &&
                hist.OldUsername == "oldusername" &&
                hist.NewUsername == "newusername"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Puts a request behind an approval token and hands the token back.
    /// </summary>
    /// <remarks>
    /// The expiry moment is the one the approval carried, and stays on the row
    /// after the request leaves Approved — which is why a spent link opened later
    /// is both Completed and past its window, and the order the two are read in
    /// decides what its holder is told.
    /// </remarks>
    private Guid IssuedApproval(UsernameChangeRequestStatus status, DateTimeOffset expiresUtc, Guid? userId = null)
    {
        var token = Guid.NewGuid();
        _repository.Setup(r => r.GetByApprovalToken(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsernameChangeRequest
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
        _dateTimeProvider.Setup(d => d.Now).Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Approved, now.AddHours(1));

        var info = await _service.GetApprovalInfoAsync(token);

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
        _dateTimeProvider.Setup(d => d.Now).Returns(now);
        var token = IssuedApproval(status, now.AddHours(-1));

        var info = await _service.GetApprovalInfoAsync(token);

        info!.Status.Should().Be("expired");
        info.CurrentUsername.Should().BeNull("a dead link says nothing about the account");
    }

    /// <summary>
    /// A spent link, opened after its 48 hours are up — which is the ordinary case,
    /// since a letter is reread days later.
    /// </summary>
    /// <remarks>
    /// This and the expired case both used to answer null and reach the reader as
    /// one 404: the person whose name had in fact been changed was told the link
    /// was invalid, and had nothing to do about it but ask again.
    /// </remarks>
    [Fact]
    public async Task ReportTheApprovalAsUsedWhenTheNameWasAlreadyChangedThroughIt()
    {
        var now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Setup(d => d.Now).Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Completed, now.AddHours(-1));

        var info = await _service.GetApprovalInfoAsync(token);

        info!.Status.Should().Be("used");
        info.CurrentUsername.Should().BeNull();
    }

    [Fact]
    public async Task ReportNothingForATokenNoRequestWasIssuedFor()
    {
        var token = Guid.NewGuid();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);
        _repository.Setup(r => r.GetByApprovalToken(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsernameChangeRequest?)null);

        (await _service.GetApprovalInfoAsync(token)).Should().BeNull();
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
        _dateTimeProvider.Setup(d => d.Now).Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Approved, now.AddHours(1), userId);
        _repository.Setup(r => r.IsUsernameAvailable("taken", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(available);
        _historyRepository.Setup(h => h.IsUsernameReservedForOthers("taken", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservedForOthers);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.CompleteWithTokenAsync(token, "taken"));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Contain("занято");
        _repository.Verify(r => r.ApplyRename(
            It.IsAny<UsernameChangeRequest>(), It.IsAny<CreateUsernameHistory>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The same letter followed twice. One approval buys one rename, and the second
    /// attempt has to say which of the two refusals it is.
    /// </summary>
    [Fact]
    public async Task RefuseALinkWhoseNameWasAlreadyChosen()
    {
        var now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Setup(d => d.Now).Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Completed, now.AddHours(-1));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.CompleteWithTokenAsync(token, "newusername"));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Contain("уже изменено");
        _repository.Verify(r => r.ApplyRename(
            It.IsAny<UsernameChangeRequest>(), It.IsAny<CreateUsernameHistory>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefuseALinkThePassAlreadyWithdrew()
    {
        var now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Setup(d => d.Now).Returns(now);
        var token = IssuedApproval(UsernameChangeRequestStatus.Expired, now.AddHours(-1));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.CompleteWithTokenAsync(token, "newusername"));

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

        _repository.Setup(r => r.GetByApprovalToken(tokenId, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.CompleteWithTokenAsync(tokenId, "newusername"));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Message.Should().Contain("истек");
    }

    [Fact]
    public async Task RollbackUsernameChange()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
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
        var moderator = Identity.Success(
            new AuthenticatedUser { UserId = moderatorId, Username = "moderator", Role = UserRole.Admin },
            new Session(),
            UserSettings.Default,
            "token");

        _identityProvider.Setup(p => p.Current).Returns(moderator);
        _repository.Setup(r => r.GetById(requestId, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _historyRepository.Setup(h => h.GetLatestByUserId(userId, It.IsAny<CancellationToken>())).ReturnsAsync(history);
        _repository.Setup(r => r.IsUsernameAvailable("oldusername", userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.RollbackAsync(requestId);

        result.Status.Should().Be(UsernameChangeRequestStatus.Rejected);
        _repository.Verify(r => r.ApplyRename(
            It.Is<UsernameChangeRequest>(req =>
                req.UserId == userId &&
                req.Status == UsernameChangeRequestStatus.Rejected),
            It.Is<CreateUsernameHistory>(hist =>
                hist.UserId == userId &&
                hist.OldUsername == "newusername" &&
                hist.NewUsername == "oldusername"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// An approval the requester has not spent is a change already granted, so
    /// asking for a second one must be refused - the code said so in a comment
    /// long before the query did.
    /// </summary>
    [Fact]
    public async Task RefuseASecondRequestWhileAnApprovalIsStillUnspent()
    {
        var userId = Guid.NewGuid();
        _identityProvider.Setup(p => p.Current).Returns(AuthenticatedAs(userId));
        _repository.Setup(r => r.GetActiveByUserId(userId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsernameChangeRequest { Status = UsernameChangeRequestStatus.Approved });

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.CreateAsync(new CreateUsernameChangeRequest { Reason = "Test reason" }));

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
        _identityProvider.Setup(p => p.Current).Returns(AuthenticatedAs(userId));
        // A finished request is not active, so the guard never sees it.
        _repository.Setup(r => r.GetActiveByUserId(userId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsernameChangeRequest?)null);
        _repository.Setup(r => r.GetLatestByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsernameChangeRequest { Status = finished });

        var result = await _service.CreateAsync(new CreateUsernameChangeRequest { Reason = "Test reason" });

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
        _identityProvider.Setup(p => p.Current).Returns(AuthenticatedAs(userId));

        _repository.Setup(r => r.GetLatestByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsernameChangeRequest
            {
                Status = UsernameChangeRequestStatus.Expired,
                ApprovalTokenExpiresUtc = null
            });
        var unreviewed = await _service.GetCurrentUserRequestAsync();
        unreviewed!.ExpiryReason.Should().Be(UsernameChangeExpiryReason.Unreviewed);

        _repository.Setup(r => r.GetLatestByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsernameChangeRequest
            {
                Status = UsernameChangeRequestStatus.Expired,
                ApprovalTokenExpiresUtc = DateTimeOffset.UnixEpoch
            });
        var lapsed = await _service.GetCurrentUserRequestAsync();
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
        _identityProvider.Setup(p => p.Current).Returns(AuthenticatedAs(userId));
        _repository.Setup(r => r.GetLatestByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsernameChangeRequest
            {
                Status = UsernameChangeRequestStatus.Approved,
                ApprovalTokenExpiresUtc = DateTimeOffset.UnixEpoch
            });

        var result = await _service.GetCurrentUserRequestAsync();

        result!.ExpiryReason.Should().BeNull();
    }

    /// <summary>
    /// The gate reads the stored deadline, not the status: Approved keeps that
    /// status until the hourly pass moves it, and for that hour the link from the
    /// letter is already dead. Refusing a new request there tells the requester to
    /// use a link that answers "expired" and leaves them nothing else to do.
    /// </summary>
    [Fact]
    public async Task AcceptANewRequestWhenTheApprovalLinkHasRunOut()
    {
        var userId = Guid.NewGuid();
        _identityProvider.Setup(p => p.Current).Returns(AuthenticatedAs(userId));
        // The repository decides this by comparing against the moment it is given,
        // so a lapsed approval is simply not among the rows it returns.
        _repository.Setup(r => r.GetActiveByUserId(
                userId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsernameChangeRequest?)null);

        var result = await _service.CreateAsync(new CreateUsernameChangeRequest { Reason = "Test reason" });

        result.Status.Should().Be(UsernameChangeRequestStatus.Pending);
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
        _identityProvider.Setup(p => p.Current).Returns(AuthenticatedAs(userId));
        _dateTimeProvider.Setup(d => d.Now).Returns(now);
        _repository.Setup(r => r.GetActiveByUserId(
                userId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsernameChangeRequest?)null);

        await _service.CreateAsync(new CreateUsernameChangeRequest { Reason = "Test reason" });

        _repository.Verify(r => r.GetActiveByUserId(
            userId, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IIdentity AuthenticatedAs(Guid userId) => Identity.Success(
        new AuthenticatedUser { UserId = userId, Username = "testuser", Role = UserRole.RegularUser },
        new Session(),
        UserSettings.Default,
        "token");
}
