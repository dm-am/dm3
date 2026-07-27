using System.Threading.Tasks;

namespace DM.Web.API.Features.General.Tickets;

/// <summary>
/// API service for public ticket intake (support/complaint forms)
/// </summary>
public interface ITicketIntakeApiService
{
    /// <summary>
    /// Create a new ticket from the public intake forms. Returns the guest
    /// tracking token when the author is a guest (null for authenticated authors)
    /// </summary>
    Task<CreateTicketIntakeResponse> CreateTicket(CreateTicketIntakeRequest request);

    /// <summary>
    /// Get a guest's ticket (status + thread) by its tracking token.
    /// Returns null for an empty or unknown token
    /// </summary>
    Task<TrackedTicket?> TrackTicket(string token);
}
