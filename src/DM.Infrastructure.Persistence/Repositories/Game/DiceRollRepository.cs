using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <inheritdoc />
    public DiceRollRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiceRoll>> GetByPostIdAsync(Guid postId)
    {
        var rolls = await _dbContext.DiceRolls
            .TagWith("DM.Game.DiceRollsByPost")
            .Where(d => d.PostId == postId)
            .OrderBy(d => d.CreatedUtc)
            .ThenBy(d => d.DiceRollId)
            .ToListAsync();

        return rolls.Select(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, IEnumerable<DiceRoll>>> GetByPostIdsAsync(IEnumerable<Guid> postIds)
    {
        var postIdList = postIds.ToList();
        if (postIdList.Count == 0)
            return new Dictionary<Guid, IEnumerable<DiceRoll>>();

        var rolls = await _dbContext.DiceRolls
            .TagWith("DM.Game.DiceRollsByPosts")
            .Where(d => postIdList.Contains(d.PostId))
            .OrderBy(d => d.CreatedUtc)
            .ThenBy(d => d.DiceRollId)
            .ToListAsync();

        return rolls
            .GroupBy(r => r.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(MapToDomain).AsEnumerable());
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
            IsAdditional = roll.IsAdditional,
            IsHidden = roll.IsHidden,
            IsFair = roll.IsFair,
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

    private static DiceRoll MapToDomain(DbDiceRoll db) => new()
    {
        Id = db.DiceRollId,
        PostId = db.PostId,
        CreatedUtc = db.CreatedUtc,
        IsAdditional = db.IsAdditional,
        IsHidden = db.IsHidden,
        IsFair = db.IsFair,
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
