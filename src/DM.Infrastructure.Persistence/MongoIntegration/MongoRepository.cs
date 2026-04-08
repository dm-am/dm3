using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.MongoIntegration;

/// <summary>
/// Base Mongo entity repository
/// </summary>
public abstract class MongoRepository
{
    private readonly DmMongoClient _client;

    /// <inheritdoc />
    protected MongoRepository(DmMongoClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Create filter definition builder
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns>Filter definition builder instance</returns>
    protected static FilterDefinitionBuilder<TEntity> Filter<TEntity>() => new();

    /// <summary>
    /// Create update definition builder
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns>Update definition builder instance</returns>
    protected static UpdateDefinitionBuilder<TEntity> Update<TEntity>() => new();

    /// <summary>
    /// Create sort definition builder
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns>Sort definition builder instance</returns>
    protected static SortDefinitionBuilder<TEntity> Sort<TEntity>() => new();

    /// <summary>
    /// Create projection definition builder
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns>Projection definition builder instance</returns>
    protected static ProjectionDefinitionBuilder<TEntity> Project<TEntity>() =>
        new();

    /// <summary>
    /// Get collection
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns>MongoDB collection for the entity type</returns>
    protected IMongoCollection<TEntity> Collection<TEntity>() => _client.GetCollection<TEntity>();
}
