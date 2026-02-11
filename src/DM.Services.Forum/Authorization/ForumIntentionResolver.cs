using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Forum.BusinessProcesses.Common;

namespace DM.Services.Forum.Authorization;

/// <inheritdoc />
internal class BoardIntentionResolver : IIntentionResolver<ForumIntention, Dto.Output.Board>
{
    private readonly IAccessPolicyConverter _accessPolicyConverter;

    /// <inheritdoc />
    public BoardIntentionResolver(
        IAccessPolicyConverter accessPolicyConverter)
    {
        _accessPolicyConverter = accessPolicyConverter;
    }

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, ForumIntention intention, Dto.Output.Board target)
    {
        switch (intention)
        {
            case ForumIntention.CreateTopic when user.IsAuthenticated:
                var userPolicy = _accessPolicyConverter.Convert(user.Role);
                return (target.CreateTopicPolicy & userPolicy) != BoardAccessPolicy.None;
            case ForumIntention.AdministrateTopics when user.IsAuthenticated:
                return user.Role >= UserRole.Admin ||
                       target.ModeratorIds.Contains(user.UserId);
            default:
                return false;
        }
    }
}