using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc cref="IIntentionResolver" />
internal class CharacterIntentionResolver :
    IIntentionResolver<CharacterIntention, CharacterToUpdate>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, CharacterIntention intention,
        CharacterToUpdate target)
    {
        // The projection turns an NPC's missing author into Guid.Empty, the id a guest carries.
        var characterOwned = user.IsAuthenticated && target.AuthorId == user.UserId;
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
            // An NPC has no owner, so the leads delete it.
            CharacterIntention.Delete when gameOwned => target.IsNpc,
            CharacterIntention.Accept when gameOwned => target.Status == CharacterStatus.UnderReview ||
                                                        target.Status == CharacterStatus.Declined,
            CharacterIntention.Decline when gameOwned => target.Status == CharacterStatus.UnderReview,
            CharacterIntention.Kill when gameOwned => target.Status == CharacterStatus.Active,
            // Each way back undoes one move of the lead; leaving is final.
            CharacterIntention.Resurrect when gameOwned => target.Status == CharacterStatus.Retired &&
                                                           target.IsDead && !target.IsPlayerLeft,
            CharacterIntention.Readmit when gameOwned => target.Status == CharacterStatus.Retired &&
                                                         target.IsPlayerExiled && !target.IsPlayerLeft,
            CharacterIntention.Exile when gameOwned => target.Status == CharacterStatus.Active,
            CharacterIntention.Leave when characterOwned => target.Status == CharacterStatus.Active,
            _ => false
        };
    }
}
