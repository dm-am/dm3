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
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Recovery;

public class RecoveryServiceShould : UnitTestBase
{
    private readonly Mock<IEmailLookupRepository> _emailLookupRepository;
    private readonly Mock<IRegistrationRepository> _registrationRepository;
    private readonly Mock<IPasswordResetRepository> _passwordResetRepository;
    private readonly Mock<IPasswordResetMailSender> _passwordResetEmailSender;
    private readonly Mock<IRegistrationMailSender> _activationEmailSender;
    private readonly Mock<ITokenFactory> _tokenFactory;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<ISecurityAuditRepository> _auditService;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
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

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new RecoveryService(
            _emailLookupRepository.Object,
            _registrationRepository.Object,
            _passwordResetRepository.Object,
            _passwordResetEmailSender.Object,
            _activationEmailSender.Object,
            _tokenFactory.Object,
            _guidFactory.Object,
            _auditService.Object,
            _dateTimeProvider.Object,
            logger.Object);
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
        var token = new CreateToken
        {
            TokenId = tokenId,
            Type = TokenType.PasswordChange
        };

        _emailLookupRepository.Setup(r => r.GetUserByEmail(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokenFactory.Setup(f => f.Create(userId, TokenType.PasswordChange)).Returns(token);

        var result = await _service.Recover(email);

        result.Should().Be(RecoveryResult.PasswordReset);
        _passwordResetRepository.Verify(r => r.ReplacePasswordResetToken(userId, token), Times.Once);
        _passwordResetEmailSender.Verify(s => s.Send(email, user.Username, tokenId), Times.Once);
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
            TokenId = Guid.NewGuid()
        };

        _emailLookupRepository.Setup(r => r.GetUserByEmail(email, It.IsAny<CancellationToken>())).ReturnsAsync((EmailLookupInfo?)null);
        _registrationRepository.Setup(r => r.FindPendingByEmail(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _guidFactory.Setup(g => g.Create()).Returns(newTokenId);

        var result = await _service.Recover(email);

        result.Should().Be(RecoveryResult.ActivationResent);
        pending.TokenId.Should().Be(newTokenId);
        _registrationRepository.Verify(r => r.UpdatePending(pending), Times.Once);
        _activationEmailSender.Verify(s => s.Send(email, newTokenId), Times.Once);
    }

    [Fact]
    public async Task ReturnNotFoundForUnknownEmail()
    {
        var email = "unknown@example.com";

        _emailLookupRepository.Setup(r => r.GetUserByEmail(email, It.IsAny<CancellationToken>())).ReturnsAsync((EmailLookupInfo?)null);
        _registrationRepository.Setup(r => r.FindPendingByEmail(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration?)null);

        var result = await _service.Recover(email);

        result.Should().Be(RecoveryResult.NotFound);
    }
}
