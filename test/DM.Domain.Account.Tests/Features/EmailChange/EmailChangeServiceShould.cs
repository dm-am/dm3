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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.EmailChange;

public class EmailChangeServiceShould : UnitTestBase
{
    private readonly IValidator<UserEmailChange> _validator;
    private readonly ITokenFactory _tokenFactory;
    private readonly IEmailChangeRepository _repository;
    private readonly IEmailChangeConfirmationRepository _confirmationRepository;
    private readonly IEmailChangeMailSender _mailSender;
    private readonly IEmailChangeWarningMailSender _warningMailSender;
    private readonly IEventProducer _eventProducer;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
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
        _auditService = Mock<ISecurityAuditRepository>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        var config = Options.Create(new TokenConfiguration
        {
            EmailChangeTokenLifetimeHours = 24
        });

        _validator.ValidateAsync(
                Arg.Any<ValidationContext<UserEmailChange>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new EmailChangeService(
            _validator,
            _tokenFactory,
            _repository,
            _confirmationRepository,
            _mailSender,
            _warningMailSender,
            _eventProducer,
            _auditService,
            _dateTimeProvider,
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

        _repository.FindUser(emailChange.Username).Returns((AuthenticatedUser?)null);

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
        // TokenId names the row and Secret goes into the letter: two different
        // values, which is the whole point of the change.
        var secret = Guid.NewGuid();
        var token = new CreateToken
        {
            TokenId = tokenId,
            Secret = secret,
            SecretHash = ConfirmationSecret.Hash(secret),
            Type = TokenType.EmailChange
        };

        _repository.FindUser(emailChange.Username).Returns(user);
        _tokenFactory.Create(userId, TokenType.EmailChange).Returns(token);

        var result = await _service.Change(emailChange);

        result.UserId.Should().Be(userId);
        result.Username.Should().Be("testuser");
        await _repository.Received(1).InvalidateOldEmailChangeTokens(userId);
        // The request puts the address in waiting: the account keeps answering by the
        // old one until the link is opened.
        await _repository.Received(1).RequestChange(userId, emailChange.Email, token);
        await _mailSender.Received(1).Send(emailChange.Email, emailChange.Username, secret);
        await _mailSender.DidNotReceive().Send(emailChange.Email, emailChange.Username, tokenId);
        await _eventProducer.Received(1).SendAsync(EventType.EmailChanged, userId);
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
        // TokenId names the row and Secret goes into the letter: two different
        // values, which is the whole point of the change.
        var secret = Guid.NewGuid();
        var token = new CreateToken
        {
            TokenId = tokenId,
            Secret = secret,
            SecretHash = ConfirmationSecret.Hash(secret),
            Type = TokenType.EmailChange
        };

        _repository.FindUser(emailChange.Username).Returns(user);
        _tokenFactory.Create(userId, TokenType.EmailChange).Returns(token);

        await _service.Change(emailChange);

        await _warningMailSender.Received(1).SendAsync(user.Email!, emailChange.Username, emailChange.Email);
    }

    [Fact]
    public async Task MoveTheAddressOnlyWhenTheLinkIsFollowed()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        _repository.ApplyPendingEmail(ownerId).Returns(true);

        await _service.Confirm(tokenId);

        // Confirmation is the moment the address changes.
        await _repository.Received(1).ApplyPendingEmail(ownerId);
        await _confirmationRepository.Received(1).MarkTokenUsed(tokenId);
    }

    [Fact]
    public async Task RefuseALiveTokenWithNothingPending()
    {
        // A link opened a second time, or a request withdrawn before it was
        // followed. The answer is the one an expired link gets: there is nothing
        // to confirm.
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        _repository.ApplyPendingEmail(ownerId).Returns(false);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Confirm(tokenId));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _confirmationRepository.DidNotReceive().MarkTokenUsed(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ThrowWhenConfirmingInvalidToken()
    {
        var tokenId = Guid.NewGuid();
        _confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns((Guid?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Confirm(tokenId));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
