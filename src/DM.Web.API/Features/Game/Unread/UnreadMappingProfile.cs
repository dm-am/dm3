using AutoMapper;
using DomainFirstUnreadPostResult = DM.Domain.Game.Features.Unread.FirstUnreadPostResult;
using DomainFirstUnreadCommentResult = DM.Domain.Game.Features.Unread.FirstUnreadCommentResult;

namespace DM.Web.API.Features.Game.Unread;

/// <inheritdoc />
internal class UnreadMappingProfile : Profile
{
    /// <inheritdoc />
    public UnreadMappingProfile()
    {
        CreateMap<DomainFirstUnreadPostResult, FirstUnreadPostResult>();
        CreateMap<DomainFirstUnreadCommentResult, FirstUnreadCommentResult>();
    }
}
