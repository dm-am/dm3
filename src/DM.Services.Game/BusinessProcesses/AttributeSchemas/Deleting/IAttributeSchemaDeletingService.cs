using System;
using System.Threading.Tasks;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Deleting;

/// <summary>
/// Service for attribute schema deleting
/// </summary>
public interface IAttributeSchemaDeletingService
{
    /// <summary>
    /// Delete existing attribute schema
    /// </summary>
    /// <param name="schemaId">Attribute schema identifier</param>
    /// <returns></returns>
    Task Delete(Guid schemaId);
}