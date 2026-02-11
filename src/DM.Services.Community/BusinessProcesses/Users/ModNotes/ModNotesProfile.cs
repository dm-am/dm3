using AutoMapper;
using DM.Services.Core.Dto;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <summary>
/// AutoMapper profile for moderator notes
/// </summary>
internal class ModNotesProfile : Profile
{
    /// <inheritdoc />
    public ModNotesProfile()
    {
        CreateMap<ProfileModNote, ProfileModNoteDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.ProfileModNoteId))
            .ForMember(d => d.User, o => o.MapFrom(s => s.User))
            .ForMember(d => d.Author, o => o.MapFrom(s => s.Author))
            .ForMember(d => d.Text, o => o.MapFrom(s => s.Text))
            .ForMember(d => d.CreatedAtUtc, o => o.MapFrom(s => s.CreatedAtUtc))
            .ForMember(d => d.ModifiedAtUtc, o => o.MapFrom(s => s.ModifiedAtUtc));

        CreateMap<User, GeneralUser>();
    }
}
