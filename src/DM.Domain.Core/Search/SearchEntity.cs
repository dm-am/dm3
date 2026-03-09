using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Search;

/// <summary>
/// Indexed entity for search engine
/// </summary>
public class SearchEntity
{
    /// <summary>
    /// Entity identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Parent entity identifier
    /// </summary>
    public Guid ParentEntityId { get; set; }

    /// <summary>
    /// Entity type
    /// </summary>
    public SearchEntityType EntityType { get; set; }

    /// <summary>
    /// Title to index
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Text to index
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Authorized roles list
    /// </summary>
    public IEnumerable<UserRole> AuthorizedRoles { get; set; } = [];

    /// <summary>
    /// Authorized user ids list
    /// </summary>
    public IEnumerable<Guid> AuthorizedUsers { get; set; } = [];

    /// <summary>
    /// Unauthorized user ids list
    /// </summary>
    public IEnumerable<Guid> UnauthorizedUsers { get; set; } = [];
}
