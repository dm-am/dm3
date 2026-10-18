using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.ProfileNotes;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class UserProfileNoteRepository : IUserProfileNoteRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public UserProfileNoteRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<UserProfileNote?> Get(Guid ownerId, Guid subjectUserId, CancellationToken ct = default)
    {
        return _dbContext.UserProfileNotes
            .Where(n => n.OwnerId == ownerId && n.SubjectUserId == subjectUserId)
            .ProjectToUserProfileNote()
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UserProfileNote?> Create(CreateUserProfileNoteEntity note, CancellationToken ct = default)
    {
        // One statement, because any read taken beforehand stops being true the
        // moment it returns: a double press of "Save" sent two requests that both
        // found nothing and both inserted, and the one that lost the unique index
        // over the pair answered its reader with a failure of the storage engine.
        //
        // On the conflict the write becomes an update: the text replaced and the
        // note dated as modified now, its identifier and creation date kept. The
        // identifier minted for the insert is thrown away, which is why the note
        // is read back by the pair and not by that identifier.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserProfileNotes" ("UserProfileNoteId", "OwnerId", "SubjectUserId", "Text", "CreatedUtc")
            VALUES ({note.Id}, {note.OwnerId}, {note.SubjectUserId}, {note.Text}, {note.CreatedUtc})
            ON CONFLICT ("OwnerId", "SubjectUserId") DO UPDATE SET
                "Text" = EXCLUDED."Text",
                "ModifiedUtc" = EXCLUDED."CreatedUtc"
            """, ct);

        return await Get(note.OwnerId, note.SubjectUserId, ct);
    }

    /// <inheritdoc />
    public Task Delete(Guid noteId, CancellationToken ct = default) =>
        // A set-based delete rather than a load-and-remove: two clears of one note
        // arriving together both loaded it, and the change tracker of the second
        // took its delete of no rows for a lost concurrency check and answered 500.
        _dbContext.UserProfileNotes
            .Where(n => n.UserProfileNoteId == noteId)
            .ExecuteDeleteAsync(ct);
}
