using System;
using System.Linq;
using DM.Domain.Core.Tokens;
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
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class RegistrationServiceShould : UnitTestBase
{
    private readonly IValidator<UserRegistration> validator;
    private readonly ISecurityManager securityManager;
    private readonly ICompromisedPasswordChecker compromisedPasswordChecker;
    private readonly IRegistrationRepository repository;
    private readonly IRegistrationMailSender mailSender;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly RegistrationService service;

    public RegistrationServiceShould()
    {
        validator = Mock<IValidator<UserRegistration>>();
        securityManager = Mock<ISecurityManager>();
        compromisedPasswordChecker = Mock<ICompromisedPasswordChecker>();
        repository = Mock<IRegistrationRepository>();
        mailSender = Mock<IRegistrationMailSender>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        validator.ValidateAsync(
                Arg.Any<ValidationContext<UserRegistration>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new RegistrationService(
            validator,
            securityManager,
            compromisedPasswordChecker,
            repository,
            mailSender,
            guidFactory,
            dateTimeProvider);
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

        compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(true);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => service.Register(registration));

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

        compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(false);
        securityManager.GeneratePassword(registration.Password).Returns(("hash", "salt"));
        guidFactory.Create().Returns(pendingId, tokenId);

        await service.Register(registration);

        await repository.Received(1).AddPending(Arg.Is<PendingRegistration>(p =>
            p.Email == registration.Email.ToLowerInvariant() &&
            p.PendingRegistrationId == pendingId &&
            p.Secret == tokenId &&
            p.SecretHash.SequenceEqual(ConfirmationSecret.Hash(tokenId)) &&
            p.AcceptedRules == registration.AcceptedRules
        ));
        await mailSender.Received(1).Send(registration.Email, tokenId);
    }

    /// <summary>
    /// Registering again on an address that already has a pending row is the same
    /// write, and the service asks nothing first.
    /// </summary>
    /// <remarks>
    /// It used to ask whether the address had a pending registration and branch on
    /// the answer, which stops being true the moment it is given: two submissions
    /// of the form arriving together both heard "nothing pending" and both
    /// inserted, and the loser was answered with a failure of the storage engine.
    /// AddPending replaces whatever the address has, so there is nothing left to
    /// decide here.
    /// </remarks>
    [Fact]
    public async Task WriteThePendingRegistrationOfAnAddressThatAlreadyHasOne()
    {
        var registration = new UserRegistration
        {
            Email = "existing@example.com",
            Password = "newsecurepass123",
            AcceptedRules = true
        };
        var pendingId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(false);
        securityManager.GeneratePassword(registration.Password).Returns(("newhash", "newsalt"));
        guidFactory.Create().Returns(pendingId, tokenId);

        await service.Register(registration);

        await repository.Received(1).AddPending(Arg.Is<PendingRegistration>(p =>
            p.Email == registration.Email.ToLowerInvariant() &&
            p.PasswordHash == "newhash" &&
            p.Salt == "newsalt"
        ));
        await mailSender.Received(1).Send(registration.Email, tokenId);
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

        compromisedPasswordChecker.IsCompromisedAsync(registration.Password).Returns(false);
        securityManager.GeneratePassword(registration.Password).Returns(("hash", "salt"));
        guidFactory.Create().Returns(Guid.NewGuid());

        await service.Register(registration);

        await repository.Received(1).AddPending(Arg.Is<PendingRegistration>(p =>
            p.Email == "test@example.com"
        ));
    }
}
