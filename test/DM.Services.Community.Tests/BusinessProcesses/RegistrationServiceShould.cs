using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Tests.Core;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Community.Tests.BusinessProcesses;

public class RegistrationServiceShould : UnitTestBase
{
    private readonly ISetup<ISecurityManager, (string Hash, string Salt, int Version)> passwordGenerationSetup;
    private readonly Mock<IRegistrationRepository> registrationRepository;
    private readonly Mock<IRegistrationMailSender> mailSender;
    private readonly RegistrationService service;
    private readonly Mock<ISecurityManager> securityManager;
    private readonly Mock<IGuidFactory> guidFactory;

    public RegistrationServiceShould()
    {
        var validator = Mock<IValidator<UserRegistration>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UserRegistration>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        securityManager = Mock<ISecurityManager>();
        passwordGenerationSetup = securityManager.Setup(m => m.GeneratePassword(It.IsAny<string>()));

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(p => p.Now).Returns(new DateTimeOffset(2019, 01, 02, 0, 0, 0, TimeSpan.Zero));

        registrationRepository = Mock<IRegistrationRepository>();
        registrationRepository
            .Setup(r => r.PendingExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        registrationRepository
            .Setup(r => r.AddPending(It.IsAny<PendingRegistration>()))
            .Returns(Task.CompletedTask);

        mailSender = Mock<IRegistrationMailSender>();
        mailSender
            .Setup(s => s.Send(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Constructor order: validator, securityManager, repository, mailSender, guidFactory, dateTimeProvider
        service = new RegistrationService(validator.Object,
            securityManager.Object,
            registrationRepository.Object,
            mailSender.Object,
            guidFactory.Object,
            dateTimeProvider.Object);
    }

    [Fact]
    public async Task CreatePendingRegistrationWithGeneratedSaltAndHash()
    {
        passwordGenerationSetup.Returns(("hash", "salt", 2));

        var userRegistration = new UserRegistration { Email = "test@email.com", Password = "my password" };
        await service.Register(userRegistration);

        securityManager.Verify(m => m.GeneratePassword("my password"));
        registrationRepository.Verify(r => r.AddPending(
            It.Is<PendingRegistration>(p =>
                p.PasswordHash == "hash" &&
                p.Salt == "salt" &&
                p.PasswordHashVersion == 2 &&
                p.Email == "test@email.com")), Times.Once);
    }

    [Fact]
    public async Task ReplacePendingRegistrationWhenEmailAlreadyExists()
    {
        passwordGenerationSetup.Returns(("hash", "salt", 2));
        registrationRepository
            .Setup(r => r.PendingExists("existing@email.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        registrationRepository
            .Setup(r => r.ReplacePending(It.IsAny<PendingRegistration>()))
            .Returns(Task.CompletedTask);

        await service.Register(new UserRegistration { Email = "existing@email.com", Password = "password" });

        registrationRepository.Verify(r => r.ReplacePending(
            It.Is<PendingRegistration>(p => p.Email == "existing@email.com")), Times.Once);
        registrationRepository.Verify(r => r.AddPending(
            It.IsAny<PendingRegistration>()), Times.Never);
    }

    [Fact]
    public async Task SendConfirmationLetter()
    {
        passwordGenerationSetup.Returns(("hash", "salt", 2));
        var tokenId = Guid.NewGuid();
        guidFactory.SetupSequence(f => f.Create())
            .Returns(Guid.NewGuid()) // PendingRegistrationId
            .Returns(tokenId);        // TokenId

        await service.Register(new UserRegistration { Email = "email@test.com", Password = "password" });

        mailSender.Verify(s => s.Send("email@test.com", tokenId), Times.Once);
        mailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NormalizeEmailToLowercase()
    {
        passwordGenerationSetup.Returns(("hash", "salt", 2));

        await service.Register(new UserRegistration { Email = "Test@EMAIL.Com", Password = "password" });

        registrationRepository.Verify(r => r.AddPending(
            It.Is<PendingRegistration>(p => p.Email == "test@email.com")), Times.Once);
    }
}
