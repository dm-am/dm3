using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Moderation.Features.ProfileNotes;
using Microsoft.EntityFrameworkCore;
using DbNote = DM.Infrastructure.Persistence.Entities.Account.ModeratedProfileNote;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class ModeratedProfileNoteRepository : IModeratedProfileNoteRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ModeratedProfileNoteRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModeratedProfileNote>> GetNotes(Guid userId) =>
        await _dbContext.ModeratedProfileNotes
            .Where(n => !n.IsRemoved && n.UserId == userId)
            .Include(n => n.User)
            .Include(n => n.Author)
            .OrderByDescending(n => n.CreatedUtc)
            .ProjectTo<ModeratedProfileNote>(_mapper.ConfigurationProvider)
            .ToArrayAsync();

    /// <inheritdoc />
    public Task<ModeratedProfileNote?> GetNote(Guid noteId) =>
        _dbContext.ModeratedProfileNotes
            .Where(n => !n.IsRemoved && n.ModeratedProfileNoteId == noteId)
            .Include(n => n.User)
            .Include(n => n.Author)
            .ProjectTo<ModeratedProfileNote>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<ModeratedProfileNote> Create(CreateModeratedProfileNoteEntity entity)
    {
        var note = new DbNote
        {
            ModeratedProfileNoteId = entity.Id,
            UserId = entity.UserId,
            AuthorId = entity.AuthorId,
            Text = entity.Text,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.ModeratedProfileNotes.Add(note);
        await _dbContext.SaveChangesAsync();

        return (await GetNote(note.ModeratedProfileNoteId))!;
    }

    /// <inheritdoc />
    public async Task Update(UpdateModeratedProfileNoteEntity entity)
    {
        var note = await _dbContext.ModeratedProfileNotes.FindAsync(entity.Id);
        if (note == null)
        {
            throw new InvalidOperationException($"ModeratedProfileNote {entity.Id} not found");
        }

        if (entity.Text != null)
        {
            note.Text = entity.Text;
        }
        note.ModifiedUtc = entity.ModifiedUtc;

        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid noteId)
    {
        var note = await _dbContext.ModeratedProfileNotes.FindAsync(noteId);
        if (note != null)
        {
            note.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }
}
