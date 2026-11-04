using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Service for moderator notes about users
/// </summary>
public interface IModeratedProfileNoteService
{
    Task<IEnumerable<ModeratedProfileNote>> GetNotes(string username);

    Task<ModeratedProfileNote> GetNote(Guid noteId);

    Task<ModeratedProfileNote> Create(CreateModeratedProfileNote createNote);

    Task<ModeratedProfileNote> Update(UpdateModeratedProfileNote updateNote);

    Task Delete(Guid noteId);
}
