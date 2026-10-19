using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc cref="IIntentionResolver" />
internal class GameIntentionResolver :
    IIntentionResolver<GameIntention>,
    IIntentionResolver<GameIntention, GameDto>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, GameIntention intention) => intention switch
    {
        GameIntention.Create when user.IsAuthenticated => true,
        // PremoderationAccess owns the rank, so the Read arm below and GameAccessibilityFilters agree.
        GameIntention.SetStatusModeration => user.MayJudgePremoderation(),
        _ => false
    };

    public bool IsAllowed(IAuthorizationSubject user, GameIntention intention, GameDto target)
    {
        if (intention != GameIntention.Read && !user.IsAuthenticated)
        {
            return false;
        }

        var userIsSeniorModerator = user.Role >= UserRole.SeniorModerator;
        var roles = target.GetRoles(user.UserId);

        return intention switch
        {
            // Public visibility is the rule the storage filter lists games with, so a listed game opens.
            // The curator and the premoderation judge open the game the moderation queue links to.
            // The blacklist closes writing, not reading: the game stays public to everybody else.
            GameIntention.Read => userIsSeniorModerator ||
                                  roles.HasEditAccess() ||
                                  roles.Contains(GameRole.Mentor) ||
                                  PremoderationAccess.IsPending(target.PremoderationStatus) &&
                                  user.MayJudgePremoderation() ||
                                  target.HasPendingInvitation(user.UserId) ||
                                  ModuleVisibility.IsPubliclyVisible(
                                      target.Status, target.PremoderationStatus, target.DraftVisibility),

            GameIntention.Edit => userIsSeniorModerator ||
                                  roles.HasEditAccess(),
            // Wider than Edit by the curating mentor, who helps shape the game without its bans or cast.
            GameIntention.EditSettings => userIsSeniorModerator ||
                                          roles.HasEditAccess() ||
                                          roles.Contains(GameRole.Mentor),
            // only the master itself is allowed to remove the game
            GameIntention.Delete => userIsSeniorModerator ||
                                    user.UserId == target.Master?.UserId,

            // Identity only: ModulePremoderationPolicy answers which status the move is legal from.
            GameIntention.SubmitForApproval =>
                user.UserId == target.Master?.UserId,

            GameIntention.SetStatusActive when target.Status == ModuleStatus.Draft =>
                roles.HasEditAccess(),
            GameIntention.SetStatusDraft when target.Status == ModuleStatus.Active =>
                roles.HasEditAccess(),
            GameIntention.SetStatusActive when target.Status == ModuleStatus.Closed =>
                roles.HasEditAccess(),
            // Closed -> Closed changes the reason, e.g. a frozen game to a plain close.
            GameIntention.SetStatusClosed when target.Status == ModuleStatus.Active ||
                                               target.Status == ModuleStatus.Closed =>
                roles.HasEditAccess(),

            // Whoever reads the game reads its discussion; this is about writing. The services ask
            // ThrowIfBlacklisted first, so the refusal names the list rather than a lack of rights.
            GameIntention.CreateComment =>
                !target.IsBlacklisted(user.UserId) &&
                (target.CommentsAccessMode == CommentsAccessMode.Public ||
                roles.Any()) &&
                user.MaySpeak(inOwnSpace: roles.IsOwnGame()),

            // A pending player invitation bypasses closed recruitment; the blacklist closes the application.
            GameIntention.CreateCharacter =>
                !target.IsBlacklisted(user.UserId) &&
                target.Status == ModuleStatus.Active &&
                (target.Recruitment?.IsOpen == true || target.HasPendingPlayerInvitation(user.UserId)),

            GameIntention.InvitePlayer =>
                roles.HasEditAccess(),
            GameIntention.InviteReader =>
                roles.HasEditAccess(),
            GameIntention.InviteAssistant =>
                roles.Contains(GameRole.Master),
            GameIntention.CancelInvitation =>
                roles.HasEditAccess(),
            // Players leave through their characters, readers cannot be removed.
            GameIntention.RemoveUser =>
                roles.Contains(GameRole.Master),
            _ => false
        };
    }
}
