using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Tickets;
using DM.Web.API.Features.Moderation.Tickets;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using TicketComment = DM.Web.API.Features.Moderation.Tickets.TicketComment;

namespace DM.Web.API.Features.General.Tickets;

/// <inheritdoc />
internal class TicketIntakeApiService : ITicketIntakeApiService
{
    private readonly ITicketService _ticketService;
    private readonly TicketIntakeMapper _mapper;
    private readonly TicketMapper _ticketMapper;

    /// <inheritdoc />
    public TicketIntakeApiService(ITicketService ticketService, TicketIntakeMapper mapper, TicketMapper ticketMapper)
    {
        _ticketService = ticketService;
        _mapper = mapper;
        _ticketMapper = ticketMapper;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
            Description = ticket.Description,
            DescriptionHtml = new InfoBbText { Value = ticket.Description },
            CreatedUtc = ticket.CreatedUtc,
            ResolvedUtc = ticket.ResolvedUtc,
            ResolvedByUsername = ticket.ResolvedByUsername,
            // The comments of the conversation in the one shape the three pages of
            // a ticket read, the guest's included.
            Comments = ticket.Comments.Select(_ticketMapper.ToTicketComment).ToList(),
            CanComment = ticket.CanComment
        };
    }

    /// <inheritdoc />
    public async Task<Envelope<TicketComment>> CreateTrackedComment(string token, CreateTicketCommentRequest request)
    {
        var comment = await _ticketService.CreateTrackedComment(token, _ticketMapper.ToCreateTicketComment(request));
        return new Envelope<TicketComment>(_ticketMapper.ToTicketComment(comment));
    }
}
