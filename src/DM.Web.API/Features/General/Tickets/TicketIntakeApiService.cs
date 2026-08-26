using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Tickets;

namespace DM.Web.API.Features.General.Tickets;

/// <inheritdoc />
internal class TicketIntakeApiService : ITicketIntakeApiService
{
    private readonly ITicketService _ticketService;
    private readonly TicketIntakeMapper _mapper;

    /// <inheritdoc />
    public TicketIntakeApiService(ITicketService ticketService, TicketIntakeMapper mapper)
    {
        _ticketService = ticketService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<CreateTicketIntakeResponse> CreateTicket(CreateTicketIntakeRequest request)
    {
        var createTicketIntake = _mapper.ToCreateTicketIntake(request);
        var ticket = await _ticketService.CreateIntakeTicket(createTicketIntake);
        return new CreateTicketIntakeResponse { TrackingToken = ticket.TrackingToken };
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
            CreatedUtc = ticket.CreatedUtc,
            ResolvedUtc = ticket.ResolvedUtc,
            Answer = ticket.Answer,
            Responses = ticket.Responses.Select(r => new TrackedTicketResponse
            {
                Text = r.Text,
                CreatedUtc = r.CreatedUtc,
                IsFromModerator = r.IsFromModerator
            })
        };
    }
}
