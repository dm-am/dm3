using AutoMapper;
using DM.Services.Forum.Dto.Output;

namespace DM.Web.API.Dto.Boards;

/// <summary>
/// Mapping profile from Service DTO to API DTO for fora/boards
/// </summary>
internal class ForumProfile : Profile
{
    /// <inheritdoc />
    public ForumProfile()
    {
        // Legacy mapping (keep for backwards compatibility if needed)
        CreateMap<DM.Services.Forum.Dto.Output.Forum, Forum>()
            .ForMember(d => d.Id, s => s.MapFrom(f => f.Title));

        // New Board mapping
        CreateMap<DM.Services.Forum.Dto.Output.Forum, Board>()
            .ForMember(d => d.Id, s => s.MapFrom(f => f.Title));

        CreateMap<ForumLastComment, BoardLastComment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreateDate));
    }
}