using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

    /// <summary>
    /// One sentence for both ways of filing a complaint about oneself.
    /// </summary>
    private const string ComplainAboutYourself = "Нельзя пожаловаться на себя";

    /// <summary>
    /// The refusal of an answer written into a closed ticket, on both ways of
    /// writing one.
    /// </summary>
    private const string TicketClosed = "Обращение закрыто";

    /// <summary>
    /// The refusal of a second closing without a word, whether the first one was
    /// read before the request or landed beside it.
    /// </summary>
    private const string TicketAlreadyClosed = "Обращение уже закрыто";

    /// <summary>
    /// The refusal of an answer past the last one a conversation takes.
    /// </summary>
    private const string ConversationFull = "В обращении уже 100 ответов, для продолжения подайте новое обращение";

    /// <summary>
    /// The refusal of a status sent by the side of a ticket.
    /// </summary>
    private const string StatusIsModerations = "Статус обращения меняет модерация, а не его автор";

    private readonly IValidator<CreateTicket> _createValidator;
    private readonly IValidator<CreateTicketIntake> _createIntakeValidator;
    private readonly IValidator<CreateTicketComment> _createCommentValidator;
    private readonly ITicketRepository _ticketRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;
    private readonly IBbTagNameCase _tagNames;

    /// <inheritdoc />
    public TicketService(
        IValidator<CreateTicket> createValidator,
        IValidator<CreateTicketIntake> createIntakeValidator,
        IValidator<CreateTicketComment> createCommentValidator,
        ITicketRepository ticketRepository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer,
        IBbTagNameCase tagNames)
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
        _tagNames = tagNames;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetTickets(PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        var caller = _identityProvider.Current.User;
        var visibleSubtypes = TicketVisibility.Subtypes(caller.Role);

        IReadOnlyCollection<TicketSubtype>? filter;
        if (subtype.HasValue)
        {
            if (visibleSubtypes != null && !visibleSubtypes.Contains(subtype.Value))
            {
                // Requested subtype is outside of the caller visibility scope
                return ([], PagingResult.Empty(query.Take));
            }

            filter = [subtype.Value];
        }
        else
        {
            filter = visibleSubtypes;
        }

        // A complaint about the caller is not his to moderate, and the queue holds
        // exactly what the page of a ticket opens for him.
        return await _ticketRepository.GetTickets(query, status, filter, caller.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetMyFiledTickets(
        PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        // The reporter owns these tickets, so no subtype visibility gating is
        // needed — the filters are the user's own view preference.
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
        // Token-gated public access: possession of the token IS the
        // authorization, so there is no role or ownership check here. The
        // repository already treats an empty/unknown token as "not found".
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

        // The violator is named in the text, as on the intake path: the ticket is
        // read as its text and nothing beside it.
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
            // Report bodies render on the moderation surface where [mod] is a
            // privileged block; strip it for non-moderator reporters, exactly
            // as the public intake path does.
            Description = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(description, currentUser.Role), BbTagSet.Common),
            Comment = ModBlockSanitizer.SanitizeForAuthor(createTicket.Comment, currentUser.Role)
        };

        return await CreateAndAnnounce(entity, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> CreateIntakeTicket(CreateTicketIntake createTicketIntake, CancellationToken ct = default)
    {
        await _createIntakeValidator.ValidateAndThrowAsync(createTicketIntake, ct);

        // Guests are allowed to submit — losing account access must not lock
        // a user out of support. For authenticated users the author identity
        // is recorded; for guests the contact goes to GuestEmail.
        var currentUser = _identityProvider.Current.User;
        var isAuthenticated = currentUser.IsAuthenticated;

        // The violator is a name the reader typed into the form, so what is wrong
        // with it is answered under that field: an unknown name used to be the
        // 404 of the lookup, which the form could put nowhere but over the
        // button, and the reader could not tell which of his words it meant.
        Guid? targetUserId = null;
        string? violator = null;
        if (!string.IsNullOrWhiteSpace(createTicketIntake.TargetUsername))
        {
            var (found, userId) = await _userLookupService.FindUserIdAsync(
                createTicketIntake.TargetUsername.Trim(), ct);
            if (!found)
            {
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    [TargetUsernameField] = RefusalMessage.UserNotFound
                });
            }

            if (isAuthenticated && userId == currentUser.UserId)
            {
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    [TargetUsernameField] = ComplainAboutYourself
                });
            }

            targetUserId = userId;
            // The name as the site spells it, not as it was typed: the lookup
            // ignores the case, the line is read by people.
            violator = (await _userLookupService.GetAsync(userId)).Username;
        }

        // The ticket body is the intake message; the fields of the form that are
        // not the message (the violator, the link, the contact) are appended as
        // lines of it, in the order the form asks them, so no form data is lost
        // and the ticket is read as its text and nothing beside it. A guest's
        // address is that line too, where moderation reads it; GuestEmail keeps
        // it as a field of its own as well.
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

        var entity = new CreateTicketEntity
        {
            TicketId = _guidFactory.Create(),
            ReporterUserId = isAuthenticated ? currentUser.UserId : null,
            TargetUserId = targetUserId,
            GuestEmail = isAuthenticated ? null : NormalizeOptional(createTicketIntake.Contact),
            // Guests have no account to track their ticket from, so they get a
            // one-time unguessable tracking token; authenticated authors use
            // "Мои обращения" instead and get no token.
            TrackingToken = isAuthenticated ? null : GenerateTrackingToken(),
            EntityId = null,
            EntityType = string.Empty,
            Status = TicketStatus.WaitingForModeration,
            Subtype = createTicketIntake.Subtype,
            CreatedUtc = _dateTimeProvider.Now,
            // Ticket bodies render on the moderation surface where [mod] is a
            // privileged block; strip it for non-moderator authors. Guests
            // (Role=Guest) are always stripped.
            Description = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(description, currentUser.Role), BbTagSet.Common),
            // The forms send no subject; an empty one is what the ticket screens
            // replace with the category name.
            Comment = NormalizeOptional(createTicketIntake.Subject) ?? string.Empty
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

        // The author answers as the author whatever his role: a moderator who filed
        // a ticket is its side, and his answer is a reply, not moderation's. Whoever
        // else got past ReadableTicket moderates the ticket.
        if (IsAuthor(ticket, caller.UserId))
        {
            await Reply(ticket, caller.UserId, caller.Role, createComment, ct);
        }
        else
        {
            await Answer(ticket, caller.UserId, caller.Role, createComment, ct);
        }

        // The page reads the ticket back whole: the answer may have moved it to a
        // status that changes what the page offers, and the answer of moderation
        // may have written no comment at all.
        return await GetTicket(ticketId, ct);
    }

    /// <inheritdoc />
    public async Task<TicketComment> CreateTrackedComment(string token, CreateTicketComment createComment,
        CancellationToken ct = default)
    {
        await _createCommentValidator.ValidateAndThrowAsync(createComment, ct);

        // The token is the credential, exactly as on the tracking page, and the
        // comment it writes is the guest's: signed by nobody whoever holds the
        // token, and stripped of [mod] the way every guest text is.
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
    /// The ticket, if the caller may read it: its author, or a moderator who
    /// moderates it (TicketVisibility).
    /// </summary>
    /// <remarks>
    /// Everybody else is answered 404 and not 403, so that the address is not an
    /// existence oracle for tickets probed by GUID: an existing ticket the caller
    /// may not touch must be indistinguishable from an identifier that addresses
    /// nothing. That includes the moderator a complaint names, who is an ordinary
    /// user to it, and an ordinary user never learns of a complaint about himself.
    /// </remarks>
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
    /// The answer of the side that filed the ticket, from his account or by the
    /// tracking token: it leaves the ticket waiting for moderation and is news to
    /// the moderators who answered him.
    /// </summary>
    /// <remarks>
    /// The status is moderation's to choose. The author who sends one is refused
    /// rather than ignored: a moderator who filed a complaint does not close it as
    /// spam in the name of moderation, and a request that asks for more than it
    /// gets would read as done.
    /// </remarks>
    private async Task<TicketComment> Reply(Ticket ticket, Guid? authorId, UserRole role,
        CreateTicketComment reply, CancellationToken ct)
    {
        if (reply.Status.HasValue)
        {
            throw new HttpException(HttpStatusCode.Forbidden, StatusIsModerations);
        }

        var comment = await WriteComment(ticket, authorId, role, reply.Text, TicketStatus.WaitingForModeration, ct);
        await _eventProducer.SendAsync(EventType.TicketReplied, comment.TicketCommentId);
        return comment;
    }

    /// <summary>
    /// The answer of moderation: the text, if any, and the status the moderator
    /// chose with it, written together.
    /// </summary>
    /// <remarks>
    /// An answer with a text is one write, the comment and the status both or
    /// neither, so a closing never stands without the last word that came with
    /// it. One without a text only moves the status, and the validator lets that
    /// through for a closing alone. Either is the conditional write of an open
    /// ticket: of two answers that close it at once, the second one is refused.
    ///
    /// The author is told once per answer, of what the answer did: an answer that
    /// waits for him, or the closing, whose page shows the last word if there was
    /// one. Only an author with an account is told: a guest reads it on the
    /// tracking page, which says so itself. Spam is closed without telling
    /// anybody: whoever sent it is the last person the site has anything to say
    /// to.
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
                throw new HttpException(HttpStatusCode.BadRequest, TicketAlreadyClosed);
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

    /// <summary>
    /// Writes a comment into the conversation and moves the ticket to the status
    /// that comes with it.
    /// </summary>
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
            // The editor of comments, [mod] included for moderation; the author of
            // a ticket without the role loses the block, as in every comment.
            Text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(text, role), BbTagSet.WithModBlock),
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
                throw new HttpException(HttpStatusCode.BadRequest, ConversationFull);
        }

        return comment!;
    }

    /// <summary>
    /// Stores a ticket and announces it.
    /// </summary>
    /// <remarks>
    /// Both intake paths go through here so that a new way of filing a ticket
    /// cannot be added without the moderators hearing about it. The queue is the
    /// only other place a ticket shows up, and nobody is asked to watch it.
    /// </remarks>
    private async Task<Ticket> CreateAndAnnounce(CreateTicketEntity entity, CancellationToken ct)
    {
        var ticket = await _ticketRepository.Create(entity, ct);
        await _eventProducer.SendAsync(EventType.TicketCreated, ticket.TicketId);
        return ticket;
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
            // Whoever reads the ticket here writes in it: its author, a moderator
            // who moderates it, the holder of its token.
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
        ticket.ReporterUserId.HasValue && ticket.ReporterUserId.Value == userId;

    private static bool IsOpen(Ticket ticket) =>
        ticket.Status is TicketStatus.WaitingForModeration or TicketStatus.WaitingForUser;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Generates a 128-bit URL-safe tracking token. The token is the only
    /// credential a guest needs to read their ticket, so it is produced from a
    /// cryptographic RNG rather than a predictable identifier.
    /// </summary>
    private static string GenerateTrackingToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
