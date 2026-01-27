using System;
using System.Linq;
using AutoMapper;
using DbMessage = DM.Services.DataAccess.BusinessObjects.Messaging.Message;
using DbMessageEdit = DM.Services.DataAccess.BusinessObjects.Messaging.MessageEdit;

namespace DM.Services.Community.BusinessProcesses.Chat.Reading;

/// <inheritdoc />
internal class ChatMessageProfile : Profile
{
    /// <inheritdoc />
    public ChatMessageProfile()
    {
        CreateMap<DbMessage, ChatMessage>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.MessageId))
            .ForMember(dest => dest.CreatedUtc, opt => opt.MapFrom(src => src.CreatedUtc))
            .ForMember(dest => dest.ModifiedUtc, opt => opt.MapFrom(src =>
                src.Edits != null && src.Edits.Any()
                    ? src.Edits.Max(e => e.EditedAtUtc)
                    : (DateTimeOffset?)null))
            .ForMember(dest => dest.IsRemoved, opt => opt.MapFrom(src => src.IsRemoved))
            .ForMember(dest => dest.DeletedBy, opt => opt.MapFrom(src => src.DeletedBy))
            .ForMember(dest => dest.DeletedAtUtc, opt => opt.MapFrom(src => src.DeletedAtUtc))
            .ForMember(dest => dest.Edits, opt => opt.MapFrom(src => src.Edits))
            .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.Author))
            .ForMember(dest => dest.Likes, opt => opt.MapFrom(src => src.Likes.Select(l => l.User)));

        CreateMap<DbMessageEdit, ChatMessageEdit>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.MessageEditId))
            .ForMember(dest => dest.EditedAtUtc, opt => opt.MapFrom(src => src.EditedAtUtc))
            .ForMember(dest => dest.Editor, opt => opt.MapFrom(src => src.Editor));
    }
}
