using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Entity that references content polymorphically.
/// Used for Warning entity to link to the content that caused the warning.
/// </summary>
public interface IReferencesContent
{
    /// <summary>
    /// Type of content being referenced (Game, Blog, Post, Comment, etc.)
    /// Null if the warning is not related to specific content.
    /// </summary>
    ContentType? ContentType { get; }

    /// <summary>
    /// ID of the referenced content.
    /// Null if the warning is not related to specific content.
    /// </summary>
    Guid? ContentId { get; }
}
