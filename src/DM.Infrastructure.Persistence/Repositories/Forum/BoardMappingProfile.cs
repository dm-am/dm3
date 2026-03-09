using System.Linq;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Boards;
using BoardEntity = DM.Infrastructure.Persistence.Entities.Forum.Board;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// Profile for board DTO and DAL mapping
/// </summary>
internal class BoardMappingProfile : Profile
{
    /// <inheritdoc />
    public BoardMappingProfile()
    {
        CreateMap<Board, Board>();

        CreateMap<BoardEntity, Board>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.BoardId))
            .ForMember(d => d.ModeratorIds,
                s => s.MapFrom(b => b.Moderators.Select(m => m.UserId)))
            .ForMember(d => d.LastComment, s => s.MapFrom(b => b.LastCommentId.HasValue
                ? new BoardLastComment
                {
                    Id = b.LastCommentId.Value,
                    TopicId = b.LastCommentTopicId!.Value,
                    CreatedUtc = b.LastCommentUtc!.Value,
                    Author = b.LastCommentAuthor != null
                        ? new GeneralUser
                        {
                            UserId = b.LastCommentAuthor.UserId,
                            Username = b.LastCommentAuthor.Username,
                            Role = b.LastCommentAuthor.Role,
                            Status = b.LastCommentAuthor.Status
                        }
                        : null
                }
                : null));
    }
}
