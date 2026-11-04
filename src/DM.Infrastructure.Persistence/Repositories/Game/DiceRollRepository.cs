using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Posts;
using Microsoft.EntityFrameworkCore;
using DbDiceRoll = DM.Infrastructure.Persistence.Entities.Game.Posts.DiceRoll;
using DbRollResult = DM.Infrastructure.Persistence.Entities.Game.Posts.RollResult;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Repository for dice rolls. Read-only: the rolls are written by
/// <see cref="PostRepository.Create"/> inside the post's transaction.
/// </summary>
internal class DiceRollRepository : IDiceRollRepository
{
    private readonly DmDbContext _dbContext;

    public DiceRollRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiceRoll>> GetByPostIdAsync(Guid postId, CancellationToken ct = default)
    {
        return await Read(_dbContext.DiceRolls
            .TagWith("DM.Game.DiceRollsByPost")
            .Where(d => d.PostId == postId), ct);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, IEnumerable<DiceRoll>>> GetByPostIdsAsync(IEnumerable<Guid> postIds)
    {
        var postIdList = postIds.ToList();
        if (postIdList.Count == 0)
            return new Dictionary<Guid, IEnumerable<DiceRoll>>();

        var rolls = await Read(_dbContext.DiceRolls
            .TagWith("DM.Game.DiceRollsByPosts")
            .Where(d => postIdList.Contains(d.PostId)));

        return rolls
            .GroupBy(r => r.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.AsEnumerable());
    }

    /// <summary>
    /// The rolls in the order they were thrown, each with the word of its room
    /// and its game on who sees the results: the domain decides on the roll
    /// alone (DiceRollVisibility), and a roll read without them hides itself from
    /// everybody but the leads and the author.
    /// </summary>
    private async Task<List<DiceRoll>> Read(IQueryable<DbDiceRoll> rolls, CancellationToken ct = default)
    {
        var rows = await rolls
            .Join(_dbContext.Posts, d => d.PostId, p => p.PostId, (d, p) => new
            {
                Roll = d,
                p.Room.ViewDiceResults,
                p.Room.Game.HideDiceResult
            })
            .OrderBy(r => r.Roll.CreatedUtc)
            .ThenBy(r => r.Roll.DiceRollId)
            .ToListAsync(ct);

        return rows
            .Select(r => MapToDomain(r.Roll, r.ViewDiceResults, r.HideDiceResult))
            .ToList();
    }

    /// <summary>
    /// The rolls of a post as rows, for the one write path — post creation.
    /// </summary>
    internal static IEnumerable<DbDiceRoll> MapToDb(IEnumerable<DiceRoll> rolls) => rolls
        .Select(roll => new DbDiceRoll
        {
            DiceRollId = roll.Id,
            PostId = roll.PostId,
            CreatedUtc = roll.CreatedUtc,
            IsHidden = roll.IsHidden,
            DiceCount = roll.DiceCount,
            EdgesCount = roll.EdgesCount,
            ExplosionCount = roll.ExplosionCount,
            Bonus = roll.Bonus,
            Comment = roll.Comment,
            Result = roll.Results.Select(r => new DbRollResult
            {
                Value = r.Value,
                IsCritical = r.IsCritical,
                IsExploded = r.IsExploded
            }).ToArray()
        });

    private static DiceRoll MapToDomain(DbDiceRoll db, bool roomViewDiceResults, bool gameHideDiceResult) => new()
    {
        Id = db.DiceRollId,
        PostId = db.PostId,
        CreatedUtc = db.CreatedUtc,
        IsHidden = db.IsHidden,
        RoomViewDiceResults = roomViewDiceResults,
        GameHideDiceResult = gameHideDiceResult,
        DiceCount = db.DiceCount,
        EdgesCount = db.EdgesCount,
        ExplosionCount = db.ExplosionCount,
        Bonus = db.Bonus,
        Comment = db.Comment,
        Results = db.Result.Select(r => new DiceRollResult
        {
            Value = r.Value,
            IsCritical = r.IsCritical,
            IsExploded = r.IsExploded
        })
    };
}
