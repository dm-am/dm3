using AutoMapper;
using DM.Services.Forum.Dto.Output;

namespace DM.Web.API.Dto.Boards;

/// <summary>
/// Mapping profile from Service DTO to API DTO for boards
/// </summary>
internal class BoardApiProfile : Profile
{
    /// <inheritdoc />
    public BoardApiProfile()
    {
        // Legacy mapping (keep for backwards compatibility if needed)
        CreateMap<DM.Services.Forum.Dto.Output.Board, Forum>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.Title));

        // Board mapping
        CreateMap<DM.Services.Forum.Dto.Output.Board, Board>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.Title));

        CreateMap<DM.Services.Forum.Dto.Output.BoardLastComment, Boards.BoardLastComment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc));
    }
}