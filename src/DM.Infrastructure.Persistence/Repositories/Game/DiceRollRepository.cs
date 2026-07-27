using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Driver;
using DbDiceRoll = DM.Infrastructure.Persistence.Entities.Game.Posts.DiceRoll;
using DbRollResult = DM.Infrastructure.Persistence.Entities.Game.Posts.RollResult;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Repository for dice rolls (MongoDB)
/// </summary>
internal class DiceRollRepository : MongoCollectionRepository<DbDiceRoll>, IDiceRollRepository
{
    /// <inheritdoc />
    public DiceRollRepository(DmMongoClient client) : base(client)
    {
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DiceRoll>> GetByPostIdAsync(Guid postId)
    {
        var rolls = await Collection
            .Find(Filter.Eq(d => d.PostId, postId))
            .SortBy(d => d.CreatedUtc)
            .ToListAsync();

        return rolls.Select(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, IEnumerable<DiceRoll>>> GetByPostIdsAsync(IEnumerable<Guid> postIds)
    {
        var postIdList = postIds.ToList();
        if (postIdList.Count == 0)
            return new Dictionary<Guid, IEnumerable<DiceRoll>>();

        var rolls = await Collection
            .Find(Filter.In(d => d.PostId, postIdList))
            .SortBy(d => d.CreatedUtc)
            .ToListAsync();

        return rolls
            .GroupBy(r => r.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(MapToDomain).AsEnumerable());
    }

    /// <inheritdoc />
    public Task CreateAsync(IEnumerable<DiceRoll> rolls)
    {
        var documents = rolls.Select(MapToDb).ToList();
        if (documents.Count == 0)
            return Task.CompletedTask;

        return Collection.InsertManyAsync(documents);
    }

    private static DbDiceRoll MapToDb(DiceRoll roll) => new()
    {
        Id = roll.Id,
        PostId = roll.PostId,
        CreatedUtc = roll.CreatedUtc.UtcDateTime,
        IsAdditional = roll.IsAdditional,
        IsHidden = roll.IsHidden,
        IsFair = roll.IsFair,
        DiceCount = roll.DiceCount,
        EdgesCount = roll.EdgesCount,
        ExplosionCount = roll.ExplosionCount,
        Bonus = roll.Bonus,
        comment = roll.Comment,
        Result = roll.Results.Select(r => new DbRollResult
        {
            Value = r.Value,
            IsCritical = r.IsCritical,
            IsExploded = r.IsExploded
        }).ToArray()
    };

    private static DiceRoll MapToDomain(DbDiceRoll db) => new()
    {
        Id = db.Id,
        PostId = db.PostId,
        CreatedUtc = new DateTimeOffset(db.CreatedUtc, TimeSpan.Zero),
        IsAdditional = db.IsAdditional,
        IsHidden = db.IsHidden,
        IsFair = db.IsFair,
        DiceCount = db.DiceCount,
        EdgesCount = db.EdgesCount,
        ExplosionCount = db.ExplosionCount,
        Bonus = db.Bonus,
        Comment = db.comment ?? string.Empty,
        Results = db.Result?.Select(r => new DiceRollResult
        {
            Value = r.Value,
            IsCritical = r.IsCritical,
            IsExploded = r.IsExploded
        }) ?? []
    };
}
