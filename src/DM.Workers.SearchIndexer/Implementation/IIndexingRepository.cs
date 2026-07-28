using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Search;

namespace DM.Workers.SearchIndexer.Implementation;

/// <summary>
/// Wrapper over the ElasticSearch indexing mechanism
/// </summary>
internal interface IIndexingRepository
{
    /// <summary>
    /// Store searchable entities in index
    /// </summary>
    /// <param name="entities">Entities to store</param>
    Task Index(params SearchEntity[] entities);

    /// <summary>
    /// Delete indexed document
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    Task Delete(Guid entityId);

    /// <summary>
    /// Delete indexed documents by their parent entity identifier
    /// </summary>
    /// <param name="parentEntityId">Parent entity identifier</param>
    Task DeleteByParent(Guid parentEntityId);

    /// <summary>
    /// Update indexed documents authorized roles by parent entity identifier
    /// </summary>
    /// <param name="parentEntityId">Parent entity identifier</param>
    /// <param name="roles">New authorized roles list</param>
    Task UpdateByParent(Guid parentEntityId, IEnumerable<UserRole> roles);
}