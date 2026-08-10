using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Tokens;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.EmailChange;

public class EmailChangeServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<UserEmailChange>> _validator;
    private readonly Mock<ITokenFactory> _tokenFactory;
    private readonly Mock<IEmailChangeRepository> _repository;
    private readonly Mock<IEmailChangeConfirmationRepository> _confirmationRepository;
    private readonly Mock<IEmailChangeMailSender> _mailSender;
    private readonly Mock<IEmailChangeWarningMailSender> _warningMailSender;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<ISecurityAuditService> _auditService;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly EmailChangeService _service;

    public EmailChangeServiceShould()
    {
        _validator = Mock<IValidator<UserEmailChange>>();
        _tokenFactory = Mock<ITokenFactory>();
        _repository = Mock<IEmailChangeRepository>();
        _confirmationRepository = Mock<IEmailChangeConfirmationRepository>();
        _mailSender = Mock<IEmailChangeMailSender>();
        _warningMailSender = Mock<IEmailChangeWarningMailSender>();
        _eventProducer = Mock<IEventProducer>();
        _auditService = Mock<ISecurityAuditService>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        var config = Options.Create(new TokenConfiguration
        {
            EmailChangeTokenLifetimeHours = 24
        });

        _validator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<UserEmailChange>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new EmailChangeService(
            _validator.Object,
            _tokenFactory.Object,
            _repository.Object,
            _confirmationRepository.Object,
            _mailSender.Object,
            _warningMailSender.Object,
            _eventProducer.Object,
            _auditService.Object,
            _dateTimeProvider.Object,
            config);
    }

    [Fact]
    public async Task ThrowWhenUserNotFound()
    {
        var emailChange = new UserEmailChange
        {
            Username = "nonexistent",
            Email = "new@example.com"
        };

        _repository.Setup(r => r.FindUser(emailChange.Username)).ReturnsAsync((AuthenticatedUser?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Change(emailChange));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTokenAndSendConfirmationEmail()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var emailChange = new UserEmailChange
        {
            Username = "testuser",
            Email = "new@example.com"
        };
        var user = new AuthenticatedUser
        {
            UserId = userId,
            Username = "testuser",
            Email = "old@example.com"
        };
        var token = new CreateToken
        {
            TokenId = tokenId,
            Type = TokenType.EmailChange
        };

        _repository.Setup(r => r.FindUser(emailChange.Username)).ReturnsAsync(user);
        _tokenFactory.Setup(f => f.Create(userId, TokenType.EmailChange)).Returns(token);

        var result = await _service.Change(emailChange);

        result.UserId.Should().Be(userId);
        result.Username.Should().Be("testuser");
        _repository.Verify(r => r.InvalidateOldEmailChangeTokens(userId), Times.Once);
        _repository.Verify(r => r.RequestChange(userId, emailChange.Email, token), Times.Once,
                        "запрос кладет адрес в ожидание: аккаунт отвечает по старому, пока ссылка не открыта");
        _mailSender.Verify(m => m.Send(emailChange.Email, emailChange.Username, tokenId), Times.Once);
        _eventProducer.Verify(e => e.SendAsync(EventType.EmailChanged, userId), Times.Once);
    }

    [Fact]
    public async Task SendWarningEmailToOldAddress()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var emailChange = new UserEmailChange
        {
            Username = "testuser",
            Email = "new@example.com"
        };
        var user = new AuthenticatedUser
        {
            UserId = userId,
            Username = "testuser",
            Email = "old@example.com"
        };
        var token = new CreateToken
        {
            TokenId = tokenId,
            Type = TokenType.EmailChange
        };

        _repository.Setup(r => r.FindUser(emailChange.Username)).ReturnsAsync(user);
        _tokenFactory.Setup(f => f.Create(userId, TokenType.EmailChange)).Returns(token);

        await _service.Change(emailChange);

        _warningMailSender.Verify(w => w.SendAsync(user.Email!, emailChange.Username, emailChange.Email), Times.Once);
    }

    [Fact]
    public async Task MoveTheAddressOnlyWhenTheLinkIsFollowed()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.Setup(r => r.FindEmailChangeTokenOwner(tokenId, It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(ownerId);
        _repository.Setup(r => r.ApplyPendingEmail(ownerId)).ReturnsAsync(true);

        await _service.Confirm(tokenId);

        _repository.Verify(r => r.ApplyPendingEmail(ownerId), Times.Once,
            "подтверждение и есть тот момент, когда адрес меняется");
        _confirmationRepository.Verify(r => r.MarkTokenUsed(tokenId), Times.Once);
    }

    [Fact]
    public async Task RefuseALiveTokenWithNothingPending()
    {
        // Ссылка, открытая второй раз, или запрос, отозванный до перехода.
        // Ответ тот же, что у истекшей ссылки: подтверждать нечего.
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.Setup(r => r.FindEmailChangeTokenOwner(tokenId, It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(ownerId);
        _repository.Setup(r => r.ApplyPendingEmail(ownerId)).ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Confirm(tokenId));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _confirmationRepository.Verify(r => r.MarkTokenUsed(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ThrowWhenConfirmingInvalidToken()
    {
        var tokenId = Guid.NewGuid();
        _confirmationRepository.Setup(r => r.FindEmailChangeTokenOwner(tokenId, It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((Guid?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Confirm(tokenId));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
