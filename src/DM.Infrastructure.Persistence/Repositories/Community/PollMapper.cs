using System.Linq;
using DM.Domain.Community.Features.Polls;
using DbPoll = DM.Infrastructure.Persistence.Entities.Community.Poll;
using DbPollOption = DM.Infrastructure.Persistence.Entities.Community.PollOption;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <summary>
/// Projection formula for polls: a loaded entity to the domain DTO, options
/// ordered and votes flattened to user ids.
/// Written out rather than generated because the ordering of options and the
/// vote flattening are query logic Mapperly attributes cannot express.
/// </summary>
internal static class PollMapper
{
    /// <summary>
    /// Loaded entity (options and votes included) to the domain DTO, for the
    /// write paths that already hold the row. Null flows through - a missing
    /// poll stays a missing poll.
    /// </summary>
    public static Poll ToPoll(this DbPoll? poll) => poll == null ? null! : new()
    {
        Id = poll.PollId,
        StartsUtc = poll.StartsUtc,
        EndsUtc = poll.EndsUtc,
        Title = poll.Title,
        Details = poll.Details,
        IsAnonymous = poll.IsAnonymous,
        Options = poll.Options
            .OrderBy(o => o.Order)
            .Select(ToPollOption)
            .ToList()
    };

    private static PollOption ToPollOption(DbPollOption option) => new()
    {
        Id = option.PollOptionId,
        Text = option.Text,
        UserIds = option.Votes.Select(v => v.UserId).ToList()
    };
}
