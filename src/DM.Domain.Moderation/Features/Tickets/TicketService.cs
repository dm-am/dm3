using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <inheritdoc />
internal class TicketService : ITicketService
{
    /// <summary>
    /// The field of the intake form that names the violator, as the request
    /// spells it: the refusal of the name stands under that field.
    /// </summary>
    private const string TargetUsernameField = "targetUsername";

    /// <summary>
    /// The caption of the line that names the violator in the text of a complaint,
    /// in the word of the field of the form it was typed into.
    /// </summary>
    private const string ViolatorCaption = "Нарушитель";

    private const string ComplainAboutYourself = "Нельзя пожаловаться на себя";

    private const string TicketClosed = "Обращение закрыто";

    private readonly IValidator<CreateTicket> _createValidator;
    private readonly IValidator<CreateTicketIntake> _createIntakeValidator;
    private readonly IValidator<CreateTicketComment> _createCommentValidator;
    private readonly ITicketRepository _ticketRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;

    public TicketService(
        IValidator<CreateTicket> createValidator,
        IValidator<CreateTicketIntake> createIntakeValidator,
        IValidator<CreateTicketComment> createCommentValidator,
        ITicketRepository ticketRepository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer)
    {
        _createValidator = createValidator;
        _createIntakeValidator = createIntakeValidator;
        _createCommentValidator = createCommentValidator;
        _ticketRepository = ticketRepository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetTickets(PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        var caller = _identityProvider.Current.User;
        var visibleSubtypes = TicketVisibility.Subtypes(caller.Role);

        if (subtype.HasValue && visibleSubtypes != null && !visibleSubtypes.Contains(subtype.Value))
        {
            return ([], PagingResult.Empty(query.Take));
        }

        IReadOnlyCollection<TicketSubtype>? filter = subtype.HasValue ? [subtype.Value] : visibleSubtypes;

        // A complaint about the caller is not his to moderate, and the queue holds
        // exactly what the page of a ticket opens for him.
        return await _ticketRepository.GetTickets(query, status, filter, caller.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetMyFiledTickets(
        PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        // The reporter's own tickets: no visibility rule applies.
        var userId = _identityProvider.Current.User.UserId;
        return await _ticketRepository.GetUserTickets(userId, query, status, subtype, ct);
    }

    /// <inheritdoc />
    public async Task<TicketDetails> GetTicket(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await ReadableTicket(ticketId, ct);
        return Detailed(ticket, await _ticketRepository.GetComments(ticketId, ct));
    }

    /// <inheritdoc />
    public async Task<TicketDetails?> GetTicketByTrackingToken(string token, CancellationToken ct = default)
    {
        // The token is the credential: no role or ownership is asked.
        var ticket = await _ticketRepository.GetByTrackingToken(token, ct);
        if (ticket == null)
        {
            return null;
        }

        return Detailed(ticket, await _ticketRepository.GetComments(ticket.TicketId, ct), byToken: true);
    }

    /// <inheritdoc />
    public async Task<Ticket> CreateTicket(CreateTicket createTicket, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createTicket, ct);

        var currentUser = _identityProvider.Current.User;
        var targetUser = await _userLookupService.GetAsync(createTicket.TargetUsername);

        if (targetUser.UserId == currentUser.UserId)
        {
            throw new HttpException(HttpStatusCode.BadRequest, ComplainAboutYourself);
        }

        // The violator is named in the text, as on the intake path.
        var description = $"{createTicket.Description}\n\n{ViolatorCaption}: {targetUser.Username}";

        var entity = new CreateTicketEntity
        {
            TicketId = _guidFactory.Create(),
            ReporterUserId = currentUser.UserId,
            TargetUserId = targetUser.UserId,
            EntityId = createTicket.EntityId,
            EntityType = createTicket.EntityType ?? string.Empty,
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.UserComplaint,
            CreatedUtc = _dateTimeProvider.Now,
            // [mod] is a privileged block on the moderation surface, as on the intake path.
            Description = ModBlockSanitizer.SanitizeForAuthor(description, currentUser.Role),
            Comment = ModBlockSanitizer.SanitizeForAuthor(createTicket.Comment, currentUser.Role)
        };

        return await CreateAndAnnounce(entity, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> CreateIntakeTicket(CreateTicketIntake createTicketIntake, CancellationToken ct = default)
    {
        await _createIntakeValidator.ValidateAndThrowAsync(createTicketIntake, ct);

        // A guest may file: losing the account must not lock anybody out of support.
        var currentUser = _identityProvider.Current.User;
        var isAuthenticated = currentUser.IsAuthenticated;

        var (targetUserId, violator) = await ResolveViolator(createTicketIntake, currentUser, ct);
        var description = IntakeDescription(createTicketIntake, violator);

        var entity = new CreateTicketEntity
        {
            TicketId = _guidFactory.Create(),
            ReporterUserId = isAuthenticated ? currentUser.UserId : null,
            TargetUserId = targetUserId,
            GuestEmail = isAuthenticated ? null : NormalizeOptional(createTicketIntake.Contact),
            // A guest has no account to track the ticket from; an author reads "Мои обращения".
            TrackingToken = isAuthenticated ? null : GenerateTrackingToken(),
            EntityId = null,
            EntityType = string.Empty,
            Status = TicketStatus.WaitingForModeration,
            Subtype = createTicketIntake.Subtype,
            CreatedUtc = _dateTimeProvider.Now,
            // [mod] is a privileged block on the moderation surface; a guest always loses it.
            Description = ModBlockSanitizer.SanitizeForAuthor(description, currentUser.Role),
            // An empty subject is what the ticket screens replace with the category name.
            Comment = AuthoredText.AsTyped(createTicketIntake.Subject) ?? string.Empty
        };

        return await CreateAndAnnounce(entity, ct);
    }

    /// <inheritdoc />
    public async Task<TicketDetails> CreateComment(Guid ticketId, CreateTicketComment createComment,
        CancellationToken ct = default)
    {
        await _createCommentValidator.ValidateAndThrowAsync(createComment, ct);

        var caller = _identityProvider.Current.User;
        var ticket = await ReadableTicket(ticketId, ct);

        // The author answers as the author whatever his role; whoever else got past
        // ReadableTicket moderates the ticket.
        if (IsAuthor(ticket, caller.UserId))
        {
            await Reply(ticket, caller.UserId, caller.Role, createComment, ct);
        }
        else
        {
            await Answer(ticket, caller.UserId, caller.Role, createComment, ct);
        }

        // Read back whole: the status may have moved, and an answer may have written no comment.
        return await GetTicket(ticketId, ct);
    }

    /// <inheritdoc />
    public async Task<TicketComment> CreateTrackedComment(string token, CreateTicketComment createComment,
        CancellationToken ct = default)
    {
        await _createCommentValidator.ValidateAndThrowAsync(createComment, ct);

        // The token is the credential, and the comment is the guest's whoever holds it.
        var ticket = await _ticketRepository.GetByTrackingToken(token, ct);
        if (ticket == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TicketNotFound);
        }

        return await Reply(ticket, authorId: null, UserRole.Guest, createComment, ct);
    }

    /// <inheritdoc />
    public async Task<Dictionary<TicketStatus, int>> GetTicketStats(CancellationToken ct = default)
    {
        var caller = _identityProvider.Current.User;
        return await _ticketRepository.GetTicketCounts(TicketVisibility.Subtypes(caller.Role), caller.UserId, ct);
    }

    /// <summary>
    /// The ticket, if the caller may read it: its author, or a moderator who moderates it
    /// (TicketVisibility). Anybody else is answered 404 and not 403, so the address tells
    /// nothing of a ticket he may not touch.
    /// </summary>
    private async Task<Ticket> ReadableTicket(Guid ticketId, CancellationToken ct)
    {
        var caller = _identityProvider.Current.User;
        var ticket = await _ticketRepository.Get(ticketId, ct);

        if (ticket == null ||
            !(IsAuthor(ticket, caller.UserId) ||
              TicketVisibility.Moderates(caller.UserId, caller.Role, ticket.Subtype, ticket.TargetUserId)))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.TicketNotFound);
        }

        return ticket;
    }

    /// <summary>
    /// The answer of the side that filed the ticket, from his account or by the tracking
    /// token: it leaves the ticket waiting for moderation. A status he sends is refused
    /// rather than ignored, so the request does not read as done.
    /// </summary>
    private async Task<TicketComment> Reply(Ticket ticket, Guid? authorId, UserRole role,
        CreateTicketComment reply, CancellationToken ct)
    {
        if (reply.Status.HasValue)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Статус обращения меняет модерация, а не его автор");
        }

        var comment = await WriteComment(ticket, authorId, role, reply.Text, TicketStatus.WaitingForModeration, ct);
        await _eventProducer.SendAsync(EventType.TicketReplied, comment.TicketCommentId);
        return comment;
    }

    /// <summary>
    /// The answer of moderation: the text, if any, and the status the moderator chose,
    /// written together into an open ticket, so of two closings the second is refused.
    /// The comment and the status are one write, so a closing never stands without the
    /// last word that came with it.
    /// </summary>
    /// <remarks>
    /// The author with an account is told once per answer; a guest reads it on the
    /// tracking page. Spam is closed without telling anybody, its sender included.
    /// </remarks>
    private async Task Answer(Ticket ticket, Guid moderatorId, UserRole role, CreateTicketComment answer,
        CancellationToken ct)
    {
        var status = answer.Status ?? TicketStatus.WaitingForUser;

        TicketComment? comment = null;
        if (string.IsNullOrWhiteSpace(answer.Text))
        {
            if (!IsOpen(ticket) ||
                !await _ticketRepository.Close(ticket.TicketId, status, moderatorId, _dateTimeProvider.Now, ct))
            {
                throw new HttpException(HttpStatusCode.BadRequest, "Обращение уже закрыто");
            }
        }
        else
        {
            comment = await WriteComment(ticket, moderatorId, role, answer.Text, status, ct);
        }

        if (!ticket.ReporterUserId.HasValue)
        {
            return;
        }

        switch (status)
        {
            case TicketStatus.WaitingForUser:
                await _eventProducer.SendAsync(EventType.TicketAnswered, comment!.TicketCommentId);
                break;
            case TicketStatus.Closed:
                await _eventProducer.SendAsync(EventType.TicketResolved, ticket.TicketId);
                break;
        }
    }

    private async Task<TicketComment> WriteComment(Ticket ticket, Guid? authorId, UserRole role,
        string text, TicketStatus statusAfter, CancellationToken ct)
    {
        if (!IsOpen(ticket))
        {
            throw new HttpException(HttpStatusCode.BadRequest, TicketClosed);
        }

        var entity = new CreateTicketCommentEntity
        {
            TicketCommentId = _guidFactory.Create(),
            TicketId = ticket.TicketId,
            AuthorId = authorId,
            // The editor of comments: [mod] for moderation, unwrapped for anybody below it.
            Text = ModBlockSanitizer.SanitizeForAuthor(text, role),
            CreatedUtc = _dateTimeProvider.Now
        };

        var (outcome, comment) = await _ticketRepository.AddComment(
            entity, statusAfter, TicketTextLimits.MaxComments, ct);

        switch (outcome)
        {
            case TicketCommentOutcome.Closed:
                // Closed between the read above and the write: the write decides.
                throw new HttpException(HttpStatusCode.BadRequest, TicketClosed);
            case TicketCommentOutcome.Full:
                throw new HttpException(HttpStatusCode.BadRequest,
                    $"В обращении уже {TicketTextLimits.MaxComments} ответов, для продолжения подайте новое обращение");
        }

        return comment!;
    }

    /// <summary>
    /// Stores a ticket and announces it: every way of filing goes through here, so none
    /// can be added without the moderators hearing of it.
    /// </summary>
    private async Task<Ticket> CreateAndAnnounce(CreateTicketEntity entity, CancellationToken ct)
    {
        var ticket = await _ticketRepository.Create(entity, ct);
        await _eventProducer.SendAsync(EventType.TicketCreated, ticket.TicketId);
        return ticket;
    }

    /// <summary>
    /// The violator an intake complaint names, by his identifier and by his name as the
    /// site spells it, or neither. The name was typed into the form, so what is wrong
    /// with it is answered under that field.
    /// </summary>
    private async Task<(Guid? targetUserId, string? violator)> ResolveViolator(
        CreateTicketIntake createTicketIntake, AuthenticatedUser currentUser, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(createTicketIntake.TargetUsername))
        {
            return (null, null);
        }

        var (found, userId) = await _userLookupService.FindUserIdAsync(
            createTicketIntake.TargetUsername.Trim(), ct);
        if (!found)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [TargetUsernameField] = RefusalMessage.UserNotFound
            });
        }

        if (currentUser.IsAuthenticated && userId == currentUser.UserId)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [TargetUsernameField] = ComplainAboutYourself
            });
        }

        // The lookup ignores the case, the line is read by people.
        return (userId, (await _userLookupService.GetAsync(userId)).Username);
    }

    /// <summary>
    /// The text of an intake ticket: the message, and after it the fields of the form
    /// that are not the message (the violator, the link, the contact) as lines of it, in
    /// the order the form asks them. A guest's address is that line too; GuestEmail keeps
    /// it as a field of its own as well.
    /// </summary>
    private static string IntakeDescription(CreateTicketIntake createTicketIntake, string? violator)
    {
        var description = createTicketIntake.Text;
        if (violator != null)
        {
            description += $"\n\n{ViolatorCaption}: {violator}";
        }

        if (!string.IsNullOrWhiteSpace(createTicketIntake.ViolationUrl))
        {
            description += $"\n\n{LinkCaption(createTicketIntake.Subtype)}: {createTicketIntake.ViolationUrl}";
        }

        if (!string.IsNullOrWhiteSpace(createTicketIntake.Contact))
        {
            description += $"\n\nКонтакт для ответа: {createTicketIntake.Contact.Trim()}";
        }

        return description;
    }

    /// <summary>
    /// What the link a complaint carries points at, in the words of the field
    /// of the form it was typed into: a complaint about a decision links the
    /// decision, every other one the violation.
    /// </summary>
    private static string LinkCaption(TicketSubtype subtype) => subtype switch
    {
        TicketSubtype.ModeratorDecisionComplaint or TicketSubtype.SeniorModeratorDecisionComplaint
            => "Ссылка на решение",
        _ => "Ссылка на нарушение"
    };

    /// <summary>
    /// The ticket with its conversation and with what the caller may do in it.
    /// </summary>
    /// <param name="ticket">The ticket the caller may read</param>
    /// <param name="comments">Its conversation</param>
    /// <param name="byToken">Read by the tracking token: the reader is its guest
    /// author, whoever is signed in</param>
    private TicketDetails Detailed(Ticket ticket, IReadOnlyList<TicketComment> comments, bool byToken = false)
    {
        var hasRoom = IsOpen(ticket) && comments.Count < TicketTextLimits.MaxComments;
        return new TicketDetails
        {
            TicketId = ticket.TicketId,
            ReporterUserId = ticket.ReporterUserId,
            Reporter = ticket.Reporter,
            IsGuest = ticket.IsGuest,
            TargetUserId = ticket.TargetUserId,
            GuestEmail = ticket.GuestEmail,
            TrackingToken = ticket.TrackingToken,
            EntityId = ticket.EntityId,
            EntityType = ticket.EntityType,
            Status = ticket.Status,
            Subtype = ticket.Subtype,
            CreatedUtc = ticket.CreatedUtc,
            Description = ticket.Description,
            Comment = ticket.Comment,
            CommentsCount = ticket.CommentsCount,
            ResolvedUtc = ticket.ResolvedUtc,
            ResolvedByUsername = ticket.ResolvedByUsername,
            Comments = comments,
            // Whoever reads the ticket here writes in it.
            CanComment = hasRoom,
            CanResolve = !byToken && MayResolve(ticket)
        };
    }

    /// <summary>
    /// Whether the caller answers as moderation, choosing the status with his
    /// answer: the ticket is open, and he moderates it without being its side,
    /// neither its violator nor its author.
    /// </summary>
    private bool MayResolve(Ticket ticket)
    {
        var caller = _identityProvider.Current.User;
        return IsOpen(ticket) &&
               !IsAuthor(ticket, caller.UserId) &&
               TicketVisibility.Moderates(caller.UserId, caller.Role, ticket.Subtype, ticket.TargetUserId);
    }

    private static bool IsAuthor(Ticket ticket, Guid userId) =>
        ticket.ReporterUserId == userId;

    private static bool IsOpen(Ticket ticket) =>
        ticket.Status is TicketStatus.WaitingForModeration or TicketStatus.WaitingForUser;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// The only credential a guest needs to read his ticket, so 128 bits from a
    /// cryptographic generator.
    /// </summary>
    private static string GenerateTrackingToken() =>
        RandomNumberGenerator.GetHexString(32, lowercase: true);
}
