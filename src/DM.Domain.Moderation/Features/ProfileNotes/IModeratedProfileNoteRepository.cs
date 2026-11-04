using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Storage for moderator notes about users
/// </summary>
public interface IModeratedProfileNoteRepository
{
    Task<IEnumerable<ModeratedProfileNote>> GetNotes(Guid userId);

    Task<ModeratedProfileNote?> GetNote(Guid noteId);

    Task<ModeratedProfileNote> Create(CreateModeratedProfileNoteEntity entity);

    Task Update(UpdateModeratedProfileNoteEntity entity);

    /// <summary>
    /// Soft-delete a moderator note
    /// </summary>
    Task Delete(Guid noteId);
}
