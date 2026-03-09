using AutoMapper;
using DM.Domain.Core.Notepads;

namespace DM.Web.API.Features.Personal.Notepads;

/// <inheritdoc />
internal class NotepadMappingProfile : Profile
{
    /// <inheritdoc />
    public NotepadMappingProfile()
    {
        CreateMap<NotepadEntry, NotepadEntryResponse>();
        CreateMap<NotepadCategory, NotepadCategoryResponse>();
    }
}
