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
    private readonly IEmailLookupRepository _emailLookupRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IPasswordResetRepository _passwordResetRepository;
    private readonly IPasswordResetMailSender _passwordResetEmailSender;
    private readonly IRegistrationMailSender _activationEmailSender;
    private readonly ITokenFactory _tokenFactory;
    private readonly IGuidFactory _guidFactory;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RecoveryService _service;

    public RecoveryServiceShould()
    {
        _emailLookupRepository = Mock<IEmailLookupRepository>();
        _registrationRepository = Mock<IRegistrationRepository>();
        _passwordResetRepository = Mock<IPasswordResetRepository>();
        _passwordResetEmailSender = Mock<IPasswordResetMailSender>();
        _activationEmailSender = Mock<IRegistrationMailSender>();
        _tokenFactory = Mock<ITokenFactory>();
        _guidFactory = Mock<IGuidFactory>();
        _auditService = Mock<ISecurityAuditRepository>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        var logger = Mock<ILogger<RecoveryService>>();

        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new RecoveryService(
            _emailLookupRepository,
            _registrationRepository,
            _passwordResetRepository,
            _passwordResetEmailSender,
            _activationEmailSender,
            _tokenFactory,
            _guidFactory,
            _auditService,
            _dateTimeProvider,
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

        _emailLookupRepository.GetUserByEmail(email, Arg.Any<CancellationToken>()).Returns(user);
        _tokenFactory.Create(userId, TokenType.PasswordChange).Returns(token);

        var result = await _service.Recover(email);

        result.Should().Be(RecoveryResult.PasswordReset);
        await _passwordResetRepository.Received(1).ReplacePasswordResetToken(userId, token);
        await _passwordResetEmailSender.Received(1).Send(email, user.Username, secret);
        await _passwordResetEmailSender.DidNotReceive().Send(email, user.Username, tokenId);
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

        _emailLookupRepository.GetUserByEmail(email, Arg.Any<CancellationToken>()).Returns((EmailLookupInfo?)null);
        _registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>()).Returns(pending);
        _guidFactory.Create().Returns(newTokenId);

        var result = await _service.Recover(email);

        result.Should().Be(RecoveryResult.ActivationResent);
        // A resend issues a new secret and stores its hash: the old value is not
        // recoverable from the row, so mailing it again is not an option.
        pending.Secret.Should().Be(newTokenId);
        pending.SecretHash.Should().Equal(ConfirmationSecret.Hash(newTokenId));
        await _registrationRepository.Received(1).UpdatePending(pending);
        await _activationEmailSender.Received(1).Send(email, newTokenId);
    }

    [Fact]
    public async Task ReturnNotFoundForUnknownEmail()
    {
        var email = "unknown@example.com";

        _emailLookupRepository.GetUserByEmail(email, Arg.Any<CancellationToken>()).Returns((EmailLookupInfo?)null);
        _registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>())
            .Returns((PendingRegistration?)null);

        var result = await _service.Recover(email);

        result.Should().Be(RecoveryResult.NotFound);
    }
}
