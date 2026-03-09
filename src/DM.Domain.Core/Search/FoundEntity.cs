using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Search;

/// <summary>
/// DTO model for search result
/// </summary>
public class FoundEntity
{
    /// <summary>
    /// Entity identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nature of found entity
    /// </summary>
    public SearchEntityType Type { get; set; }

    /// <summary>
    /// Title with highlights
    /// </summary>
    public string FoundTitle { get; set; } = null!;

    /// <summary>
    /// Original title
    /// </summary>
    public string OriginalTitle { get; set; } = null!;

    /// <summary>
    /// Text with highlights
    /// </summary>
    public string FoundText { get; set; } = null!;
}
