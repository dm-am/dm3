using System.Linq;
using AutoMapper;
using DM.Services.Core.Dto;
using DM.Services.Forum.Dto.Output;

namespace DM.Services.Forum.Dto;

/// <summary>
/// Profile for board DTO and DAL mapping
/// </summary>
internal class BoardProfile : Profile
{
    /// <inheritdoc />
    public BoardProfile()
    {
        CreateMap<Output.Board, Output.Board>();

        CreateMap<DataAccess.BusinessObjects.Boards.Board, Output.Board>()
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
                            Login = b.LastCommentAuthor.Login,
                            Role = b.LastCommentAuthor.Role,
                            Status = b.LastCommentAuthor.Status
                        }
                        : null
                }
                : null));
    }
}