using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <inheritdoc />
internal class TicketService : ITicketService
{
    /// <summary>
    /// Subtypes visible to a junior moderator (per the design doc)
    /// </summary>
    private static readonly TicketSubtype[] ModeratorSubtypes =
    [
        TicketSubtype.UserComplaint,
        TicketSubtype.SiteImprovementSuggestion
    ];

    /// <summary>
    /// Subtypes visible to a senior moderator: junior moderator scope plus
    /// complaints about junior moderator decisions
    /// </summary>
    private static readonly TicketSubtype[] SeniorModeratorSubtypes =
    [
        TicketSubtype.UserComplaint,
        TicketSubtype.SiteImprovementSuggestion,
        TicketSubtype.ModeratorDecisionComplaint
    ];

    private readonly IValidator<CreateTicket> _createValidator;
    private readonly IValidator<CreateTicketIntake> _createIntakeValidator;
    private readonly IValidator<ResolveTicket> _resolveValidator;
    private readonly ITicketRepository _ticketRepository;
    private readonly IWarningService _warningService;
    private readonly IBanService _banService;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;

    /// <inheritdoc />
    public TicketService(
        IValidator<CreateTicket> createValidator,
        IValidator<CreateTicketIntake> createIntakeValidator,
        IValidator<ResolveTicket> resolveValidator,
        ITicketRepository ticketRepository,
        IWarningService warningService,
        IBanService banService,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IEventProducer eventProducer)
    {
        _createValidator = createValidator;
        _createIntakeValidator = createIntakeValidator;
        _resolveValidator = resolveValidator;
        _ticketRepository = ticketRepository;
        _warningService = warningService;
        _banService = banService;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _eventProducer = eventProducer;
    }

    /// <summary>
    /// Subtypes visible to the given role, null meaning "everything".
    /// Server-side so that a junior moderator cannot request admin-only
    /// categories by tweaking the query string
    /// </summary>
    private static IReadOnlyCollection<TicketSubtype>? GetVisibleSubtypes(UserRole role) =>
        role switch
        {
            UserRole.Admin or UserRole.System => null,
            UserRole.SeniorModerator => SeniorModeratorSubtypes,
            _ => ModeratorSubtypes
        };

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetTickets(
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        var visibleSubtypes = GetVisibleSubtypes(_identityProvider.Current.User.Role);

        IReadOnlyCollection<TicketSubtype>? filter;
        if (subtype.HasValue)
        {
            if (visibleSubtypes != null && !visibleSubtypes.Contains(subtype.Value))
            {
                // Requested subtype is outside of the caller visibility scope
                return [];
            }

            filter = [subtype.Value];
        }
        else
        {
            filter = visibleSubtypes;
        }

        return await _ticketRepository.GetTickets(status, filter, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetMyAssignedTickets(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _ticketRepository.GetModeratorTickets(userId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetMyFiledTickets(
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        // The reporter owns these tickets, so no subtype visibility gating is
        // needed — the filters are the user's own view preference.
        var userId = _identityProvider.Current.User.UserId;
        return await _ticketRepository.GetUserTickets(userId, status, subtype, ct);
    }

    /// <inheritdoc />
    public async Task<TicketDetails?> GetTicket(Guid ticketId, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        // Detail path: includes the conversation thread. List endpoints keep
        // using the lighter repository projections without responses.
        var ticket = await _ticketRepository.GetDetails(ticketId, ct);

        // A missing ticket and a ticket outside the caller visibility must be
        // indistinguishable (both 404) so this endpoint is not an existence
        // oracle for out-of-scope tickets probed by GUID.
        if (ticket == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Ticket not found");
        }

        // The reporter can always read their own ticket.
        if (ticket.ReporterUserId.HasValue && ticket.ReporterUserId.Value == currentUser.UserId)
        {
            return ticket;
        }

        // Otherwise only a moderator within the subtype visibility scope may
        // read it (Admin/System see everything: GetVisibleSubtypes == null).
        var visible = GetVisibleSubtypes(currentUser.Role);
        if (currentUser.Role >= UserRole.Moderator && (visible == null || visible.Contains(ticket.Subtype)))
        {
            return ticket;
        }

        throw new HttpException(System.Net.HttpStatusCode.NotFound, "Ticket not found");
    }

    /// <inheritdoc />
    public async Task<Ticket> CreateTicket(CreateTicket createTicket, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createTicket, ct);

        var currentUser = _identityProvider.Current.User;
        var targetUser = await _userLookupService.GetAsync(createTicket.TargetUsername);

        if (targetUser.UserId == currentUser.UserId)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Cannot report yourself");
        }

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
            Description = ModBlockSanitizer.SanitizeForAuthor(createTicket.Description, currentUser.Role),
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

        GeneralUser? targetUser = null;
        if (!string.IsNullOrWhiteSpace(createTicketIntake.TargetUsername))
        {
            targetUser = await _userLookupService.GetAsync(createTicketIntake.TargetUsername);
        }

        // The ticket body is the intake message; the optional violation link
        // and the contact of an authenticated author (guest contacts live in
        // GuestEmail) are appended so no form data is lost.
        var description = createTicketIntake.Text;
        if (!string.IsNullOrWhiteSpace(createTicketIntake.ViolationUrl))
        {
            description += $"\n\nСсылка на нарушение: {createTicketIntake.ViolationUrl}";
        }

        if (isAuthenticated && !string.IsNullOrWhiteSpace(createTicketIntake.Contact))
        {
            description += $"\n\nКонтакт для ответа: {createTicketIntake.Contact}";
        }

        var entity = new CreateTicketEntity
        {
            TicketId = _guidFactory.Create(),
            ReporterUserId = isAuthenticated ? currentUser.UserId : null,
            TargetUserId = targetUser?.UserId,
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
            Description = ModBlockSanitizer.SanitizeForAuthor(description, currentUser.Role),
            Comment = createTicketIntake.Subject
        };

        return await CreateAndAnnounce(entity, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> AssignToMe(Guid ticketId, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        var ticket = await _ticketRepository.Get(ticketId, ct);

        if (ticket == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Ticket not found");
        }

        // Out-of-scope subtypes are invisible to this role: 404 (not 403) so a
        // junior moderator cannot assign / probe admin-only tickets by GUID.
        var visible = GetVisibleSubtypes(currentUser.Role);
        if (visible != null && !visible.Contains(ticket.Subtype))
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Ticket not found");
        }

        if (ticket.Status is TicketStatus.Closed or TicketStatus.Spam)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Cannot assign a closed ticket");
        }

        var updateEntity = new UpdateTicketEntity
        {
            TicketId = ticketId,
            AssignedModeratorId = currentUser.UserId
        };

        return await _ticketRepository.Update(updateEntity, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> ResolveTicket(Guid ticketId, ResolveTicket resolveTicket, CancellationToken ct = default)
    {
        await _resolveValidator.ValidateAndThrowAsync(resolveTicket, ct);

        var currentUser = _identityProvider.Current.User;
        var ticket = await _ticketRepository.Get(ticketId, ct);

        if (ticket == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Ticket not found");
        }

        // Out-of-scope subtypes are invisible to this role: 404 (not 403) so a
        // junior moderator cannot resolve / probe admin-only tickets by GUID.
        var visible = GetVisibleSubtypes(currentUser.Role);
        if (visible != null && !visible.Contains(ticket.Subtype))
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Ticket not found");
        }

        if (ticket.Status is TicketStatus.Closed or TicketStatus.Spam)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Ticket is already closed");
        }

        var now = _dateTimeProvider.Now;

        var updateEntity = new UpdateTicketEntity
        {
            TicketId = ticketId,
            Status = resolveTicket.Status,
            ResolvedUtc = now,
            Answer = resolveTicket.Answer
        };

        if ((resolveTicket.IssueWarning || resolveTicket.IssueBan) && ticket.TargetUsername == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest,
                "Cannot issue a warning or ban: the ticket has no target user");
        }

        // Issue warning if requested. Routed through IWarningService so it
        // inherits the moderator gate, the 0-6 points clamp and validation,
        // instead of writing to the warning repository directly.
        if (resolveTicket.IssueWarning && !string.IsNullOrEmpty(resolveTicket.WarningText))
        {
            var warning = await _warningService.CreateWarning(new CreateWarning
            {
                Username = ticket.TargetUsername!,
                EntityId = ticket.EntityId ?? ticketId,
                EntityType = ticket.EntityType,
                Points = resolveTicket.WarningPoints,
                Reason = resolveTicket.WarningText
            }, ct);
            updateEntity.WarningId = warning.WarningId;
        }

        // Issue ban if requested. Issuing a ban is a senior-moderator action,
        // so a plain moderator resolving a ticket cannot ban (403). Routed
        // through IBanService so it inherits the senior-moderator gate and the
        // already-banned conflict check, instead of writing to the ban
        // repository directly.
        if (resolveTicket.IssueBan && resolveTicket.BanDurationHours.HasValue)
        {
            if (currentUser.Role < UserRole.SeniorModerator)
            {
                throw new HttpException(System.Net.HttpStatusCode.Forbidden,
                    "Only senior moderators can issue bans");
            }

            var ban = await _banService.CreateBan(new CreateBan
            {
                Username = ticket.TargetUsername!,
                DurationHours = resolveTicket.BanDurationHours.Value,
                Comment = resolveTicket.BanComment ?? "Banned via ticket resolution",
                IsVoluntary = false
            }, ct);
            updateEntity.BanId = ban.BanId;
        }

        return await _ticketRepository.Update(updateEntity, ct);
    }

    /// <inheritdoc />
    public async Task<TicketDetails?> GetTicketByTrackingToken(string token, CancellationToken ct = default)
    {
        // Token-gated public access: possession of the token IS the
        // authorization, so there is no role or ownership check here. The
        // repository already treats an empty/unknown token as "not found".
        return await _ticketRepository.GetByTrackingToken(token, ct);
    }

    /// <inheritdoc />
    public async Task<Dictionary<TicketStatus, int>> GetTicketStats(CancellationToken ct = default)
    {
        var visibleSubtypes = GetVisibleSubtypes(_identityProvider.Current.User.Role);
        return await _ticketRepository.GetTicketCounts(visibleSubtypes, ct);
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
