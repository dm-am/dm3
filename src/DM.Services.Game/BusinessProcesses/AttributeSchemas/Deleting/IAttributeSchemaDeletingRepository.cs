using System;
using System.Threading.Tasks;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Deleting;

/// <summary>
/// Storage for attribute schema deleting
/// </summary>
internal interface IAttributeSchemaDeletingRepository
{
    /// <summary>
    /// Delete existing attribute schema
    /// </summary>
    /// <param name="schemaId">Schema identifier</param>
    /// <returns></returns>
    Task Delete(Guid schemaId);
}