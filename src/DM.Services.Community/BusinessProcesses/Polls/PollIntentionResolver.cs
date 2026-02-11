using System;
using System.Linq;
using DM.Services.Authentication.Dto;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Polls.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;

namespace DM.Services.Community.BusinessProcesses.Polls;

/// <inheritdoc />
internal class PollIntentionResolver :
    IIntentionResolver<PollIntention>,
    IIntentionResolver<PollIntention, (Poll poll, Guid optionId)>,
    IIntentionResolver<PollIntention, Poll>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PollIntentionResolver(
        IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, PollIntention intention) => intention switch
    {
        PollIntention.Create => user.Role >= UserRole.SeniorModerator,
        _ => false
    };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, PollIntention intention, (Poll poll, Guid optionId) target) =>
        intention switch
        {
            PollIntention.Vote when user.IsAuthenticated =>
                target.poll.EndDate > _dateTimeProvider.Now &&
                target.poll.Options.Any(o => o.Id == target.optionId),
            _ => false
        };

    /// <inheritdoc />
    public bool IsAllowed(AuthenticatedUser user, PollIntention intention, Poll poll) =>
        intention switch
        {
            PollIntention.Unvote when user.IsAuthenticated =>
                poll.EndDate > _dateTimeProvider.Now,
            PollIntention.Edit => user.Role >= UserRole.SeniorModerator,
            PollIntention.Delete => user.Role >= UserRole.SeniorModerator,
            _ => false
        };
}