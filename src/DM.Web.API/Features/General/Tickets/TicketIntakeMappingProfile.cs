using AutoMapper;
using DM.Domain.Moderation.Features.Tickets;

namespace DM.Web.API.Features.General.Tickets;

/// <inheritdoc />
internal class TicketIntakeMappingProfile : Profile
{
    /// <inheritdoc />
    public TicketIntakeMappingProfile()
    {
        CreateMap<CreateTicketIntakeRequest, CreateTicketIntake>();
    }
}
