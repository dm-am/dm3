using AutoMapper;
using DomainBoard = DM.Domain.Forum.Features.Boards.Board;
using DomainBoardLastComment = DM.Domain.Forum.Features.Boards.BoardLastComment;

namespace DM.Web.API.Features.Forum.Boards;

/// <summary>
/// Mapping profile from Service DTO to API DTO for boards
/// </summary>
internal class BoardMappingProfile : Profile
{
    /// <inheritdoc />
    public BoardMappingProfile()
    {
        CreateMap<DomainBoard, Board>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.Title));

        CreateMap<DomainBoardLastComment, BoardLastComment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc));
    }
}
