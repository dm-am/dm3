using System.Linq;
using AutoMapper;
using DbMessage = DM.Services.DataAccess.BusinessObjects.Common.ChatMessage;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <inheritdoc />
internal class ChatMessageProfile : Profile
{
    /// <inheritdoc />
    public ChatMessageProfile()
    {
        CreateMap<DbMessage, ChatMessage>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ChatMessageId))
            .ForMember(dest => dest.CreateDate, opt => opt.MapFrom(src => src.CreateDate))
            .ForMember(dest => dest.LastUpdateDate, opt => opt.MapFrom(src => src.LastUpdateDate))
            .ForMember(dest => dest.IsRemoved, opt => opt.MapFrom(src => src.IsRemoved))
            .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.Author))
            .ForMember(dest => dest.Likes, opt => opt.MapFrom(src => src.ChatMessageLikes.Select(l => l.User)));
    }
}