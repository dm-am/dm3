using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.ProfileNotes;

/// <inheritdoc />
internal class ProfileNoteRepository : IProfileNoteRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public ProfileNoteRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<ProfileNote?> Get(Guid ownerId, Guid subjectUserId, CancellationToken ct = default)
    {
        return _dbContext.ProfileNotes
            .Include(n => n.SubjectUser)
            .FirstOrDefaultAsync(n => n.OwnerId == ownerId && n.SubjectUserId == subjectUserId, ct);
    }

    /// <inheritdoc />
    public Task<ProfileNote?> GetById(Guid noteId, CancellationToken ct = default)
    {
        return _dbContext.ProfileNotes
            .Include(n => n.SubjectUser)
            .FirstOrDefaultAsync(n => n.NoteId == noteId, ct);
    }

    /// <inheritdoc />
    public async Task<ProfileNote> Create(ProfileNote note, CancellationToken ct = default)
    {
        _dbContext.ProfileNotes.Add(note);
        await _dbContext.SaveChangesAsync(ct);
        return note;
    }

    /// <inheritdoc />
    public async Task<ProfileNote> Update(ProfileNote note, CancellationToken ct = default)
    {
        _dbContext.ProfileNotes.Update(note);
        await _dbContext.SaveChangesAsync(ct);
        return note;
    }

    /// <inheritdoc />
    public async Task Delete(Guid noteId, CancellationToken ct = default)
    {
        var note = await _dbContext.ProfileNotes.FindAsync(new object[] { noteId }, ct);
        if (note != null)
        {
            _dbContext.ProfileNotes.Remove(note);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
