using System.Collections.Generic;
using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Dto;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.Authorization;

/// <inheritdoc cref="IIntentionResolver" />
internal class GameIntentionResolver :
    IIntentionResolver<GameIntention>,
    IIntentionResolver<GameIntention, Game>
{
    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, GameIntention intention) => intention switch
    {
        GameIntention.Create when user.IsAuthenticated => true,
        GameIntention.Subscribe when user.IsAuthenticated => true,
        GameIntention.SetStatusModeration when user.IsAuthenticated => user.Role >= UserRole.Mentor,
        _ => false
    };

    private static readonly IEnumerable<GameStatus> HiddenStates = new HashSet<GameStatus>
    {
        GameStatus.Draft
    };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, GameIntention intention, Game target)
    {
        if (intention != GameIntention.Read && intention != GameIntention.ReadComments && !user.IsAuthenticated)
        {
            return false;
        }

        var userIsHighAuthority = user.Role >= UserRole.SeniorModerator;
        var userIsMentor = user.Role >= UserRole.Mentor;
        var participation = target.Participation(user.UserId);

        // Also check premoderation - game is hidden if awaiting approval
        var isHiddenByPremoderation = target.PremoderationStatus != PremoderationStatus.Approved;

        return intention switch
        {
            GameIntention.Read => userIsHighAuthority || participation.HasFlag(GameParticipation.Authority) ||
                                  (!HiddenStates.Contains(target.Status) && !isHiddenByPremoderation),
            GameIntention.Subscribe when user.IsAuthenticated => participation == GameParticipation.None,
            GameIntention.Unsubscribe when user.IsAuthenticated => participation.HasFlag(GameParticipation.Reader),

            GameIntention.Edit when user.IsAuthenticated => userIsHighAuthority ||
                                                            participation.HasFlag(GameParticipation.Authority),
            // only the master itself is allowed to remove the game
            GameIntention.Delete when user.IsAuthenticated => userIsHighAuthority ||
                                                              user.UserId == target.Master.UserId,

            // Premoderation: mentor takes game for review (AwaitingApproval -> Approved sets MentorId)
            GameIntention.SetStatusModeration when target.PremoderationStatus == PremoderationStatus.AwaitingApproval =>
                userIsHighAuthority || userIsMentor,
            // Premoderation: mentor returns game for edits
            GameIntention.SetStatusDraft when target.PremoderationStatus != PremoderationStatus.Approved =>
                userIsHighAuthority || participation.HasFlag(GameParticipation.Moderator),

            // Draft -> Active (publish)
            GameIntention.SetStatusActive when target.Status == GameStatus.Draft =>
                participation.HasFlag(GameParticipation.Authority),
            // Active -> Draft (unpublish)
            GameIntention.SetStatusDraft when target.Status == GameStatus.Active =>
                participation.HasFlag(GameParticipation.Authority),
            // Closed -> Active (reopen)
            GameIntention.SetStatusActive when target.Status == GameStatus.Closed =>
                participation.HasFlag(GameParticipation.Authority),
            // Active -> Closed (close/freeze/finish)
            GameIntention.SetStatusClosed when target.Status == GameStatus.Active =>
                participation.HasFlag(GameParticipation.Authority),

            GameIntention.ReadComments =>
                target.CommentariesAccessMode != CommentariesAccessMode.Private ||
                target.Participation(user.UserId) != GameParticipation.None,
            GameIntention.CreateComment when user.IsAuthenticated =>
                target.CommentariesAccessMode == CommentariesAccessMode.Public ||
                target.Participation(user.UserId) != GameParticipation.None,
            // Character creation when game is active and recruitment is open
            GameIntention.CreateCharacter when user.IsAuthenticated =>
                target.Status == GameStatus.Active && target.Recruitment?.IsOpen == true,

            // Invitations: only master or assistant can invite players/readers
            GameIntention.InvitePlayer when user.IsAuthenticated =>
                participation.HasFlag(GameParticipation.Authority),
            GameIntention.InviteReader when user.IsAuthenticated =>
                participation.HasFlag(GameParticipation.Authority),
            GameIntention.CancelInvitation when user.IsAuthenticated =>
                participation.HasFlag(GameParticipation.Authority),
            _ => false
        };
    }
}
