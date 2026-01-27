using System.Collections.Generic;

namespace DM.Services.DataAccess.BusinessObjects.DataContracts;

/// <summary>
/// Entity with edit history tracking
/// </summary>
/// <typeparam name="TEdit">Edit record type</typeparam>
public interface IHasEditHistory<TEdit>
{
    /// <summary>
    /// Edit history collection
    /// </summary>
    ICollection<TEdit> Edits { get; set; }
}
