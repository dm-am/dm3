using System;
using DM.Domain.Core.Tokens;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class ActivationServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<ActivationRequest>> _validator;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IActivationRepository> _repository;
    private readonly Mock<IUserFactory> _userFactory;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IRecoveryService> _recoveryService;
    private readonly ActivationService _service;

    public ActivationServiceShould()
    {
        _validator = Mock<IValidator<ActivationRequest>>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _repository = Mock<IActivationRepository>();
        _userFactory = Mock<IUserFactory>();
        _producer = Mock<IEventProducer>();
        _recoveryService = Mock<IRecoveryService>();
        var config = Options.Create(new TokenConfiguration
        {
            ActivationTokenLifetimeHours = 48
        });

        _validator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<ActivationRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new ActivationService(
            _validator.Object,
            _dateTimeProvider.Object,
            _repository.Object,
            _userFactory.Object,
            _producer.Object,
            _recoveryService.Object,
            config);
    }

    [Fact]
    public async Task ThrowWhenTokenNotFound()
    {
        var request = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = "newuser"
        };

        _repository.Setup(r => r.FindPendingByToken(request.Token, It.IsAny<CancellationToken>())).ReturnsAsync((PendingRegistration?)null);
        _repository.Setup(r => r.FindUserByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AuthenticatedUser?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Activate(request));

        exception.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task ThrowWhenTokenIsExpired()
    {
        var request = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = "newuser"
        };
        var now = DateTimeOffset.UtcNow;
        var pending = new PendingRegistration
        {
            Email = "test@example.com",
            TokenCreatedUtc = now.AddDays(-3)
        };

        _repository.Setup(r => r.FindPendingByToken(request.Token, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Activate(request));

        exception.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task CreateUserAndCompleteActivation()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new ActivationRequest
        {
            Token = tokenId,
            Username = "newuser"
        };
        var now = DateTimeOffset.UtcNow;
        var pending = new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            Email = "test@example.com",
            SecretHash = ConfirmationSecret.Hash(tokenId),
            TokenCreatedUtc = now.AddHours(-1)
        };
        var user = new CreateUser
        {
            UserId = userId,
            Username = "newuser",
            Email = pending.Email
        };

        _repository.Setup(r => r.FindPendingByToken(tokenId, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _userFactory.Setup(f => f.CreateFromPending(pending, request.Username)).Returns(user);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var result = await _service.Activate(request);

        result.Should().Be(userId);
        _repository.Verify(r => r.CompleteActivation(user, pending.PendingRegistrationId), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.ActivatedUser, userId), Times.Once);
    }

    [Fact]
    public async Task ReturnExistingUserIdForIdempotentRetry()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new ActivationRequest
        {
            Token = tokenId,
            Username = "newuser",
            RetryEmail = "test@example.com"
        };
        var existingUser = new AuthenticatedUser
        {
            UserId = userId,
            Username = "newuser",
            Email = "test@example.com"
        };

        _repository.Setup(r => r.FindPendingByToken(tokenId, It.IsAny<CancellationToken>())).ReturnsAsync((PendingRegistration?)null);
        _repository.Setup(r => r.FindUserByEmail(request.RetryEmail, It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);

        var result = await _service.Activate(request);

        result.Should().Be(userId);
        _repository.Verify(r => r.CompleteActivation(It.IsAny<CreateUser>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ReturnPendingInfoWhenTokenIsValid()
    {
        var tokenId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var pending = new PendingRegistration
        {
            Email = "test@example.com",
            SecretHash = ConfirmationSecret.Hash(tokenId),
            TokenCreatedUtc = now.AddHours(-1)
        };

        _repository.Setup(r => r.FindPendingByToken(tokenId, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var result = await _service.GetPendingInfo(tokenId);

        result.Should().NotBeNull();
        result!.Email.Should().Be(pending.Email);
        result.Status.Should().Be("ready");
    }

    [Fact]
    public async Task DelegateToRecoveryServiceForActivationResend()
    {
        var email = "test@example.com";
        _recoveryService.Setup(r => r.Recover(email)).ReturnsAsync(RecoveryResult.ActivationResent);

        var result = await _service.ResendActivation(email);

        result.Should().BeTrue();
        _recoveryService.Verify(r => r.Recover(email), Times.Once);
    }
}
