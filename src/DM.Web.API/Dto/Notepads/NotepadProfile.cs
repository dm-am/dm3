using AutoMapper;
using DM.Services.Game.BusinessProcesses.Notepads;

namespace DM.Web.API.Dto.Notepads;

/// <inheritdoc />
internal class NotepadProfile : Profile
{
    /// <inheritdoc />
    public NotepadProfile()
    {
        CreateMap<NotepadEntryDto, NotepadEntry>();
        CreateMap<NotepadCategoryDto, NotepadCategory>();
    }
}
