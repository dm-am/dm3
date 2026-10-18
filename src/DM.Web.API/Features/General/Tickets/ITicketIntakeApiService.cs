using System.Threading.Tasks;
using DM.Web.API.Features.Moderation.Tickets;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.General.Tickets;

/// <summary>
/// API service for public ticket intake (support/complaint forms)
/// </summary>
public interface ITicketIntakeApiService
{
    /// <summary>
    /// Create a new ticket from the public intake forms. Returns the ticket
    /// identifier, and the guest tracking token when the author is a guest
    /// (null for authenticated authors)
    /// </summary>
    Task<CreateTicketIntakeResponse> CreateTicket(CreateTicketIntakeRequest request);

    /// <summary>
    /// Get a guest's ticket by its tracking token.
    /// Returns null for an empty or unknown token
    /// </summary>
    Task<TrackedTicket?> TrackTicket(string token);

    /// <summary>
    /// Write in the conversation of a guest's ticket by its tracking token
    /// </summary>
    Task<Envelope<TicketComment>> CreateTrackedComment(string token, CreateTicketCommentRequest request);
}
