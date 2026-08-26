using DM.Domain.Moderation.Features.Tickets;
using Riok.Mapperly.Abstractions;

namespace DM.Web.API.Features.General.Tickets;

/// <summary>
/// Compile-time mapper for the public ticket intake
/// </summary>
[Mapper]
internal partial class TicketIntakeMapper
{
    /// <summary>
    /// Intake request to the domain create command. The honeypot field stays
    /// behind on purpose: the refusal check reads it from the request, and the
    /// domain command has no business carrying bot bait.
    /// </summary>
    [MapperIgnoreSource(nameof(CreateTicketIntakeRequest.Website))]
    public partial CreateTicketIntake ToCreateTicketIntake(CreateTicketIntakeRequest request);
}
