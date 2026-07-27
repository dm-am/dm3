using System.Collections.Generic;
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
        GameIntention.Subscribe when user.IsAuthenticated => true,
        GameIntention.SetStatusModeration when user.IsAuthenticated => user.Role >= UserRole.Mentor,
        _ => false
    };

    private static readonly IEnumerable<ModuleStatus> HiddenStates = new HashSet<ModuleStatus>
    {
        ModuleStatus.Draft
    };

    public bool IsAllowed(IAuthorizationSubject user, GameIntention intention, GameDto target)
    {
        if (intention != GameIntention.Read && intention != GameIntention.ReadComments && !user.IsAuthenticated)
        {
            return false;
        }

        var userIsSeniorModerator = user.Role >= UserRole.SeniorModerator;
        var userIsMentor = user.Role >= UserRole.Mentor;
        var roles = target.GetRoles(user.UserId);

        // Also check premoderation - game is hidden if awaiting approval
        var isHiddenByPremoderation = target.PremoderationStatus != PremoderationStatus.Approved;

        return intention switch
        {
            // Allow read if: site admin/moderator, master/assistant, pending invitation, or game is public
            GameIntention.Read => userIsSeniorModerator ||
                                  roles.HasEditAccess() ||
                                  target.HasPendingInvitation(user.UserId) ||
                                  (!HiddenStates.Contains(target.Status) && !isHiddenByPremoderation),
            GameIntention.Subscribe when user.IsAuthenticated => !roles.HasAnyRole(),
            GameIntention.Unsubscribe when user.IsAuthenticated => roles.Contains(GameRole.Reader),

            GameIntention.Edit when user.IsAuthenticated => userIsSeniorModerator ||
                                                            roles.HasEditAccess(),
            // Settings / embedded schema editing is broader than lead-only Edit: mentors may edit too
            GameIntention.EditSettings when user.IsAuthenticated => userIsSeniorModerator ||
                                                                    roles.HasEditAccess() ||
                                                                    roles.Contains(GameRole.Mentor),
            // only the master itself is allowed to remove the game
            GameIntention.Delete when user.IsAuthenticated => userIsSeniorModerator ||
                                                              user.UserId == target.Master.UserId,

            // Premoderation: mentor takes game for review (AwaitingApproval -> Approved sets MentorId)
            GameIntention.SetStatusModeration when target.PremoderationStatus == PremoderationStatus.AwaitingApproval =>
                userIsSeniorModerator || userIsMentor,
            // Premoderation: mentor returns game for edits
            GameIntention.SetStatusDraft when target.PremoderationStatus != PremoderationStatus.Approved =>
                userIsSeniorModerator || roles.Contains(GameRole.Mentor),

            // Draft -> Active (publish)
            GameIntention.SetStatusActive when target.Status == ModuleStatus.Draft =>
                roles.HasEditAccess(),
            // Active -> Draft (unpublish)
            GameIntention.SetStatusDraft when target.Status == ModuleStatus.Active =>
                roles.HasEditAccess(),
            // Closed -> Active (reopen)
            GameIntention.SetStatusActive when target.Status == ModuleStatus.Closed =>
                roles.HasEditAccess(),
            // Active -> Closed (close/freeze/finish), or Closed -> Closed
            // (change reason, e.g. unfreeze a Frozen game to a plain Close)
            GameIntention.SetStatusClosed when target.Status == ModuleStatus.Active ||
                                               target.Status == ModuleStatus.Closed =>
                roles.HasEditAccess(),

            GameIntention.ReadComments =>
                target.CommentsAccessMode != CommentsAccessMode.Private ||
                target.GetRoles(user.UserId).HasAnyRole(),
            GameIntention.CreateComment when user.IsAuthenticated =>
                !target.BlacklistedUsers.Any(b => b.UserId == user.UserId) &&
                (target.CommentsAccessMode == CommentsAccessMode.Public ||
                target.GetRoles(user.UserId).HasAnyRole()),

            // Character creation when:
            // - game is active AND recruitment is open, OR
            // - user has a pending player invitation (allows bypassing closed recruitment)
            GameIntention.CreateCharacter when user.IsAuthenticated =>
                target.Status == ModuleStatus.Active &&
                (target.Recruitment?.IsOpen == true || target.HasPendingPlayerInvitation(user.UserId)),

            // Invitations: only master or assistant can invite players/readers
            GameIntention.InvitePlayer when user.IsAuthenticated =>
                roles.HasEditAccess(),
            GameIntention.InviteReader when user.IsAuthenticated =>
                roles.HasEditAccess(),
            // InviteAssistant: only master (Owner) can invite assistants
            GameIntention.InviteAssistant when user.IsAuthenticated =>
                roles.Contains(GameRole.Master),
            GameIntention.CancelInvitation when user.IsAuthenticated =>
                roles.HasEditAccess(),
            // RemoveUser: only master can remove assistants (players are removed via characters, readers cannot be removed)
            GameIntention.RemoveUser when user.IsAuthenticated =>
                roles.Contains(GameRole.Master),
            _ => false
        };
    }
}
