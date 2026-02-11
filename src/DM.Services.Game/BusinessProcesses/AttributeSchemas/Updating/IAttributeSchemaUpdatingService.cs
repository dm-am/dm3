using System.Threading.Tasks;
using DM.Services.Game.Dto.Shared;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Updating;

/// <summary>
/// Service for attribute schema updating
/// </summary>
public interface IAttributeSchemaUpdatingService
{
    /// <summary>
    /// Update attribute schema
    /// </summary>
    /// <param name="attributeSchema">DTO for updating</param>
    /// <returns></returns>
    Task<AttributeSchema> Update(AttributeSchema attributeSchema);
}