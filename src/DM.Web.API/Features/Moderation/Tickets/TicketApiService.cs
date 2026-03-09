using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <inheritdoc />
internal class TicketApiService : ITicketApiService
{
    private readonly ITicketService _ticketService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public TicketApiService(ITicketService ticketService, IMapper mapper)
    {
        _ticketService = ticketService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Ticket>> GetTickets(TicketStatus? status = null)
    {
        var tickets = await _ticketService.GetTickets(status);
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.Map<Ticket>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Ticket>> GetMyAssignedTickets()
    {
        var tickets = await _ticketService.GetMyAssignedTickets();
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.Map<Ticket>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Ticket>> GetMyFiledTickets()
    {
        var tickets = await _ticketService.GetMyFiledTickets();
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.Map<Ticket>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Ticket>?> GetTicket(Guid ticketId)
    {
        var ticket = await _ticketService.GetTicket(ticketId);
        return ticket != null ? new Envelope<Ticket>(_mapper.Map<Ticket>(ticket)) : null;
    }

    /// <inheritdoc />
    public async Task<Envelope<Ticket>> CreateTicket(CreateTicketRequest request)
    {
        var createTicket = _mapper.Map<CreateTicket>(request);
        var ticket = await _ticketService.CreateTicket(createTicket);
        return new Envelope<Ticket>(_mapper.Map<Ticket>(ticket));
    }

    /// <inheritdoc />
    public async Task<Envelope<Ticket>> AssignToMe(Guid ticketId)
    {
        var ticket = await _ticketService.AssignToMe(ticketId);
        return new Envelope<Ticket>(_mapper.Map<Ticket>(ticket));
    }

    /// <inheritdoc />
    public async Task<Envelope<Ticket>> ResolveTicket(Guid ticketId, ResolveTicketRequest request)
    {
        var resolveTicket = _mapper.Map<ResolveTicket>(request);
        var ticket = await _ticketService.ResolveTicket(ticketId, resolveTicket);
        return new Envelope<Ticket>(_mapper.Map<Ticket>(ticket));
    }

    /// <inheritdoc />
    public async Task<TicketStats> GetStats()
    {
        var stats = await _ticketService.GetTicketStats();
        return new TicketStats
        {
            Open = stats.TryGetValue(TicketStatus.Open, out var open) ? open : 0,
            InProgress = stats.TryGetValue(TicketStatus.InProgress, out var inProgress) ? inProgress : 0,
            Resolved = stats.TryGetValue(TicketStatus.Resolved, out var resolved) ? resolved : 0,
            Rejected = stats.TryGetValue(TicketStatus.Rejected, out var rejected) ? rejected : 0
        };
    }
}
