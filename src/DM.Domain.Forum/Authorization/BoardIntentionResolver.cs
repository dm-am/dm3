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
                return user.Role >= UserRole.Admin ||
                       target.ModeratorIds.Contains(user.UserId);
            default:
                return false;
        }
    }
}