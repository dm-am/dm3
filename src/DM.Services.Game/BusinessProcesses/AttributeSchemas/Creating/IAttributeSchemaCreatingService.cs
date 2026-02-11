using System.Threading.Tasks;
using DM.Services.Game.Dto.Shared;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Creating;

/// <summary>
/// Service for attribute schema creating
/// </summary>
public interface IAttributeSchemaCreatingService
{
    /// <summary>
    /// Create new attribute schema
    /// </summary>
    /// <param name="attributeSchema">DTO for creating</param>
    /// <returns></returns>
    Task<AttributeSchema> Create(AttributeSchema attributeSchema);
}