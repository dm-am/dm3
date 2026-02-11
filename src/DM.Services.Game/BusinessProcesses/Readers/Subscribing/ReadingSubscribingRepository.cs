using System;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Readers.Subscribing;

/// <inheritdoc />
internal class ReadingSubscribingRepository : IReadingSubscribingRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public ReadingSubscribingRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<bool> HasSubscription(Guid userId, Guid gameId) =>
        _dbContext.Readers.AnyAsync(r => r.UserId == userId && r.GameId == gameId);

    /// <inheritdoc />
    public Task Add(Reader reader)
    {
        _dbContext.Readers.Add(reader);
        return _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid userId, Guid gameId)
    {
        var reader = await _dbContext.Readers.FirstAsync(r => r.GameId == gameId && r.UserId == userId);
        _dbContext.Readers.Remove(reader);
        await _dbContext.SaveChangesAsync();
    }
}