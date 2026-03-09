using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc cref="IIntentionResolver" />
internal class CharacterIntentionResolver :
    IIntentionResolver<CharacterIntention, CharacterToUpdate>,
    IIntentionResolver<CharacterIntention, (Character, GameExtended)>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, CharacterIntention intention,
        CharacterToUpdate target)
    {
        var characterOwned = target.UserId == user.UserId;
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

    private static readonly CharacterIntention[] CharacterGameIntentions =
    {
        CharacterIntention.ViewTemper,
        CharacterIntention.ViewStory,
        CharacterIntention.ViewSkills,
        CharacterIntention.ViewInventory
    };

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, CharacterIntention intention, (Character, GameExtended) target)
    {
        var (character, game) = target;

        if (!CharacterGameIntentions.Contains(intention))
        {
            return false;
        }

        if (game.GetRoles(user.UserId).HasEditAccess())
        {
            return true;
        }

        if (character.Author.UserId == user.UserId)
        {
            return true;
        }

        return intention switch
        {
            CharacterIntention.ViewTemper when !game.HideTemper => true,
            CharacterIntention.ViewStory when !game.HideStory => true,
            CharacterIntention.ViewSkills when !game.HideSkills => true,
            CharacterIntention.ViewInventory when !game.HideInventory => true,
            _ => false
        };
    }
}
