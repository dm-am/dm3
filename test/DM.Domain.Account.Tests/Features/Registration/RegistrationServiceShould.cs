using System;
using System.Linq;
using DM.Domain.Core.Tokens;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class RegistrationServiceShould : UnitTestBase
{
    private readonly IValidator<UserRegistration> _validator;
    private readonly ISecurityManager _securityManager;
    private readonly ICompromisedPasswordChecker _compromisedPasswordChecker;
    private readonly IRegistrationRepository _repository;
    private readonly IRegistrationMailSender _mailSender;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RegistrationService _service;

    public RegistrationServiceShould()
    {
        _validator = Mock<IValidator<UserRegistration>>();
        _securityManager = Mock<ISecurityManager>();
        _compromisedPasswordChecker = Mock<ICompromisedPasswordChecker>();
        _repository = Mock<IRegistrationRepository>();
        _mailSender = Mock<IRegistrationMailSender>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _validator.ValidateAsync(
                Arg.Any<ValidationContext<UserRegistration>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new RegistrationService(
            _validator,
            _securityManager,
            _compromisedPasswordChecker,
            _repository,
            _mailSender,
            _guidFactory,
            _dateTimeProvider);
    }

    [Fact]
    public async Task ThrowWhenPasswordIsCompromised()
    {
        var registration = new UserRegistration
        {
            Email = "test@example.com",
            Password = "compromised123",
            AcceptedRules = true
        };

        _compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(true);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.Register(registration));

        exception.ValidationErrors.Should().ContainKey(nameof(registration.Password));
    }

    [Fact]
    public async Task CreatePendingRegistrationForNewEmail()
    {
        var registration = new UserRegistration
        {
            Email = "newuser@example.com",
            Password = "securepass123",
            AcceptedRules = true
        };
        var pendingId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        _compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(false);
        _securityManager.GeneratePassword(registration.Password).Returns(("hash", "salt"));
        _guidFactory.Create().Returns(pendingId, tokenId);
        _repository.PendingExists(registration.Email, Arg.Any<CancellationToken>()).Returns(false);

        await _service.Register(registration);

        await _repository.Received(1).AddPending(Arg.Is<PendingRegistration>(p =>
            p.Email == registration.Email.ToLowerInvariant() &&
            p.PendingRegistrationId == pendingId &&
            p.Secret == tokenId &&
            p.SecretHash.SequenceEqual(ConfirmationSecret.Hash(tokenId)) &&
            p.AcceptedRules == registration.AcceptedRules
        ));
        await _mailSender.Received(1).Send(registration.Email, tokenId);
    }

    [Fact]
    public async Task ReplacePendingRegistrationForExistingEmail()
    {
        var registration = new UserRegistration
        {
            Email = "existing@example.com",
            Password = "newsecurepass123",
            AcceptedRules = true
        };
        var pendingId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        _compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(false);
        _securityManager.GeneratePassword(registration.Password).Returns(("newhash", "newsalt"));
        _guidFactory.Create().Returns(pendingId, tokenId);
        _repository.PendingExists(registration.Email, Arg.Any<CancellationToken>()).Returns(true);

        await _service.Register(registration);

        await _repository.Received(1).ReplacePending(Arg.Is<PendingRegistration>(p =>
            p.Email == registration.Email.ToLowerInvariant() &&
            p.PasswordHash == "newhash" &&
            p.Salt == "newsalt"
        ));
        await _mailSender.Received(1).Send(registration.Email, tokenId);
    }

    [Fact]
    public async Task NormalizeEmailToLowercase()
    {
        var registration = new UserRegistration
        {
            Email = "Test@EXAMPLE.COM",
            Password = "securepass123",
            AcceptedRules = true
        };

        _compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(false);
        _securityManager.GeneratePassword(registration.Password).Returns(("hash", "salt"));
        _guidFactory.Create().Returns(Guid.NewGuid());
        _repository.PendingExists(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await _service.Register(registration);

        await _repository.Received(1).AddPending(Arg.Is<PendingRegistration>(p =>
            p.Email == "test@example.com"
        ));
    }
}
