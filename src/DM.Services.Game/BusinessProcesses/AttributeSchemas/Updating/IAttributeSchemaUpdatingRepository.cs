using System.Threading.Tasks;
using DbAttributeSchema = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.AttributeSchema;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Updating;

/// <summary>
/// Storage for attribute schema updating
/// </summary>
internal interface IAttributeSchemaUpdatingRepository
{
    /// <summary>
    /// Update existing attribute schema
    /// </summary>
    /// <param name="schema">DAL model</param>
    /// <returns></returns>
    Task<DbAttributeSchema> UpdateSchema(DbAttributeSchema schema);
}