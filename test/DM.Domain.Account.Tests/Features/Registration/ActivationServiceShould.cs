using System;
using DM.Domain.Core.Tokens;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
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
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IUserFactory _userFactory;
    private readonly IEventProducer _producer;
    private readonly ActivationService _service;

    public ActivationServiceShould()
    {
        _validator = Mock<IValidator<ActivationRequest>>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _repository = Mock<IActivationRepository>();
        _registrationRepository = Mock<IRegistrationRepository>();
        _userFactory = Mock<IUserFactory>();
        _producer = Mock<IEventProducer>();
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
            _registrationRepository,
            _userFactory,
            _producer,
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
    /// <summary>
    /// The same click landing twice is a success, not a failure of the storage
    /// engine.
    /// </summary>
    /// <remarks>
    /// Two requests carrying one activation link reach the write together: the
    /// second finds the pending row gone or the name written, and the unique index
    /// refuses it. Which index refused is not read off the constraint - the caller
    /// looks at what is actually there, and here it is this very account, made a
    /// moment ago by the other half of the double click.
    /// </remarks>
    [Fact]
    public async Task ReturnTheAccountWhenTheSameActivationLandedTwice()
    {
        var (request, pending, user) = ArrangeReadyActivation();
        _repository.CompleteActivation(user, pending.PendingRegistrationId)
            .Returns(_ => throw new DuplicateEntityException("Duplicate account"));
        _repository.FindUserByEmail(pending.Email, Arg.Any<CancellationToken>()).Returns(
            new AuthenticatedUser
            {
                UserId = user.UserId,
                Username = "NewUser",
                Email = pending.Email
            });

        (await _service.Activate(request)).Should().Be(user.UserId);
    }

    /// <summary>
    /// A name taken between the check and the write is refused under the name
    /// field, so the form can offer another one.
    /// </summary>
    [Fact]
    public async Task RefuseANameTakenSinceItWasChecked()
    {
        var (request, pending, user) = ArrangeReadyActivation();
        _repository.CompleteActivation(user, pending.PendingRegistrationId)
            .Returns(_ => throw new DuplicateEntityException("Duplicate account"));
        _repository.FindUserByEmail(pending.Email, Arg.Any<CancellationToken>())
            .Returns((AuthenticatedUser?)null);
        _registrationRepository.UsernameFree(request.Username, Arg.Any<CancellationToken>())
            .Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.Activate(request));

        thrown.ValidationErrors.Should().ContainKey(nameof(ActivationRequest.Username))
            .WhoseValue.Should().Be(ValidationError.Taken);
    }

    /// <summary>
    /// The address gone to another account leaves this registration with nothing
    /// to complete.
    /// </summary>
    [Fact]
    public async Task RefuseAnActivationWhoseAddressWentToSomebodyElse()
    {
        var (request, pending, user) = ArrangeReadyActivation();
        _repository.CompleteActivation(user, pending.PendingRegistrationId)
            .Returns(_ => throw new DuplicateEntityException("Duplicate account"));
        _repository.FindUserByEmail(pending.Email, Arg.Any<CancellationToken>()).Returns(
            new AuthenticatedUser
            {
                UserId = Guid.NewGuid(),
                Username = "somebodyelse",
                Email = pending.Email
            });

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Activate(request));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _producer.DidNotReceive().SendAsync(EventType.ActivatedUser, Arg.Any<Guid>());
    }

    /// <summary>A link that is live, unspent and a name that is free.</summary>
    private (ActivationRequest Request, PendingRegistration Pending, CreateUser User)
        ArrangeReadyActivation()
    {
        var tokenId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = new ActivationRequest { Token = tokenId, Username = "newuser" };
        var pending = new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            Email = "test@example.com",
            SecretHash = ConfirmationSecret.Hash(tokenId),
            TokenCreatedUtc = now.AddHours(-1)
        };
        var user = new CreateUser
        {
            UserId = Guid.NewGuid(),
            Username = "newuser",
            Email = pending.Email
        };

        _repository.FindPendingByToken(tokenId, Arg.Any<CancellationToken>()).Returns(pending);
        _userFactory.CreateFromPending(pending, request.Username).Returns(user);
        _registrationRepository.UsernameFree(request.Username, Arg.Any<CancellationToken>())
            .Returns(true);
        _dateTimeProvider.Now.Returns(now);
        return (request, pending, user);
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
}
