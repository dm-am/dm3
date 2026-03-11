using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Core.Users;

namespace DM.Domain.Moderation.Features.Tickets;

/// <inheritdoc />
internal class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IWarningRepository _warningRepository;
    private readonly IBanRepository _banRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public TicketService(
        ITicketRepository ticketRepository,
        IWarningRepository warningRepository,
        IBanRepository banRepository,
        IUserLookupService userLookupService,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _ticketRepository = ticketRepository;
        _warningRepository = warningRepository;
        _banRepository = banRepository;
        _userLookupService = userLookupService;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetTickets(TicketStatus? status = null, CancellationToken ct = default)
    {
        return await _ticketRepository.GetTickets(status, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetMyAssignedTickets(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _ticketRepository.GetModeratorTickets(userId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetMyFiledTickets(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        return await _ticketRepository.GetUserTickets(userId, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket?> GetTicket(Guid ticketId, CancellationToken ct = default)
    {
        return await _ticketRepository.Get(ticketId, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> CreateTicket(CreateTicket createTicket, CancellationToken ct = default)
    {
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
            Status = TicketStatus.Open,
            CreatedUtc = _dateTimeProvider.Now,
            Description = createTicket.Description,
            Comment = createTicket.Comment
        };

        return await _ticketRepository.Create(entity, ct);
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

        if (ticket.Status != TicketStatus.Open)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Can only assign open tickets");
        }

        var updateEntity = new UpdateTicketEntity
        {
            TicketId = ticketId,
            AssignedModeratorId = currentUser.UserId,
            Status = TicketStatus.InProgress
        };

        return await _ticketRepository.Update(updateEntity, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> ResolveTicket(Guid ticketId, ResolveTicket resolveTicket, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        var ticket = await _ticketRepository.Get(ticketId, ct);

        if (ticket == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Ticket not found");
        }

        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Rejected)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Ticket is already closed");
        }

        var now = _dateTimeProvider.Now;

        // Need to get target user ID
        var targetUser = await _userLookupService.GetAsync(ticket.TargetUsername);

        var updateEntity = new UpdateTicketEntity
        {
            TicketId = ticketId,
            Status = resolveTicket.Status,
            ResolvedUtc = now,
            Answer = resolveTicket.Answer
        };

        // Issue warning if requested
        if (resolveTicket.IssueWarning && !string.IsNullOrEmpty(resolveTicket.WarningText))
        {
            var warningEntity = new CreateWarningEntity
            {
                WarningId = _guidFactory.Create(),
                TargetUserId = targetUser.UserId,
                AuthorId = currentUser.UserId,
                EntityId = ticket.EntityId ?? ticketId,
                EntityType = WarningEntityType.Unknown,
                CreatedUtc = now,
                Text = resolveTicket.WarningText,
                Points = resolveTicket.WarningPoints
            };
            var warning = await _warningRepository.Create(warningEntity, ct);
            updateEntity.WarningId = warning.WarningId;
        }

        // Issue ban if requested
        if (resolveTicket.IssueBan && resolveTicket.BanDurationHours.HasValue)
        {
            var banEntity = new CreateBanEntity
            {
                BanId = _guidFactory.Create(),
                TargetUserId = targetUser.UserId,
                AuthorId = currentUser.UserId,
                StartedUtc = now,
                EndedUtc = now.AddHours(resolveTicket.BanDurationHours.Value),
                Comment = resolveTicket.BanComment ?? "Banned via ticket resolution",
                AccessRestrictionPolicy = AccessPolicy.FullBan,
                IsVoluntary = false
            };
            var ban = await _banRepository.Create(banEntity, ct);
            updateEntity.BanId = ban.BanId;
        }

        return await _ticketRepository.Update(updateEntity, ct);
    }

    /// <inheritdoc />
    public async Task<Dictionary<TicketStatus, int>> GetTicketStats(CancellationToken ct = default)
    {
        return await _ticketRepository.GetTicketCounts(ct);
    }
}
