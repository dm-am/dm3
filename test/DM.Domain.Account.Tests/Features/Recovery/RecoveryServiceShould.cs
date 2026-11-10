using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Tokens;
using DM.Domain.Core.Enums;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Recovery;

public class RecoveryServiceShould : UnitTestBase
{
    private readonly IEmailLookupRepository emailLookupRepository;
    private readonly IRegistrationRepository registrationRepository;
    private readonly IPasswordResetRepository passwordResetRepository;
    private readonly IPasswordResetMailSender passwordResetEmailSender;
    private readonly IRegistrationMailSender activationEmailSender;
    private readonly ITokenFactory tokenFactory;
    private readonly IGuidFactory guidFactory;
    private readonly ISecurityAuditRepository auditService;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly RecoveryService service;

    public RecoveryServiceShould()
    {
        emailLookupRepository = Mock<IEmailLookupRepository>();
        registrationRepository = Mock<IRegistrationRepository>();
        passwordResetRepository = Mock<IPasswordResetRepository>();
        passwordResetEmailSender = Mock<IPasswordResetMailSender>();
        activationEmailSender = Mock<IRegistrationMailSender>();
        tokenFactory = Mock<ITokenFactory>();
        guidFactory = Mock<IGuidFactory>();
        auditService = Mock<ISecurityAuditRepository>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        var logger = Mock<ILogger<RecoveryService>>();

        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new RecoveryService(
            emailLookupRepository,
            registrationRepository,
            passwordResetRepository,
            passwordResetEmailSender,
            activationEmailSender,
            tokenFactory,
            guidFactory,
            auditService,
            dateTimeProvider,
            logger);
    }

    [Fact]
    public async Task SendPasswordResetEmailForActiveUser()
    {
        var email = "user@example.com";
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var user = new EmailLookupInfo
        {
            UserId = userId,
            Username = "testuser",
            Email = email
        };
        // TokenId names the row, Secret goes into the letter, and the two are
        // deliberately different values — that is the whole point of the change.
        var secret = Guid.NewGuid();
        var token = new CreateToken
        {
            TokenId = tokenId,
            Secret = secret,
            SecretHash = ConfirmationSecret.Hash(secret),
            Type = TokenType.PasswordChange
        };

        emailLookupRepository.GetUserByEmail(email, Arg.Any<CancellationToken>()).Returns(user);
        tokenFactory.Create(userId, TokenType.PasswordChange).Returns(token);

        var result = await service.Recover(email);

        result.Should().Be(RecoveryResult.PasswordReset);
        await passwordResetRepository.Received(1).ReplacePasswordResetToken(userId, token);
        await passwordResetEmailSender.Received(1).Send(email, secret);
        await passwordResetEmailSender.DidNotReceive().Send(email, tokenId);
    }

    [Fact]
    public async Task ResendActivationEmailForPendingRegistration()
    {
        var email = "pending@example.com";
        var newTokenId = Guid.NewGuid();
        var pending = new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            Email = email,
            SecretHash = ConfirmationSecret.Hash(Guid.NewGuid())
        };

        emailLookupRepository.GetUserByEmail(email, Arg.Any<CancellationToken>()).Returns((EmailLookupInfo?)null);
        registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>()).Returns(pending);
        guidFactory.Create().Returns(newTokenId);

        var result = await service.Recover(email);

        result.Should().Be(RecoveryResult.ActivationResent);
        // A resend issues a new secret and stores its hash: the old value is not
        // recoverable from the row, so mailing it again is not an option.
        pending.Secret.Should().Be(newTokenId);
        pending.SecretHash.Should().Equal(ConfirmationSecret.Hash(newTokenId));
        await registrationRepository.Received(1).UpdatePending(pending);
        await activationEmailSender.Received(1).Send(email, newTokenId);
    }

    [Fact]
    public async Task ReturnNotFoundForUnknownEmail()
    {
        var email = "unknown@example.com";

        emailLookupRepository.GetUserByEmail(email, Arg.Any<CancellationToken>()).Returns((EmailLookupInfo?)null);
        registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>())
            .Returns((PendingRegistration?)null);

        var result = await service.Recover(email);

        result.Should().Be(RecoveryResult.NotFound);
    }

    // "Not found" here plus "taken" on registration equalled "removed" for a guest with two
    // requests and no password (SECURITY.md).
    [Fact]
    public async Task AnswerARemovedAccountAsALiveOneAndSendNothing()
    {
        var email = "gone@example.com";
        emailLookupRepository.GetUserByEmail(email, Arg.Any<CancellationToken>()).Returns(new EmailLookupInfo
        {
            UserId = Guid.NewGuid(),
            Username = "gone",
            Email = email,
            IsRemoved = true
        });

        var result = await service.Recover(email);

        result.Should().Be(RecoveryResult.PasswordReset);
        await passwordResetEmailSender.DidNotReceiveWithAnyArgs().Send(default!, default);
        await passwordResetRepository.DidNotReceiveWithAnyArgs().ReplacePasswordResetToken(default, default!);
        await auditService.DidNotReceiveWithAnyArgs().LogAsync(default, default);
    }
}
