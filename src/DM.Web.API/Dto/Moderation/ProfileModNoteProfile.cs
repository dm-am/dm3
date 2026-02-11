using AutoMapper;
using DM.Services.Community.BusinessProcesses.Users.ModNotes;

namespace DM.Web.API.Dto.Moderation;

/// <summary>
/// AutoMapper profile for moderator notes
/// </summary>
internal class ProfileModNoteProfile : Profile
{
    /// <inheritdoc />
    public ProfileModNoteProfile()
    {
        CreateMap<ProfileModNoteDto, ProfileModNote>()
            .ForMember(d => d.Id, s => s.MapFrom(n => n.Id))
            .ForMember(d => d.User, s => s.MapFrom(n => n.User))
            .ForMember(d => d.Author, s => s.MapFrom(n => n.Author))
            .ForMember(d => d.Text, s => s.MapFrom(n => n.Text))
            .ForMember(d => d.CreatedAtUtc, s => s.MapFrom(n => n.CreatedAtUtc))
            .ForMember(d => d.ModifiedAtUtc, s => s.MapFrom(n => n.ModifiedAtUtc));
    }
}
