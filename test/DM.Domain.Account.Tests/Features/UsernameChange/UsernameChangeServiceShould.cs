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
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IUsernameChangeMailSender> _notificationSender;
    private readonly UsernameChangeService _service;

    public UsernameChangeServiceShould()
    {
        _validator = Mock<IValidator<CreateUsernameChangeRequest>>();
        _repository = Mock<IUsernameChangeRepository>();
        _historyRepository = Mock<IUsernameHistoryRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _notificationSender = Mock<IUsernameChangeMailSender>();

        _validator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateUsernameChangeRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new UsernameChangeService(
            _validator.Object,
            _repository.Object,
            _historyRepository.Object,
            _identityProvider.Object,
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
        _repository.Setup(r => r.GetPendingByUserId(userId, It.IsAny<CancellationToken>()))
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
        _repository.Setup(r => r.GetPendingByUserId(userId, It.IsAny<CancellationToken>())).ReturnsAsync((UsernameChangeRequest?)null);

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
            It.IsAny<string>()
        ), Times.Once);
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
        _repository.Verify(r => r.UpdateUserUsername(userId, "newusername", It.IsAny<CancellationToken>()), Times.Once);
        _historyRepository.Verify(h => h.Add(It.Is<CreateUsernameHistory>(hist =>
            hist.UserId == userId &&
            hist.OldUsername == "oldusername" &&
            hist.NewUsername == "newusername"
        ), It.IsAny<CancellationToken>()), Times.Once);
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
        _repository.Verify(r => r.UpdateUserUsername(userId, "oldusername", It.IsAny<CancellationToken>()), Times.Once);
        _historyRepository.Verify(h => h.Add(It.Is<CreateUsernameHistory>(hist =>
            hist.UserId == userId &&
            hist.OldUsername == "newusername" &&
            hist.NewUsername == "oldusername"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
