using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Moderation.Tickets;

public class TicketApiService
{
    private readonly ITicketService _ticketService;
    private readonly TicketMapper _mapper;

    public TicketApiService(ITicketService ticketService, TicketMapper mapper)
    {
        _ticketService = ticketService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<Ticket>> GetTickets(PagingQuery query, TicketStatus? status = null,
        TicketSubtype? subtype = null)
    {
        var (tickets, paging) = await _ticketService.GetTickets(query, status, subtype);
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.ToTicket), new PagingInfo(paging));
    }

    public async Task<ListEnvelope<Ticket>> GetMyFiledTickets(PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null)
    {
        var (tickets, paging) = await _ticketService.GetMyFiledTickets(query, status, subtype);
        return new ListEnvelope<Ticket>(tickets.Select(_mapper.ToTicket), new PagingInfo(paging));
    }

    public async Task<Envelope<TicketDetails>> GetTicket(Guid ticketId)
    {
        var ticket = await _ticketService.GetTicket(ticketId);
        return new Envelope<TicketDetails>(_mapper.ToTicketDetails(ticket));
    }

    public async Task<Envelope<Ticket>> CreateTicket(CreateTicketRequest request)
    {
        var createTicket = _mapper.ToCreateTicket(request);
        var ticket = await _ticketService.CreateTicket(createTicket);
        return new Envelope<Ticket>(_mapper.ToTicket(ticket));
    }

    public async Task<Envelope<TicketDetails>> CreateComment(Guid ticketId, AnswerTicketRequest request)
    {
        var ticket = await _ticketService.CreateComment(ticketId, _mapper.ToCreateTicketComment(request));
        return new Envelope<TicketDetails>(_mapper.ToTicketDetails(ticket));
    }

    public async Task<TicketStats> GetStats()
    {
        var stats = await _ticketService.GetTicketStats();
        return new TicketStats
        {
            WaitingForModeration = stats.GetValueOrDefault(TicketStatus.WaitingForModeration),
            WaitingForUser = stats.GetValueOrDefault(TicketStatus.WaitingForUser),
            Closed = stats.GetValueOrDefault(TicketStatus.Closed),
            Spam = stats.GetValueOrDefault(TicketStatus.Spam)
        };
    }
}
