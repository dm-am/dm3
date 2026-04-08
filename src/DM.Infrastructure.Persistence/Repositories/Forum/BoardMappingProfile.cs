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
            .ForMember(d => d.Moderators,
                s => s.MapFrom(b => b.Moderators.Select(m => new GeneralUser
                {
                    UserId = m.User.UserId,
                    Username = m.User.Username,
                    Role = m.User.Role,
                    Status = m.User.Status,
                    QuantityRating = m.User.QuantityRating
                })))
            .ForMember(d => d.UnreadTopicsCount, opt => opt.Ignore())
            .ForMember(d => d.UnreadCommentsCount, opt => opt.Ignore())
            .ForMember(d => d.LastComment, s => s.MapFrom(b => b.LastCommentId.HasValue
                ? new BoardLastComment
                {
                    Id = b.LastCommentId.Value,
                    TopicId = b.LastCommentTopicId!.Value,
                    TopicTitle = b.LastCommentTopicTitle ?? string.Empty,
                    TopicNumber = b.LastCommentTopicNumber ?? 0,
                    CreatedUtc = b.LastCommentUtc!.Value,
                    Author = b.LastCommentAuthor != null
                        ? new GeneralUser
                        {
                            UserId = b.LastCommentAuthor.UserId,
                            Username = b.LastCommentAuthor.Username,
                            Role = b.LastCommentAuthor.Role,
                            Status = b.LastCommentAuthor.Status,
                            QuantityRating = b.LastCommentAuthor.QuantityRating
                        }
                        : null
                }
                : null))
            .ForMember(d => d.LastTopic, s => s.MapFrom(b => b.LastTopicId.HasValue
                ? new BoardLastTopic
                {
                    Id = b.LastTopicId.Value,
                    TopicNumber = b.LastTopicNumber ?? 0,
                    Title = b.LastTopicTitle ?? string.Empty,
                    CreatedUtc = b.LastTopicCreatedUtc!.Value,
                    Author = b.LastTopicAuthor != null
                        ? new GeneralUser
                        {
                            UserId = b.LastTopicAuthor.UserId,
                            Username = b.LastTopicAuthor.Username,
                            Role = b.LastTopicAuthor.Role,
                            Status = b.LastTopicAuthor.Status,
                            QuantityRating = b.LastTopicAuthor.QuantityRating
                        }
                        : null
                }
                : null));
    }
}
