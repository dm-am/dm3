using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc cref="IIntentionResolver" />
internal class CharacterIntentionResolver :
    IIntentionResolver<CharacterIntention, CharacterToUpdate>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, CharacterIntention intention,
        CharacterToUpdate target)
    {
        // An NPC has no author, but the projection into CharacterToUpdate
        // collapses that null into Guid.Empty — the exact id an anonymous
        // visitor carries. Without the authentication check every guest would
        // own every NPC.
        var characterOwned = user.IsAuthenticated && target.UserId == user.UserId;
        var gameOwned = target.GameMasterId == user.UserId || target.GameAssistantIds.Contains(user.UserId);
        var gameActive = target.GameStatus == ModuleStatus.Active;

        return intention switch
        {
            CharacterIntention.Edit when characterOwned => gameActive,
            CharacterIntention.Edit when gameOwned => target.IsNpc ||
                                                      target.AccessPolicy.HasFlag(CharacterAccessPolicy.EditAllowed),
            CharacterIntention.EditPrivacySettings when characterOwned => gameActive,
            CharacterIntention.EditMasterSettings when gameOwned => true,
            CharacterIntention.Delete when characterOwned => gameActive,
            // An NPC has no author, so no authenticated user owns it — the
            // master and assistants delete them instead.
            CharacterIntention.Delete when gameOwned => target.IsNpc,
            CharacterIntention.Accept when gameOwned => target.Status == CharacterStatus.UnderReview ||
                                                        target.Status == CharacterStatus.Declined,
            CharacterIntention.Decline when gameOwned => target.Status == CharacterStatus.UnderReview,
            CharacterIntention.Kill when gameOwned => target.Status == CharacterStatus.Active,
            CharacterIntention.Resurrect when gameOwned => target.Status == CharacterStatus.Retired && target.IsDead,
            CharacterIntention.Exile when gameOwned => target.Status == CharacterStatus.Active,
            CharacterIntention.Leave when characterOwned => target.Status == CharacterStatus.Active,
            CharacterIntention.Return when characterOwned => target.Status == CharacterStatus.Retired && target.IsPlayerLeft,
            _ => false
        };
    }
}
