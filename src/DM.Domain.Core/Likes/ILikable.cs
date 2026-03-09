using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Likes;

/// <summary>
/// Interface for entities that can be liked
/// </summary>
public interface ILikable
{
    /// <summary>
    /// Entity identifier
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Type of likable entity
    /// </summary>
    LikeEntityType LikeEntityType { get; }

    /// <summary>
    /// Users who liked this entity
    /// </summary>
    IEnumerable<GeneralUser> Likes { get; set; }
}
