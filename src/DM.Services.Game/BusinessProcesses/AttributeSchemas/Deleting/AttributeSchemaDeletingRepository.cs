using System;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes;
using DM.Services.DataAccess.MongoIntegration;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Deleting;

/// <inheritdoc />
internal class AttributeSchemaDeletingRepository : MongoCollectionRepository<AttributeSchema>, IAttributeSchemaDeletingRepository
{
    /// <inheritdoc />
    public AttributeSchemaDeletingRepository(DmMongoClient client) : base(client)
    {
    }

    /// <inheritdoc />
    public async Task Delete(Guid schemaId) => await Collection.DeleteOneAsync(Filter.Eq(s => s.Id, schemaId));
}