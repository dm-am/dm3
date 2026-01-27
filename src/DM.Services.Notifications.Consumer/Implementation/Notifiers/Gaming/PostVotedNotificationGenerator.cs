using System;
using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Notifications.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Notifications.Consumer.Implementation.Notifiers.Gaming;

/// <summary>
/// Notification generator for post votes (ratings)
/// </summary>
internal class PostVotedNotificationGenerator : BaseNotificationGenerator
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public PostVotedNotificationGenerator(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.PostVoted;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CreateNotification> Generate(Guid entityId)
    {
        var data = await _dbContext.Votes
            .Where(v => v.VoteId == entityId)
            .Select(v => new
            {
                v.TargetUserId,
                v.UserId,
                VoterLogin = v.VotedUser.Login,
                v.GameId,
                GameTitle = v.Game.Title,
                v.PostId,
                v.SignValue,
                v.Type
            })
            .FirstOrDefaultAsync();

        if (data == null)
        {
            yield break;
        }

        // Don't notify if someone votes for their own post (shouldn't happen but safety check)
        if (data.TargetUserId == data.UserId)
        {
            yield break;
        }

        yield return new CreateNotification
        {
            UsersInterested = new[] { data.TargetUserId },
            Metadata = new
            {
                VoterLogin = data.VoterLogin,
                GameTitle = data.GameTitle,
                GameId = data.GameId.EncodeToReadable(),
                PostId = data.PostId.EncodeToReadable(),
                VoteSign = data.SignValue > 0 ? "Positive" : data.SignValue < 0 ? "Negative" : "Neutral",
                VoteType = data.Type.ToString()
            }
        };
    }
}
