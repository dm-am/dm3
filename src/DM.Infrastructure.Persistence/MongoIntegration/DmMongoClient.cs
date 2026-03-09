using DM.Infrastructure.Core.Extensions;
using MongoDB.Driver;
using System.Reflection;

namespace DM.Infrastructure.Persistence.MongoIntegration;

/// <summary>
/// Mongo DB client wrapper
/// </summary>
public class DmMongoClient : MongoClient
{
    private readonly string databaseName;

    private IMongoDatabase Database => GetDatabase(databaseName);

    /// <inheritdoc />
    public DmMongoClient(MongoClientSettings settings, MongoUrl connectionUrl) : base(settings)
    {
        databaseName = connectionUrl.DatabaseName;
    }

    /// <summary>
    /// Get collection for entity type
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <returns>Mongo collection</returns>
    public IMongoCollection<T> GetCollection<T>()
    {
        var mongoCollectionNameAttribute = typeof(T).GetCustomAttribute<MongoCollectionNameAttribute>() ??
                                           throw new AttributeNotFoundException(typeof(MongoCollectionNameAttribute));
        return Database.GetCollection<T>(mongoCollectionNameAttribute.CollectionName);
    }
}
