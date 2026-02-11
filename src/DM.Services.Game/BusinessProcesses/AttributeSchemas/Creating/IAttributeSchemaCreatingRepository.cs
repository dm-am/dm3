using System.Threading.Tasks;
using DM.Services.Game.Dto.Shared;
using DbAttributeSchema = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.AttributeSchema;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Creating;

/// <summary>
/// Storage for attribute schema creating
/// </summary>
internal interface IAttributeSchemaCreatingRepository
{
    /// <summary>
    /// Create new attribute schema
    /// </summary>
    /// <returns></returns>
    Task<AttributeSchema> Create(DbAttributeSchema schema);
}