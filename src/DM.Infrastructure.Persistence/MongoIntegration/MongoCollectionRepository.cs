using MongoDB.Driver;

namespace DM.Infrastructure.Persistence.MongoIntegration;

/// <summary>
/// Repository for Mongo collection
/// </summary>
/// <typeparam name="TEntity">Entity type</typeparam>
public abstract class MongoCollectionRepository<TEntity> : MongoRepository
    where TEntity : class
{
    /// <inheritdoc />
    protected MongoCollectionRepository(DmMongoClient client) : base(client)
    {
    }

    /// <summary>
    /// Typed filter definition builder
    /// </summary>
    protected static FilterDefinitionBuilder<TEntity> Filter => Filter<TEntity>();

    /// <summary>
    /// Typed update definition builder
    /// </summary>
    /// <remarks>
    /// Named for the builder rather than for the operation, unlike its three
    /// neighbours, because a repository whose own contract has an Update method
    /// otherwise collides with it: the method hides the property, and the two
    /// repositories that hit this wrote `public new` to say so. `new` silences
    /// the compiler without answering the question it asks — inside such a
    /// repository the bare name then means the method, and the builder is
    /// unreachable by name at all.
    /// </remarks>
    protected static UpdateDefinitionBuilder<TEntity> UpdateBuilder => Update<TEntity>();

    /// <summary>
    /// Typed sort definition builder
    /// </summary>
    protected static SortDefinitionBuilder<TEntity> Sort => Sort<TEntity>();

    /// <summary>
    /// Typed projection definition builder
    /// </summary>
    protected static ProjectionDefinitionBuilder<TEntity> Project => Project<TEntity>();

    /// <summary>
    /// Typed collection
    /// </summary>
    protected IMongoCollection<TEntity> Collection => Collection<TEntity>();
}
