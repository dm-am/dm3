using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Dto;

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
    public async Task<ListEnvelope<Ticket>> GetTickets(PagingQuery query, TicketStatus? status = null, TicketSubtype? subtype = null)
    {
        var (tickets, paging) = await _ticketService.GetTickets(query, status, subtype);
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.Map<Ticket>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Ticket>> GetMyAssignedTickets(PagingQuery query)
    {
        var (tickets, paging) = await _ticketService.GetMyAssignedTickets(query);
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.Map<Ticket>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Ticket>> GetMyFiledTickets(PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null)
    {
        var (tickets, paging) = await _ticketService.GetMyFiledTickets(query, status, subtype);
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.Map<Ticket>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<TicketDetails>?> GetTicket(Guid ticketId)
    {
        var ticket = await _ticketService.GetTicket(ticketId);
        return ticket != null ? new Envelope<TicketDetails>(_mapper.Map<TicketDetails>(ticket)) : null;
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
            WaitingForModeration = stats.TryGetValue(TicketStatus.WaitingForModeration, out var waitingForModeration)
                ? waitingForModeration
                : 0,
            WaitingForUser = stats.TryGetValue(TicketStatus.WaitingForUser, out var waitingForUser)
                ? waitingForUser
                : 0,
            Closed = stats.TryGetValue(TicketStatus.Closed, out var closed) ? closed : 0,
            Spam = stats.TryGetValue(TicketStatus.Spam, out var spam) ? spam : 0
        };
    }
}
