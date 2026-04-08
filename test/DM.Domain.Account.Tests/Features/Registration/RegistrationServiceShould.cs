using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class RegistrationServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<UserRegistration>> _validator;
    private readonly Mock<ISecurityManager> _securityManager;
    private readonly Mock<ICompromisedPasswordChecker> _compromisedPasswordChecker;
    private readonly Mock<IRegistrationRepository> _repository;
    private readonly Mock<IRegistrationMailSender> _mailSender;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
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

        _validator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<UserRegistration>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new RegistrationService(
            _validator.Object,
            _securityManager.Object,
            _compromisedPasswordChecker.Object,
            _repository.Object,
            _mailSender.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
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

        _compromisedPasswordChecker.Setup(c => c.IsCompromisedAsync(registration.Password))
            .ReturnsAsync(true);

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

        _compromisedPasswordChecker.Setup(c => c.IsCompromisedAsync(registration.Password))
            .ReturnsAsync(false);
        _securityManager.Setup(s => s.GeneratePassword(registration.Password))
            .Returns(("hash", "salt"));
        _guidFactory.SetupSequence(g => g.Create())
            .Returns(pendingId)
            .Returns(tokenId);
        _repository.Setup(r => r.PendingExists(registration.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _service.Register(registration);

        _repository.Verify(r => r.AddPending(It.Is<PendingRegistration>(p =>
            p.Email == registration.Email.ToLowerInvariant() &&
            p.PendingRegistrationId == pendingId &&
            p.TokenId == tokenId &&
            p.AcceptedRules == registration.AcceptedRules
        )), Times.Once);
        _mailSender.Verify(m => m.Send(registration.Email, tokenId), Times.Once);
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

        _compromisedPasswordChecker.Setup(c => c.IsCompromisedAsync(registration.Password))
            .ReturnsAsync(false);
        _securityManager.Setup(s => s.GeneratePassword(registration.Password))
            .Returns(("newhash", "newsalt"));
        _guidFactory.SetupSequence(g => g.Create())
            .Returns(pendingId)
            .Returns(tokenId);
        _repository.Setup(r => r.PendingExists(registration.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.Register(registration);

        _repository.Verify(r => r.ReplacePending(It.Is<PendingRegistration>(p =>
            p.Email == registration.Email.ToLowerInvariant() &&
            p.PasswordHash == "newhash" &&
            p.Salt == "newsalt"
        )), Times.Once);
        _mailSender.Verify(m => m.Send(registration.Email, tokenId), Times.Once);
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

        _compromisedPasswordChecker.Setup(c => c.IsCompromisedAsync(registration.Password))
            .ReturnsAsync(false);
        _securityManager.Setup(s => s.GeneratePassword(registration.Password))
            .Returns(("hash", "salt"));
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());
        _repository.Setup(r => r.PendingExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _service.Register(registration);

        _repository.Verify(r => r.AddPending(It.Is<PendingRegistration>(p =>
            p.Email == "test@example.com"
        )), Times.Once);
    }
}
