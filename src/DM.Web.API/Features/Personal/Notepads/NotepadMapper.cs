using DM.Domain.Core.Notepads;
using Riok.Mapperly.Abstractions;

namespace DM.Web.API.Features.Personal.Notepads;

/// <summary>
/// Compile-time mapper for notepad responses. An instance class on purpose:
/// the blanket assembly scan registers it like any other collaborator, and
/// mappers that need services (identity, clock) take them through the
/// constructor instead of AutoMapper's resolver indirection.
/// </summary>
[Mapper]
internal partial class NotepadMapper
{
    /// <summary>
    /// Notepad entry to its response DTO
    /// </summary>
    public partial NotepadEntryResponse ToResponse(NotepadEntry entry);

    /// <summary>
    /// Create request to the domain DTO.
    /// </summary>
    /// <remarks>
    /// Which notepad the entry lands in is decided by the route and by the
    /// service behind it, not by the body: the three placement members stay at
    /// their defaults here exactly as the five hand-written initializers left
    /// them.
    /// </remarks>
    [MapperIgnoreTarget(nameof(CreateNotepadEntry.NotepadType))]
    [MapperIgnoreTarget(nameof(CreateNotepadEntry.ContainerId))]
    [MapperIgnoreTarget(nameof(CreateNotepadEntry.OwnerId))]
    public partial CreateNotepadEntry ToCreateEntry(CreateNotepadEntryRequest request);

    /// <summary>
    /// Update request to the domain DTO: the same three members, PATCH-nullable
    /// on both sides.
    /// </summary>
    public partial UpdateNotepadEntry ToUpdateEntry(UpdateNotepadEntryRequest request);
}
