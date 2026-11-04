using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Tickets;
using DM.Web.API.Features.Moderation.Tickets;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using TicketComment = DM.Web.API.Features.Moderation.Tickets.TicketComment;

namespace DM.Web.API.Features.General.Tickets;

/// <summary>
/// API service for public ticket intake (support/complaint forms)
/// </summary>
public class TicketIntakeApiService
{
    private readonly ITicketService _ticketService;
    private readonly TicketIntakeMapper _mapper;
    private readonly TicketMapper _ticketMapper;

    public TicketIntakeApiService(ITicketService ticketService, TicketIntakeMapper mapper, TicketMapper ticketMapper)
    {
        _ticketService = ticketService;
        _mapper = mapper;
        _ticketMapper = ticketMapper;
    }

    /// <summary>
    /// Create a new ticket from the public intake forms. Returns the ticket
    /// identifier, and the guest tracking token when the author is a guest
    /// (null for authenticated authors)
    /// </summary>
    public async Task<CreateTicketIntakeResponse> CreateTicket(CreateTicketIntakeRequest request)
    {
        var createTicketIntake = _mapper.ToCreateTicketIntake(request);
        var ticket = await _ticketService.CreateIntakeTicket(createTicketIntake);
        return new CreateTicketIntakeResponse
        {
            TicketId = ticket.TicketId,
            TrackingToken = ticket.TrackingToken
        };
    }

    /// <summary>
    /// Get a guest's ticket by its tracking token.
    /// Returns null for an empty or unknown token
    /// </summary>
    public async Task<TrackedTicket?> TrackTicket(string token)
    {
        var ticket = await _ticketService.GetTicketByTrackingToken(token);
        if (ticket == null)
        {
            return null;
        }

        return new TrackedTicket
        {
            Status = ticket.Status,
            Subtype = ticket.Subtype,
            // Intake stores the subject in Comment and the body in Description
            // (see TicketService.CreateIntakeTicket).
            Subject = ticket.Comment,
            Description = new InfoBbText { Value = ticket.Description },
            CreatedUtc = ticket.CreatedUtc,
            ResolvedUtc = ticket.ResolvedUtc,
            ResolvedByUsername = ticket.ResolvedByUsername,
            // The comments of the conversation in the one shape the three pages of
            // a ticket read, the guest's included.
            Comments = ticket.Comments.Select(_ticketMapper.ToTicketComment).ToList(),
            CanComment = ticket.CanComment
        };
    }

    /// <summary>
    /// Write in the conversation of a guest's ticket by its tracking token
    /// </summary>
    public async Task<Envelope<TicketComment>> CreateTrackedComment(string token, CreateTicketCommentRequest request)
    {
        var comment = await _ticketService.CreateTrackedComment(token, _ticketMapper.ToCreateTicketComment(request));
        return new Envelope<TicketComment>(_ticketMapper.ToTicketComment(comment));
    }
}
