using System;
using System.Threading;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace DM.Domain.Moderation.Tests.Dsl;

/// <summary>
/// The ticket service over substitutes of everything it asks: validators that
/// pass, a signed-in moderator, a clock that stands still and conversations
/// that hold nothing until a test writes into one.
/// </summary>
public abstract class TicketServiceTestBase : UnitTestBase
{
    protected readonly IValidator<CreateTicket> createValidator;
    protected readonly IValidator<CreateTicketIntake> createIntakeValidator;
    protected readonly IValidator<CreateTicketComment> createCommentValidator;
    protected readonly ITicketRepository ticketRepository;
    protected readonly IUserLookupService userLookupService;
    protected readonly IIdentityProvider identityProvider;
    protected readonly IGuidFactory guidFactory;
    protected readonly IDateTimeProvider dateTimeProvider;
    protected readonly IEventProducer eventProducer;
    private protected readonly TicketService service;
    protected readonly Guid currentUserId = Guid.NewGuid();
    protected readonly Guid targetUserId = Guid.NewGuid();
    protected readonly Guid ticketId = Guid.NewGuid();
    protected readonly Guid commentId = Guid.NewGuid();
    protected readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    protected TicketServiceTestBase()
    {
        createValidator = Mock<IValidator<CreateTicket>>();
        createValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicket>>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        createIntakeValidator = Mock<IValidator<CreateTicketIntake>>();
        createIntakeValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicketIntake>>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        createCommentValidator = Mock<IValidator<CreateTicketComment>>();
        createCommentValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicketComment>>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        ticketRepository = Mock<ITicketRepository>();
        userLookupService = Mock<IUserLookupService>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        eventProducer = Mock<IEventProducer>();

        SetCurrentUser(UserRole.Moderator);
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(ticketId);
        ticketRepository.GetComments(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TicketComment>());

        service = new TicketService(
            createValidator,
            createIntakeValidator,
            createCommentValidator,
            ticketRepository,
            userLookupService,
            identityProvider,
            guidFactory,
            dateTimeProvider,
            eventProducer);
    }

    protected void SetCurrentUser(UserRole role, AccessPolicy accessPolicy = AccessPolicy.NotSpecified)
    {
        var identity = Identities.User(currentUserId, "CurrentUser", role);
        identity.User.AccessPolicy = accessPolicy;
        identityProvider.Current.Returns(identity);
    }

    /// <summary>
    /// The conditional closing goes through, as it does when nobody closed the
    /// ticket in the meantime.
    /// </summary>
    protected void ClosingSucceeds() =>
        ticketRepository.Close(Arg.Any<Guid>(), Arg.Any<TicketStatus>(), Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

    /// <summary>
    /// An open ticket of a user complaint, filed by the given reporter, that the
    /// repository takes a comment into; the entity it was given is read back by
    /// the function returned.
    /// </summary>
    protected Func<CreateTicketCommentEntity> OpenTicketTakingComments(Guid? reporterUserId,
        TicketSubtype subtype = TicketSubtype.UserComplaint, Guid? targetUserId = null)
    {
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = reporterUserId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = subtype,
            TargetUserId = targetUserId
        });
        return CommentsTaken();
    }

    /// <summary>
    /// The identifiers the factory hands out from here on are the comment's.
    /// </summary>
    private void CommentIdentifiers()
    {
        guidFactory.Create().Returns(commentId);
    }

    /// <summary>
    /// The repository takes the comment it is given; the entity and the status it
    /// was asked to move the ticket to are read back by the function returned.
    /// </summary>
    protected Func<CreateTicketCommentEntity> CommentsTaken() => CommentsTaken(out _);

    protected Func<CreateTicketCommentEntity> CommentsTaken(out Func<TicketStatus> statusAfter)
    {
        CommentIdentifiers();
        CreateTicketCommentEntity? captured = null;
        TicketStatus? status = null;
        ticketRepository.AddComment(Arg.Any<CreateTicketCommentEntity>(), Arg.Any<TicketStatus>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => (TicketCommentOutcome.Added, new TicketComment
            {
                TicketCommentId = ci.ArgAt<CreateTicketCommentEntity>(0).TicketCommentId,
                TicketId = ticketId,
                Text = ci.ArgAt<CreateTicketCommentEntity>(0).Text
            }))
            .AndDoes(ci =>
            {
                captured = ci.ArgAt<CreateTicketCommentEntity>(0);
                status = ci.ArgAt<TicketStatus>(1);
            });
        statusAfter = () => status ?? throw new InvalidOperationException("nothing was written");
        return () => captured ?? throw new InvalidOperationException("nothing was written");
    }
}
