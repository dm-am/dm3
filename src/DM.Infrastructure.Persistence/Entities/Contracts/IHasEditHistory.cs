using System.Collections.Generic;

namespace DM.Infrastructure.Persistence.Entities.Contracts;

/// <summary>
/// Entity with edit history tracking
/// </summary>
/// <typeparam name="TEdit">Edit record type</typeparam>
internal interface IHasEditHistory<TEdit>
{
    /// <summary>
    /// Edit history collection
    /// </summary>
    ICollection<TEdit> Edits { get; set; }
}
