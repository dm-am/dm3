using System.Linq;
using AutoMapper;
using DM.Services.Core.Dto;
using DM.Services.Forum.Dto.Output;

namespace DM.Services.Forum.Dto;

/// <summary>
/// Profile for forum DTO and DAL mapping
/// </summary>
internal class ForumProfile : Profile
{
    /// <inheritdoc />
    public ForumProfile()
    {
        CreateMap<Output.Forum, Output.Forum>();

        CreateMap<DataAccess.BusinessObjects.Boards.Forum, Output.Forum>()
            .ForMember(d => d.Id, s => s.MapFrom(f => f.ForumId))
            .ForMember(d => d.ModeratorIds,
                s => s.MapFrom(f => f.Moderators.Select(m => m.UserId)))
            .ForMember(d => d.LastComment, s => s.MapFrom(f => f.LastCommentId.HasValue
                ? new ForumLastComment
                {
                    Id = f.LastCommentId.Value,
                    TopicId = f.LastCommentTopicId!.Value,
                    CreateDate = f.LastCommentDate!.Value,
                    Author = f.LastCommentAuthor != null
                        ? new GeneralUser
                        {
                            UserId = f.LastCommentAuthor.UserId,
                            Login = f.LastCommentAuthor.Login,
                            Role = f.LastCommentAuthor.Role,
                            Status = f.LastCommentAuthor.Status
                        }
                        : null
                }
                : null));
    }
}