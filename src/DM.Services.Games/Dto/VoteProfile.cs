using AutoMapper;
using DalVote = DM.Services.DataAccess.BusinessObjects.Games.Rating.Vote;
using DtoVote = DM.Services.Gaming.Dto.Output.Vote;

namespace DM.Services.Gaming.Dto;

/// <summary>
/// Mapping profile for vote models
/// </summary>
internal class VoteProfile : Profile
{
    /// <inheritdoc />
    public VoteProfile()
    {
        CreateMap<DalVote, DtoVote>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.VoteId))
            .ForMember(d => d.Author, o => o.MapFrom(s => s.VotedUser))
            .ForMember(d => d.CreatedUtc, o => o.MapFrom(s => s.CreateDate));
    }
}
