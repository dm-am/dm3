using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Driver;
using DbDiceRoll = DM.Infrastructure.Persistence.Entities.Game.Posts.DiceRoll;

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
