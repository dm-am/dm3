using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Moderation.Warnings;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Administration;

namespace DM.Services.Community.BusinessProcesses.Moderation.Tickets;

/// <inheritdoc />
internal class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IWarningRepository _warningRepository;
    private readonly IBanRepository _banRepository;
    private readonly IUserReadingRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public TicketService(
        ITicketRepository ticketRepository,
        IWarningRepository warningRepository,
        IBanRepository banRepository,
        IUserReadingRepository userRepository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _ticketRepository = ticketRepository;
        _warningRepository = warningRepository;
        _banRepository = banRepository;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TicketDto>> GetTickets(TicketStatus? status = null, CancellationToken ct = default)
    {
        var tickets = await _ticketRepository.GetTickets(status, ct);
        return tickets.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TicketDto>> GetMyAssignedTickets(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var tickets = await _ticketRepository.GetModeratorTickets(userId, ct);
        return tickets.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TicketDto>> GetMyFiledTickets(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var tickets = await _ticketRepository.GetUserTickets(userId, ct);
        return tickets.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<TicketDto?> GetTicket(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.Get(ticketId, ct);
        return ticket != null ? MapToDto(ticket) : null;
    }

    /// <inheritdoc />
    public async Task<TicketDto> CreateTicket(CreateTicket createTicket, CancellationToken ct = default)
    {
        var currentUser = _identityProvider.Current.User;
        var targetUser = await _userRepository.GetUser(createTicket.TargetLogin);

        if (targetUser == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "User not found");
        }

        if (targetUser.UserId == currentUser.UserId)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest, "Cannot report yourself");
        }

        var ticket = new Ticket
        {
            TicketId = _guidFactory.Create(),
            UserId = currentUser.UserId,
            TargetId = targetUser.UserId,
            EntityId = createTicket.EntityId,
            EntityType = createTicket.EntityType ?? string.Empty,
            Status = TicketStatus.Open,
            CreatedUtc = _dateTimeProvider.Now,
            Description = createTicket.Description,
            Comment = createTicket.Comment
        };

        await _ticketRepository.Create(ticket, ct);

        // Re-fetch to get navigation properties
        var result = await _ticketRepository.Get(ticket.TicketId, ct);
        return MapToDto(result!);
    }

    /// <inheritdoc />
    public async Task<TicketDto> AssignToMe(Guid ticketId, CancellationToken ct = default)
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

        ticket.AssignedModeratorId = currentUser.UserId;
        ticket.Status = TicketStatus.InProgress;
        ticket.UpdatedUtc = _dateTimeProvider.Now;

        await _ticketRepository.Update(ticket, ct);

        var result = await _ticketRepository.Get(ticketId, ct);
        return MapToDto(result!);
    }

    /// <inheritdoc />
    public async Task<TicketDto> ResolveTicket(Guid ticketId, ResolveTicket resolveTicket, CancellationToken ct = default)
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

        // Issue warning if requested
        if (resolveTicket.IssueWarning && !string.IsNullOrEmpty(resolveTicket.WarningText))
        {
            var warning = new Warning
            {
                WarningId = _guidFactory.Create(),
                UserId = ticket.TargetId,
                ModeratorId = currentUser.UserId,
                EntityId = ticket.EntityId ?? ticket.TicketId,
                CreatedUtc = now,
                Text = resolveTicket.WarningText,
                Points = resolveTicket.WarningPoints,
                IsRemoved = false
            };
            await _warningRepository.Create(warning, ct);
            ticket.WarningId = warning.WarningId;
        }

        // Issue ban if requested
        if (resolveTicket.IssueBan && resolveTicket.BanDurationHours.HasValue)
        {
            var ban = new Ban
            {
                BanId = _guidFactory.Create(),
                UserId = ticket.TargetId,
                ModeratorId = currentUser.UserId,
                StartedUtc = now,
                EndedUtc = now.AddHours(resolveTicket.BanDurationHours.Value),
                Comment = resolveTicket.BanComment ?? "Banned via ticket resolution",
                AccessRestrictionPolicy = AccessPolicy.FullBan, // Default restriction
                IsVoluntary = false,
                IsRemoved = false
            };
            await _banRepository.Create(ban, ct);
            ticket.BanId = ban.BanId;
        }

        ticket.Status = resolveTicket.Status;
        ticket.AnswerAuthorId = currentUser.UserId;
        ticket.Answer = resolveTicket.Answer;
        ticket.ResolvedUtc = now;
        ticket.UpdatedUtc = now;

        await _ticketRepository.Update(ticket, ct);

        var result = await _ticketRepository.Get(ticketId, ct);
        return MapToDto(result!);
    }

    /// <inheritdoc />
    public async Task<Dictionary<TicketStatus, int>> GetTicketStats(CancellationToken ct = default)
    {
        return await _ticketRepository.GetTicketCounts(ct);
    }

    private static TicketDto MapToDto(Ticket ticket)
    {
        return new TicketDto
        {
            TicketId = ticket.TicketId,
            ReporterLogin = ticket.Author?.Login ?? "",
            TargetLogin = ticket.Target?.Login ?? "",
            EntityId = ticket.EntityId,
            EntityType = ticket.EntityType,
            Status = ticket.Status,
            CreatedUtc = ticket.CreatedUtc,
            Description = ticket.Description,
            Comment = ticket.Comment,
            AssignedModeratorLogin = ticket.AssignedModerator?.Login,
            ResolvedUtc = ticket.ResolvedUtc,
            Answer = ticket.Answer,
            HasWarning = ticket.WarningId.HasValue,
            HasBan = ticket.BanId.HasValue
        };
    }
}
