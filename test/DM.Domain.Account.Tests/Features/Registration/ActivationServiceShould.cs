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
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class ActivationServiceShould : UnitTestBase
{
    private readonly IValidator<ActivationRequest> validator;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IActivationRepository repository;
    private readonly IRegistrationRepository registrationRepository;
    private readonly IUserFactory userFactory;
    private readonly IEventProducer producer;
    private readonly ActivationService service;

    public ActivationServiceShould()
    {
        validator = Mock<IValidator<ActivationRequest>>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        repository = Mock<IActivationRepository>();
        registrationRepository = Mock<IRegistrationRepository>();
        userFactory = Mock<IUserFactory>();
        producer = Mock<IEventProducer>();
        var config = Options.Create(new TokenConfiguration
        {
            ActivationTokenLifetimeHours = 48
        });

        validator.ValidateAsync(
                Arg.Any<ValidationContext<ActivationRequest>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new ActivationService(
            validator,
            dateTimeProvider,
            repository,
            registrationRepository,
            userFactory,
            producer,
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

        repository.FindPendingByToken(request.Token, Arg.Any<CancellationToken>()).Returns((PendingRegistration?)null);
        repository.FindUserByEmail(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AuthenticatedUser?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.Activate(request));

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

        repository.FindPendingByToken(request.Token, Arg.Any<CancellationToken>()).Returns(pending);
        dateTimeProvider.Now.Returns(now);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.Activate(request));

        exception.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task CreateUserAndCompleteActivation()
    {
        var (request, pending, user) = ArrangeReadyActivation();

        var result = await service.Activate(request);

        result.Should().Be(user.UserId);
        await repository.Received(1).CompleteActivation(user, pending.PendingRegistrationId);
        await producer.Received(1).SendAsync(EventType.ActivatedUser, user.UserId);
    }

    // The same click landing twice is a success, not a failure of the storage engine.
    [Fact]
    public async Task ReturnTheAccountWhenTheSameActivationLandedTwice()
    {
        var (request, pending, user) = ArrangeReadyActivation();
        repository.CompleteActivation(user, pending.PendingRegistrationId)
            .Returns(_ => throw new DuplicateEntityException("Duplicate account"));
        repository.FindUserByEmail(pending.Email, Arg.Any<CancellationToken>()).Returns(
            new AuthenticatedUser
            {
                UserId = user.UserId,
                Username = "NewUser",
                Email = pending.Email
            });

        (await service.Activate(request)).Should().Be(user.UserId);
    }

    // A name taken between the check and the write is refused under the name field, so the form can
    // offer another one.
    [Fact]
    public async Task RefuseANameTakenSinceItWasChecked()
    {
        var (request, pending, user) = ArrangeReadyActivation();
        repository.CompleteActivation(user, pending.PendingRegistrationId)
            .Returns(_ => throw new DuplicateEntityException("Duplicate account"));
        repository.FindUserByEmail(pending.Email, Arg.Any<CancellationToken>())
            .Returns((AuthenticatedUser?)null);
        registrationRepository.UsernameFree(request.Username, Arg.Any<CancellationToken>())
            .Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => service.Activate(request));

        thrown.ValidationErrors.Should().ContainKey(nameof(ActivationRequest.Username))
            .WhoseValue.Should().Be(ValidationError.Taken);
    }

    [Fact]
    public async Task RefuseAnActivationWhoseAddressWentToSomebodyElse()
    {
        var (request, pending, user) = ArrangeReadyActivation();
        repository.CompleteActivation(user, pending.PendingRegistrationId)
            .Returns(_ => throw new DuplicateEntityException("Duplicate account"));
        repository.FindUserByEmail(pending.Email, Arg.Any<CancellationToken>()).Returns(
            new AuthenticatedUser
            {
                UserId = Guid.NewGuid(),
                Username = "somebodyelse",
                Email = pending.Email
            });

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Activate(request));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await producer.DidNotReceive().SendAsync(EventType.ActivatedUser, Arg.Any<Guid>());
    }

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

        repository.FindPendingByToken(tokenId, Arg.Any<CancellationToken>()).Returns(pending);
        userFactory.CreateFromPending(pending, request.Username).Returns(user);
        registrationRepository.UsernameFree(request.Username, Arg.Any<CancellationToken>())
            .Returns(true);
        dateTimeProvider.Now.Returns(now);
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

        repository.FindPendingByToken(tokenId, Arg.Any<CancellationToken>()).Returns(pending);
        dateTimeProvider.Now.Returns(now);

        var result = await service.GetPendingInfo(tokenId);

        result.Should().NotBeNull();
        result!.Email.Should().Be(pending.Email);
        result.Status.Should().Be("ready");
    }
}
