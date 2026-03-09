using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Personal.Features.ProfileNotes;
using Microsoft.EntityFrameworkCore;
using DbUserProfileNote = DM.Infrastructure.Persistence.Entities.Account.UserProfileNote;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class UserProfileNoteRepository : IUserProfileNoteRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserProfileNoteRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<UserProfileNote?> Get(Guid ownerId, Guid subjectUserId, CancellationToken ct = default)
    {
        return _dbContext.UserProfileNotes
            .Where(n => n.OwnerId == ownerId && n.SubjectUserId == subjectUserId)
            .Include(n => n.SubjectUser)
            .ProjectTo<UserProfileNote>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public Task<UserProfileNote?> GetById(Guid noteId, CancellationToken ct = default)
    {
        return _dbContext.UserProfileNotes
            .Where(n => n.UserProfileNoteId == noteId)
            .Include(n => n.SubjectUser)
            .ProjectTo<UserProfileNote>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UserProfileNote> Create(CreateUserProfileNoteEntity note, CancellationToken ct = default)
    {
        var entity = new DbUserProfileNote
        {
            UserProfileNoteId = note.NoteId,
            OwnerId = note.OwnerId,
            SubjectUserId = note.SubjectUserId,
            Text = note.Text,
            CreatedUtc = note.CreatedUtc
        };

        _dbContext.UserProfileNotes.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        return (await GetById(entity.UserProfileNoteId, ct))!;
    }

    /// <inheritdoc />
    public async Task<UserProfileNote> Update(UpdateUserProfileNoteEntity note, CancellationToken ct = default)
    {
        var entity = await _dbContext.UserProfileNotes.FindAsync(new object[] { note.NoteId }, ct);
        if (entity == null)
        {
            throw new InvalidOperationException($"UserProfileNote {note.NoteId} not found");
        }

        if (note.Text != null)
        {
            entity.Text = note.Text;
        }
        entity.UpdatedUtc = note.UpdatedUtc;

        await _dbContext.SaveChangesAsync(ct);

        return (await GetById(entity.UserProfileNoteId, ct))!;
    }

    /// <inheritdoc />
    public async Task Delete(Guid noteId, CancellationToken ct = default)
    {
        var note = await _dbContext.UserProfileNotes.FindAsync(new object[] { noteId }, ct);
        if (note != null)
        {
            _dbContext.UserProfileNotes.Remove(note);
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
