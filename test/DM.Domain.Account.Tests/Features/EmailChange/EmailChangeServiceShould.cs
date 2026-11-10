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
using Xunit;

namespace DM.Domain.Account.Tests.Features.EmailChange;

public class EmailChangeServiceShould : UnitTestBase
{
    private readonly IValidator<UserEmailChange> validator;
    private readonly ITokenFactory tokenFactory;
    private readonly IEmailChangeRepository repository;
    private readonly IEmailChangeConfirmationRepository confirmationRepository;
    private readonly IEmailChangeMailSender mailSender;
    private readonly IEmailChangeNoticeMailSender noticeMailSender;
    private readonly IEventProducer eventProducer;
    private readonly ISecurityAuditRepository auditService;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly EmailChangeService service;

    public EmailChangeServiceShould()
    {
        validator = Mock<IValidator<UserEmailChange>>();
        tokenFactory = Mock<ITokenFactory>();
        repository = Mock<IEmailChangeRepository>();
        confirmationRepository = Mock<IEmailChangeConfirmationRepository>();
        mailSender = Mock<IEmailChangeMailSender>();
        noticeMailSender = Mock<IEmailChangeNoticeMailSender>();
        eventProducer = Mock<IEventProducer>();
        auditService = Mock<ISecurityAuditRepository>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        var config = Options.Create(new TokenConfiguration
        {
            EmailChangeTokenLifetimeHours = 24
        });

        validator.ValidateAsync(
                Arg.Any<ValidationContext<UserEmailChange>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new EmailChangeService(
            validator,
            tokenFactory,
            repository,
            confirmationRepository,
            mailSender,
            noticeMailSender,
            eventProducer,
            auditService,
            dateTimeProvider,
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

        repository.FindUser(emailChange.Username).Returns((AuthenticatedUser?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.Change(emailChange));

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

        repository.FindUser(emailChange.Username).Returns(user);
        tokenFactory.Create(userId, TokenType.EmailChange).Returns(token);

        var result = await service.Change(emailChange);

        result.UserId.Should().Be(userId);
        result.Username.Should().Be("testuser");
        // One call, and it is the call that also retires the letters sent before
        // it: as two, a request from another tab slipped between them and left two
        // live links.
        //
        // The request puts the address in waiting: the account keeps answering by the
        // old one until the link is opened.
        await repository.Received(1).RequestChange(userId, emailChange.Email, token);
        await mailSender.Received(1).Send(emailChange.Email, secret);
        await mailSender.DidNotReceive().Send(emailChange.Email, tokenId);
        await eventProducer.Received(1).SendAsync(EventType.EmailChanged, userId);
        // The old address is warned while a new password can still call the move off.
        await noticeMailSender.Received(1).SendRequested(user.Email!, emailChange.Email);
    }

    [Fact]
    public async Task MoveTheAddressOnlyWhenTheLinkIsFollowed()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        repository.ApplyPendingEmail(ownerId)
            .Returns(new EmailMove("was@example.com", "now@example.com"));

        await service.Confirm(tokenId);

        // Confirmation is the moment the address changes, and the address the
        // account just left is told: without it the move is silent to an owner
        // who never opened the first letter.
        await repository.Received(1).ApplyPendingEmail(ownerId);
        await confirmationRepository.Received(1).MarkTokenUsed(tokenId);
        await noticeMailSender.Received(1).SendCompleted("was@example.com", "now@example.com");
    }

    [Fact]
    public async Task RefuseALiveTokenWithNothingPending()
    {
        // A link opened a second time, or a request withdrawn before it was
        // followed. The answer is the one an expired link gets: there is nothing
        // to confirm.
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        repository.ApplyPendingEmail(ownerId).Returns((EmailMove?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.Confirm(tokenId));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await confirmationRepository.DidNotReceive().MarkTokenUsed(Arg.Any<Guid>());
        await noticeMailSender.DidNotReceiveWithAnyArgs().SendCompleted(default!, default!);
    }

    // The check that the address is free runs when the change is asked for, and nothing holds it
    // between then and the click: whoever registers or moves onto it first gets it, and the unique
    // index is what says so.
    [Fact]
    public async Task RefuseAnAddressTakenWhileTheLetterWaited()
    {
        var tokenId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns(ownerId);
        repository.ApplyPendingEmail(ownerId)
            .Returns<EmailMove?>(_ => throw new DuplicateEntityException("Duplicate account email"));

        var thrown = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => service.Confirm(tokenId));

        thrown.ValidationErrors.Should().ContainKey(nameof(UserEmailChange.Email))
            .WhoseValue.Should().Be(ValidationError.Taken);
        await confirmationRepository.Received(1).MarkTokenUsed(tokenId);
    }

    [Fact]
    public async Task ThrowWhenConfirmingInvalidToken()
    {
        var tokenId = Guid.NewGuid();
        confirmationRepository.FindEmailChangeTokenOwner(tokenId, Arg.Any<DateTimeOffset>()).Returns((Guid?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.Confirm(tokenId));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
