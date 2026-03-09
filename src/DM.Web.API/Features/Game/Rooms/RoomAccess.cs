using System;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// API DTO model for room access
/// </summary>
public class RoomAccess
{
    /// <summary>
    /// Access identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy? Policy { get; set; }

    /// <summary>
    /// Character
    /// </summary>
    public Character? Character { get; set; }

    /// <summary>
    /// Reader or character author
    /// </summary>
    public User? User { get; set; }
}
