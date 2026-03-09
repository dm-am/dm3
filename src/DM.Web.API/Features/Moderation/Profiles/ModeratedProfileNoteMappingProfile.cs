using AutoMapper;
using DomainNote = DM.Domain.Moderation.Features.ProfileNotes.ModeratedProfileNote;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// AutoMapper profile for moderator notes
/// </summary>
internal class ModeratedProfileNoteMappingProfile : Profile
{
    /// <inheritdoc />
    public ModeratedProfileNoteMappingProfile()
    {
        CreateMap<DomainNote, ModeratedProfileNote>()
            .ForMember(d => d.Id, s => s.MapFrom(n => n.Id))
            .ForMember(d => d.User, s => s.MapFrom(n => n.User))
            .ForMember(d => d.Author, s => s.MapFrom(n => n.Author))
            .ForMember(d => d.Text, s => s.MapFrom(n => n.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(n => n.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, s => s.MapFrom(n => n.UpdatedUtc));
    }
}
