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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class ActivationServiceShould : UnitTestBase
{
    private readonly IValidator<ActivationRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IActivationRepository _repository;
    private readonly IUserFactory _userFactory;
    private readonly IEventProducer _producer;
    private readonly IRecoveryService _recoveryService;
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

        _validator.ValidateAsync(
                Arg.Any<ValidationContext<ActivationRequest>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new ActivationService(
            _validator,
            _dateTimeProvider,
            _repository,
            _userFactory,
            _producer,
            _recoveryService,
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

        _repository.FindPendingByToken(request.Token, Arg.Any<CancellationToken>()).Returns((PendingRegistration?)null);
        _repository.FindUserByEmail(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AuthenticatedUser?)null);

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

        _repository.FindPendingByToken(request.Token, Arg.Any<CancellationToken>()).Returns(pending);
        _dateTimeProvider.Now.Returns(now);

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

        _repository.FindPendingByToken(tokenId, Arg.Any<CancellationToken>()).Returns(pending);
        _userFactory.CreateFromPending(pending, request.Username).Returns(user);
        _dateTimeProvider.Now.Returns(now);

        var result = await _service.Activate(request);

        result.Should().Be(userId);
        await _repository.Received(1).CompleteActivation(user, pending.PendingRegistrationId);
        await _producer.Received(1).SendAsync(EventType.ActivatedUser, userId);
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

        _repository.FindPendingByToken(tokenId, Arg.Any<CancellationToken>()).Returns((PendingRegistration?)null);
        _repository.FindUserByEmail(request.RetryEmail, Arg.Any<CancellationToken>()).Returns(existingUser);

        var result = await _service.Activate(request);

        result.Should().Be(userId);
        await _repository.DidNotReceive().CompleteActivation(Arg.Any<CreateUser>(), Arg.Any<Guid>());
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

        _repository.FindPendingByToken(tokenId, Arg.Any<CancellationToken>()).Returns(pending);
        _dateTimeProvider.Now.Returns(now);

        var result = await _service.GetPendingInfo(tokenId);

        result.Should().NotBeNull();
        result!.Email.Should().Be(pending.Email);
        result.Status.Should().Be("ready");
    }

    [Fact]
    public async Task DelegateToRecoveryServiceForActivationResend()
    {
        var email = "test@example.com";
        _recoveryService.Recover(email).Returns(RecoveryResult.ActivationResent);

        var result = await _service.ResendActivation(email);

        result.Should().BeTrue();
        await _recoveryService.Received(1).Recover(email);
    }
}
