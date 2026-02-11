using AutoMapper;
using DM.Services.Community.BusinessProcesses.Blacklist;

namespace DM.Web.API.Dto.Blacklist;

/// <inheritdoc />
internal class BlacklistProfile : Profile
{
    /// <inheritdoc />
    public BlacklistProfile()
    {
        CreateMap<BlacklistEntryDto, BlacklistEntry>();
    }
}
