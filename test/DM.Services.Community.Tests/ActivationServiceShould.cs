using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.Activation;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;
using DM.Services.Community.Configuration;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.MessageQueuing.GeneralBus;
using DM.Tests.Core;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Community.Tests;

public class ActivationServiceShould : UnitTestBase
{
    private readonly ActivationService activationService;
    private readonly ISetup<IActivationRepository, Task<PendingRegistration?>> findPendingSetup;
    private readonly Mock<IActivationRepository> activationRepository;
    private readonly Mock<IRegistrationRepository> registrationRepository;
    private readonly Mock<IUserFactory> userFactory;
    private readonly Mock<IInvokedEventProducer> publisher;
    private readonly Mock<IRegistrationMailSender> mailSender;
    private readonly Mock<IGuidFactory> guidFactory;

    public ActivationServiceShould()
    {
        var validator = Mock<IValidator<ActivationRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<ActivationRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        activationRepository = Mock<IActivationRepository>();
        findPendingSetup = activationRepository
            .Setup(r => r.FindPendingByToken(It.IsAny<Guid>(), It.IsAny<CancellationToken>()));
        activationRepository
            .Setup(r => r.CompleteActivation(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        registrationRepository = Mock<IRegistrationRepository>();
        registrationRepository
            .Setup(r => r.SavePasswordToHistory(It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        userFactory = Mock<IUserFactory>();
        userFactory
            .Setup(f => f.CreateFromPending(It.IsAny<PendingRegistration>(), It.IsAny<string>()))
            .Returns((PendingRegistration p, string login) => new User
            {
                UserId = Guid.NewGuid(),
                Login = login,
                Email = p.Email,
                PasswordHash = p.PasswordHash,
                Salt = p.Salt,
                PasswordHashVersion = p.PasswordHashVersion
            });

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(p => p.Now).Returns(new DateTimeOffset(2019, 01, 02, 0, 0, 0, TimeSpan.Zero));

        publisher = Mock<IInvokedEventProducer>();
        publisher
            .Setup(p => p.Send(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        mailSender = Mock<IRegistrationMailSender>();
        mailSender
            .Setup(s => s.Send(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var tokenConfig = Options.Create(new TokenConfiguration { ActivationTokenLifetimeHours = 48 });

        activationService = new ActivationService(validator.Object,
            dateTimeProvider.Object,
            activationRepository.Object, registrationRepository.Object,
            userFactory.Object, publisher.Object, mailSender.Object,
            guidFactory.Object, tokenConfig);
    }

    [Fact]
    public async Task ThrowGoneException_WhenPendingNotFound()
    {
        findPendingSetup.ReturnsAsync((PendingRegistration?)null);

        var request = new ActivationRequest { Token = Guid.NewGuid(), Login = "TestUser" };
        (await activationService
            .Awaiting(s => s.Activate(request))
            .Should().ThrowAsync<HttpException>())
            .And.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task ThrowGoneException_WhenTokenExpired()
    {
        var pending = new PendingRegistration
        {
            TokenId = Guid.NewGuid(),
            Email = "test@test.com",
            TokenCreatedUtc = new DateTimeOffset(2018, 12, 30, 0, 0, 0, TimeSpan.Zero) // More than 48h ago
        };
        findPendingSetup.ReturnsAsync(pending);

        var request = new ActivationRequest { Token = pending.TokenId, Login = "TestUser" };
        (await activationService
            .Awaiting(s => s.Activate(request))
            .Should().ThrowAsync<HttpException>())
            .And.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task ReturnActivatedUserId_WhenTokenValid()
    {
        var pending = new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            TokenId = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            PasswordHashVersion = 2,
            TokenCreatedUtc = new DateTimeOffset(2019, 01, 01, 0, 0, 0, TimeSpan.Zero) // Within 48h
        };
        findPendingSetup.ReturnsAsync(pending);

        var request = new ActivationRequest { Token = pending.TokenId, Login = "TestUser" };
        var actual = await activationService.Activate(request);

        actual.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateUserFromPending_WhenTokenValid()
    {
        var pending = new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            TokenId = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            PasswordHashVersion = 2,
            TokenCreatedUtc = new DateTimeOffset(2019, 01, 01, 0, 0, 0, TimeSpan.Zero)
        };
        findPendingSetup.ReturnsAsync(pending);

        var request = new ActivationRequest { Token = pending.TokenId, Login = "TestUser" };
        await activationService.Activate(request);

        userFactory.Verify(f => f.CreateFromPending(pending, "TestUser"), Times.Once);
        activationRepository.Verify(r => r.CompleteActivation(
            It.IsAny<User>(),
            pending.PendingRegistrationId), Times.Once);
    }

    [Fact]
    public async Task SavePasswordToHistory_WhenActivated()
    {
        var pending = new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            TokenId = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            PasswordHashVersion = 2,
            TokenCreatedUtc = new DateTimeOffset(2019, 01, 01, 0, 0, 0, TimeSpan.Zero)
        };
        findPendingSetup.ReturnsAsync(pending);

        var request = new ActivationRequest { Token = pending.TokenId, Login = "TestUser" };
        await activationService.Activate(request);

        registrationRepository.Verify(r => r.SavePasswordToHistory(
            It.IsAny<Guid>(), "hash", "salt", 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishActivationMessage_WhenTokenValid()
    {
        var pending = new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            TokenId = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            PasswordHashVersion = 2,
            TokenCreatedUtc = new DateTimeOffset(2019, 01, 01, 0, 0, 0, TimeSpan.Zero)
        };
        findPendingSetup.ReturnsAsync(pending);

        var request = new ActivationRequest { Token = pending.TokenId, Login = "TestUser" };
        await activationService.Activate(request);

        publisher.Verify(p => p.Send(EventType.ActivatedUser, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task GetPendingInfo_ReturnsReady_WhenTokenValid()
    {
        var pending = new PendingRegistration
        {
            TokenId = Guid.NewGuid(),
            Email = "test@test.com",
            TokenCreatedUtc = new DateTimeOffset(2019, 01, 01, 0, 0, 0, TimeSpan.Zero)
        };
        findPendingSetup.ReturnsAsync(pending);

        var result = await activationService.GetPendingInfo(pending.TokenId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("ready");
        result.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task GetPendingInfo_ReturnsExpired_WhenTokenOld()
    {
        var pending = new PendingRegistration
        {
            TokenId = Guid.NewGuid(),
            Email = "test@test.com",
            TokenCreatedUtc = new DateTimeOffset(2018, 12, 30, 0, 0, 0, TimeSpan.Zero)
        };
        findPendingSetup.ReturnsAsync(pending);

        var result = await activationService.GetPendingInfo(pending.TokenId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("expired");
        result.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task GetPendingInfo_ReturnsNull_WhenTokenNotFound()
    {
        findPendingSetup.ReturnsAsync((PendingRegistration?)null);

        var result = await activationService.GetPendingInfo(Guid.NewGuid());

        result.Should().BeNull();
    }
}
