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
    private readonly IEmailChangeNoticeMailSender _noticeMailSender;
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
        _noticeMailSender = Mock<IEmailChangeNoticeMailSender>();
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
            _noticeMailSender,
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
        // One call, and it is the call that also retires the letters sent before
        // it: as two, a request from another tab slipped between them and left two
        // live links.
        //
        // The request puts the address in waiting: the account keeps answering by the
        // old one until the link is opened.
        await _repository.Received(1).RequestChange(userId, emailChange.Email, token);
        await _mailSender.Received(1).Send(emailChange.Email, secret);
        await _mailSender.DidNotReceive().Send(emailChange.Email, tokenId);
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

        await _noticeMailSender.Received(1).SendRequested(user.Email!, emailChange.Email);
    }

    [Fact]
    public async Task MoveTheAddressOnlyWhenTheLinkIsFollowed()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        _repository.ApplyPendingEmail(ownerId)
            .Returns(new EmailMove("was@example.com", "now@example.com"));

        await _service.Confirm(tokenId);

        // Confirmation is the moment the address changes.
        await _repository.Received(1).ApplyPendingEmail(ownerId);
        await _confirmationRepository.Received(1).MarkTokenUsed(tokenId);
    }

    /// <summary>
    /// The second letter of the pair goes to the address the account just left.
    /// </summary>
    /// <remarks>
    /// The first letter warns while a new password can still call the move off.
    /// This one cannot be acted on from that mailbox at all, and it exists for
    /// the reader who never opened the first: without it the move is silent, and
    /// the owner learns of it at a door that no longer opens.
    /// </remarks>
    [Fact]
    public async Task TellThePreviousAddressThatTheAccountHasMoved()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        _repository.ApplyPendingEmail(ownerId)
            .Returns(new EmailMove("was@example.com", "now@example.com"));

        await _service.Confirm(tokenId);

        await _noticeMailSender.Received(1).SendCompleted("was@example.com", "now@example.com");
    }

    /// <summary>
    /// A link with nothing behind it moves nobody, so nobody is told about it.
    /// </summary>
    [Fact]
    public async Task TellNobodyWhenThereWasNothingToMove()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        _repository.ApplyPendingEmail(ownerId).Returns((EmailMove?)null);

        await Assert.ThrowsAsync<HttpException>(() => _service.Confirm(tokenId));

        await _noticeMailSender.DidNotReceiveWithAnyArgs().SendCompleted(default!, default!);
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
        _repository.ApplyPendingEmail(ownerId).Returns((EmailMove?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Confirm(tokenId));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _confirmationRepository.DidNotReceive().MarkTokenUsed(Arg.Any<Guid>());
    }

    /// <summary>
    /// An address taken while the letter waited is refused the way the form
    /// refuses one, and the link is spent all the same.
    /// </summary>
    /// <remarks>
    /// The check that the address is free runs when the change is asked for, and
    /// nothing holds it between then and the click: whoever registers or moves
    /// onto it first gets it, and the unique index is what says so. Answered under
    /// the address field, because the form that sent the person here is the form
    /// that has to offer them another address - and spent, because the link
    /// authorised a move that cannot happen and would otherwise only collect the
    /// same refusal again.
    /// </remarks>
    [Fact]
    public async Task RefuseAnAddressTakenWhileTheLetterWaited()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        _repository.ApplyPendingEmail(ownerId)
            .Returns<EmailMove?>(_ => throw new DuplicateEntityException("Duplicate account email"));

        var thrown = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.Confirm(tokenId));

        thrown.ValidationErrors.Should().ContainKey(nameof(UserEmailChange.Email))
            .WhoseValue.Should().Be(ValidationError.Taken);
        await _confirmationRepository.Received(1).MarkTokenUsed(tokenId);
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
