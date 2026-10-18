using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Boards;

namespace DM.Domain.Forum.Authorization;

/// <inheritdoc />
internal class BoardIntentionResolver : IIntentionResolver<ForumIntention, Board>
{
    private readonly IAccessPolicyConverter _accessPolicyConverter;

    /// <inheritdoc />
    public BoardIntentionResolver(
        IAccessPolicyConverter accessPolicyConverter)
    {
        _accessPolicyConverter = accessPolicyConverter;
    }

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, ForumIntention intention, Board target)
    {
        switch (intention)
        {
            case ForumIntention.CreateTopic when user.IsAuthenticated:
                var userPolicy = _accessPolicyConverter.Convert(user.Role);
                return (target.CreateTopicPolicy & userPolicy) != BoardAccessPolicy.None &&
                       user.MaySpeak();
            case ForumIntention.AdministrateTopics when user.IsAuthenticated:
                return AdministratesTopics(user, target);
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether the user administrates every topic of the board: an administrator,
    /// or a moderator of that board. Shared with the topic's own rules, so the
    /// moderator's right to close a topic is stated once.
    /// </summary>
    internal static bool AdministratesTopics(IAuthorizationSubject user, Board board) =>
        user.Role >= UserRole.Admin || board.ModeratorIds.Contains(user.UserId);
}
