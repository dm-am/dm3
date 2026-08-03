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

    public bool IsAllowed(IAuthorizationSubject user, GameIntention intention, GameDto target)
    {
        if (intention != GameIntention.Read && intention != GameIntention.ReadComments && !user.IsAuthenticated)
        {
            return false;
        }

        var userIsSeniorModerator = user.Role >= UserRole.SeniorModerator;
        var userIsMentor = user.Role >= UserRole.Mentor;
        var roles = target.GetRoles(user.UserId);

        return intention switch
        {
            // Allow read if: site admin/moderator, master/assistant, the mentor
            // curating the game, pending invitation, or the game is publicly
            // visible. The last one is the rule the storage filter answers the
            // game list with, so a game the user was just shown cannot refuse to
            // open: a draft is public when its master opened the preview, and
            // premoderation hides it anyway.
            //
            // The curator is on the list because premoderation is what hides a
            // newbie's game and the mentor assigned to it is the one who has to
            // open it. The SQL scope already hands them the row, so without this
            // line the moderation queue links to a 403 on the very game the link
            // exists for.
            GameIntention.Read => userIsSeniorModerator ||
                                  roles.HasEditAccess() ||
                                  roles.Contains(GameRole.Mentor) ||
                                  target.HasPendingInvitation(user.UserId) ||
                                  ModuleVisibility.IsPubliclyVisible(
                                      target.Status, target.PremoderationStatus, target.DraftVisibility),
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
            // An ordinary ban silences discussion of games the user is not part
            // of; a game they lead or were accepted into stays open. Posts in
            // rooms are a different intention and are not affected.
            GameIntention.CreateComment when user.IsAuthenticated =>
                !target.BlacklistedUsers.Any(b => b.UserId == user.UserId) &&
                (target.CommentsAccessMode == CommentsAccessMode.Public ||
                target.GetRoles(user.UserId).HasAnyRole()) &&
                user.MaySpeak(inOwnSpace: target.GetRoles(user.UserId).IsOwnGame()),

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
