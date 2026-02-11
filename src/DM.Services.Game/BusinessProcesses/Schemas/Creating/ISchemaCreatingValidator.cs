using DM.Services.Game.Dto.Shared;

namespace DM.Services.Game.BusinessProcesses.Schemas.Creating;

/// <summary>
/// Validator for new attribute schema model
/// </summary>
internal interface ISchemaCreatingValidator
{
    /// <summary>
    /// Validate and throw exception if the model is invalid
    /// </summary>
    /// <param name="attributeSchema"></param>
    void ValidateAndThrow(AttributeSchema attributeSchema);
}