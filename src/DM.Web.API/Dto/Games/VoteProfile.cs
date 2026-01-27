using AutoMapper;
using DM.Services.Gaming.Dto.Input;
using DtoVote = DM.Services.Gaming.Dto.Output.Vote;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// Mapping profile for vote models
/// </summary>
internal class VoteProfile : Profile
{
    /// <inheritdoc />
    public VoteProfile()
    {
        CreateMap<DtoVote, Vote>();
        CreateMap<Vote, CreateVote>();
    }
}
