using System;
using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Game.Dto;
using DM.Services.Game.Dto.Internal;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.Authorization;

/// <inheritdoc />
internal class RoomIntentionResolver :
    IIntentionResolver<RoomIntention, (RoomToUpdate, Guid?)>,
    IIntentionResolver<RoomIntention, RoomToUpdate>,
    IIntentionResolver<RoomIntention, PostPendency>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, RoomIntention intention, (RoomToUpdate, Guid?) target)
    {
        var (room, characterId) = target;
        switch (intention)
        {
            case RoomIntention.CreatePost when characterId.HasValue:
                var access = room.Accesses.FirstOrDefault(a => a.Character?.Id == characterId.Value);
                return access != null &&
                       (access.Character.Author.UserId == user.UserId ||
                        access.Character.IsNpc &&
                        room.Game.Participation(user.UserId).HasFlag(GameParticipation.Authority));
            case RoomIntention.CreatePost:
                return room.Game.Participation(user.UserId).HasFlag(GameParticipation.Authority);
            default:
                return false;
        }
    }

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, RoomIntention intention, RoomToUpdate target) => intention switch
    {
        RoomIntention.CreatePostPendency => target.Accesses.Any(a => a.Character?.Author.UserId == user.UserId) ||
                                            target.Game.Participation(user.UserId).HasFlag(GameParticipation.Authority),
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, RoomIntention intention, PostPendency target) => intention switch
    {
        RoomIntention.DeletePostPendency => target.CreatedBy.UserId == user.UserId,
        _ => false
    };
}
