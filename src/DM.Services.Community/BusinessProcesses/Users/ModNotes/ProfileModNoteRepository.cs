using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <inheritdoc />
internal class ProfileModNoteRepository : IProfileModNoteRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public ProfileModNoteRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProfileModNote>> GetNotes(Guid userId) =>
        await _dbContext.ProfileModNotes
            .Where(n => !n.IsRemoved && n.UserId == userId)
            .Include(n => n.User)
            .Include(n => n.Author)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<ProfileModNote?> GetNote(Guid noteId) =>
        _dbContext.ProfileModNotes
            .Include(n => n.User)
            .Include(n => n.Author)
            .FirstOrDefaultAsync(n => !n.IsRemoved && n.ProfileModNoteId == noteId);

    /// <inheritdoc />
    public async Task<ProfileModNote> Create(ProfileModNote note)
    {
        _dbContext.ProfileModNotes.Add(note);
        await _dbContext.SaveChangesAsync();
        return await GetNote(note.ProfileModNoteId) ?? note;
    }

    /// <inheritdoc />
    public async Task Update(ProfileModNote note)
    {
        _dbContext.ProfileModNotes.Update(note);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid noteId)
    {
        var note = await _dbContext.ProfileModNotes.FindAsync(noteId);
        if (note != null)
        {
            note.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }
}
