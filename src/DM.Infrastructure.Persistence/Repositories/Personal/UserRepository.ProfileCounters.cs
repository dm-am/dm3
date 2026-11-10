using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Chats;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Repositories.Account;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

internal partial class UserRepository
{
    private record StatusCountItem(Guid UserId, ModuleStatus Status, int Count);

    private static Dictionary<Guid, ModuleStatusCounts> BuildStatusBreakdownDict(
        IEnumerable<StatusCountItem> primary,
        IEnumerable<StatusCountItem>? secondary)
    {
        var combined = primary.ToList();
        if (secondary != null)
        {
            combined.AddRange(secondary);
        }

        return combined
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var counts = new ModuleStatusCounts();
                    foreach (var item in g)
                    {
                        switch (item.Status)
                        {
                            case ModuleStatus.Draft:
                                counts.Draft += item.Count;
                                break;
                            case ModuleStatus.Active:
                                counts.Active += item.Count;
                                break;
                            case ModuleStatus.Closed:
                                counts.Closed += item.Count;
                                break;
                        }
                    }
                    return counts;
                });
    }

    /// <summary>
    /// The counters the user list draws: recommendations received, and the game and
    /// blog breakdowns behind its three numeric columns.
    /// </summary>
    /// <remarks>
    /// Split out of the profile enrichment because the list was paying for all of it —
    /// twenty-odd aggregates for a page of fifty, of which the table renders four.
    /// Post reviews, bans, drops, likes, subscribers and username history are
    /// profile content and are fetched by the profile. Game reviews received is
    /// the one review counter the list DOES carry: the community table renders
    /// it as a column.
    /// </remarks>
    private async Task PopulateListCounts(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();

        var endorsementsReceived = await CountByUser(_dbContext.UserEndorsements
            .Where(e => userIds.Contains(e.TargetUserId))
            .Select(e => e.TargetUserId));

        // Games hosting: user is master (count games where user is MasterId) - with status breakdown
        var gamesMasterByStatus = await _dbContext.Games
            .Where(g => userIds.Contains(g.MasterId))
            .GroupBy(g => new { g.MasterId, g.Status })
            .Select(g => new StatusCountItem(g.Key.MasterId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Games assistant: with status breakdown
        var gamesAssistantByStatus = await _dbContext.GameAssistants
            .Where(a => userIds.Contains(a.UserId) && !a.Game.IsRemoved)
            .GroupBy(a => new { a.UserId, a.Game.Status })
            .Select(g => new StatusCountItem(g.Key.UserId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Games playing: user has active character - with status breakdown
        var gamesPlayingByStatus = await _dbContext.Characters
            .Where(c => !c.IsNpc && c.AuthorId.HasValue && userIds.Contains(c.AuthorId.Value) && !c.Game.IsRemoved)
            .Select(c => new { AuthorId = c.AuthorId!.Value, c.Game.GameId, c.Game.Status })
            .Distinct()
            .GroupBy(c => new { c.AuthorId, c.Status })
            .Select(g => new StatusCountItem(g.Key.AuthorId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Blogs hosting: user is owner - with status breakdown
        var blogsOwnerByStatus = await _dbContext.Blogs
            .Where(b => userIds.Contains(b.AuthorId))
            .GroupBy(b => new { b.AuthorId, b.Status })
            .Select(g => new StatusCountItem(g.Key.AuthorId, g.Key.Status, g.Count()))
            .ToListAsync();

        // Blogs assistant: with status breakdown
        var blogsAssistantByStatus = await _dbContext.BlogAssistants
            .Where(a => userIds.Contains(a.UserId) && !a.Blog.IsRemoved)
            .GroupBy(a => new { a.UserId, a.Blog.Status })
            .Select(g => new StatusCountItem(g.Key.UserId, g.Key.Status, g.Count()))
            .ToListAsync();

        // A game review received lands on the game's master, the relation
        // GameReviewFilter.GmId selects by, so the counter agrees with its list.
        var gameReviewsReceived = await CountByUser(_dbContext.GameReviews
            .Where(r => !r.Game.IsRemoved && userIds.Contains(r.Game.MasterId))
            .Select(r => r.Game.MasterId));

        // Build status breakdown dictionaries
        var gamesHostingByStatusDict = BuildStatusBreakdownDict(gamesMasterByStatus, gamesAssistantByStatus);
        var gamesPlayingByStatusDict = BuildStatusBreakdownDict(gamesPlayingByStatus, null);
        var blogsHostingByStatusDict = BuildStatusBreakdownDict(blogsOwnerByStatus, blogsAssistantByStatus);

        foreach (var user in usersList)
        {
            user.EndorsementsReceivedCount = endorsementsReceived.GetValueOrDefault(user.UserId);
            user.GameReviewsReceivedCount = gameReviewsReceived.GetValueOrDefault(user.UserId);

            // Games hosting = sum from status breakdown
            user.GamesHostingByStatus = gamesHostingByStatusDict.TryGetValue(user.UserId, out var gh) ? gh : null;
            user.GamesHosting = user.GamesHostingByStatus?.Total ?? 0;

            // Games playing = sum from status breakdown
            user.GamesPlayingByStatus = gamesPlayingByStatusDict.TryGetValue(user.UserId, out var gp) ? gp : null;
            user.GamesPlaying = user.GamesPlayingByStatus?.Total ?? 0;

            // Blogs hosting = sum from status breakdown
            user.BlogsHostingByStatus = blogsHostingByStatusDict.TryGetValue(user.UserId, out var bh) ? bh : null;
            user.BlogsHosting = user.BlogsHostingByStatus?.Total ?? 0;
        }
    }

    private async Task PopulateProfileCounters(IEnumerable<GeneralUser> users)
    {
        var usersList = users.ToList();
        if (!usersList.Any())
        {
            return;
        }

        var userIds = usersList.Select(u => u.UserId).ToList();

        // Everything the list needs is the first part of everything the profile needs.
        await PopulateListCounts(usersList);

        // Sequential: DbContext is not thread-safe.
        await PopulateActivityCounts(usersList, userIds);
        await PopulateSubscribers(usersList, userIds);
        await PopulateUsernameHistory(usersList, userIds);
    }

    /// <summary>
    /// The profile's numbers past the list's: reviews and recommendations given,
    /// what the user wrote, the bans served, the games dropped and the likes received.
    /// </summary>
    private async Task PopulateActivityCounts(List<GeneralUser> usersList, List<Guid> userIds)
    {
        var given = await CountByUser(_dbContext.PostReviews
            .Where(r => userIds.Contains(r.AuthorId))
            .Select(r => r.AuthorId));

        var received = await CountByUser(_dbContext.PostReviews
            .Where(r => userIds.Contains(r.PostAuthorId))
            .Select(r => r.PostAuthorId));

        var endorsementsGiven = await CountByUser(_dbContext.UserEndorsements
            .Where(e => userIds.Contains(e.AuthorId))
            .Select(e => e.AuthorId));

        var gameReviewsGiven = await CountByUser(_dbContext.GameReviews
            .Where(r => userIds.Contains(r.AuthorId))
            .Select(r => r.AuthorId));

        var topicsAuthored = await CountByUser(_dbContext.Topics
            .Where(t => userIds.Contains(t.AuthorId))
            .Select(t => t.AuthorId));

        var commentsAuthored = await CountByUser(_dbContext.Comments
            .Where(c => userIds.Contains(c.AuthorId))
            .Select(c => c.AuthorId));

        var globalChatId = WellKnownChats.GlobalChatId;
        var globalChatMessages = await CountByUser(_dbContext.Messages
            .Where(m => m.ChatId == globalChatId && userIds.Contains(m.UserId))
            .Select(m => m.UserId));

        // A ban lifted early was taken back: the count is of bans a user served.
        var bansReceived = await CountByUser(_dbContext.Bans
            .Where(b => userIds.Contains(b.TargetUserId) && b.LiftedUtc == null)
            .Select(b => b.TargetUserId));

        // A drop is a player leaving on their own: a retired non-NPC with IsPlayerLeft.
        // Death and exile by the master are not drops.
        var gameDrops = await CountByUser(_dbContext.Characters
            .Where(c => c.AuthorId.HasValue
                && userIds.Contains(c.AuthorId.Value)
                && !c.IsNpc
                && !c.Game.IsRemoved
                && c.Status == CharacterStatus.Retired
                && c.IsPlayerLeft)
            .Select(c => c.AuthorId!.Value));

        // Drafts count too.
        var publicationsAuthored = await CountByUser(_dbContext.Publications
            .Where(p => userIds.Contains(p.AuthorId))
            .Select(p => p.AuthorId));

        var likesReceived = await LikesReceived(userIds);

        foreach (var user in usersList)
        {
            user.PostReviewsGivenCount = given.GetValueOrDefault(user.UserId);
            user.PostReviewsReceivedCount = received.GetValueOrDefault(user.UserId);
            user.EndorsementsGivenCount = endorsementsGiven.GetValueOrDefault(user.UserId);
            user.GameReviewsGivenCount = gameReviewsGiven.GetValueOrDefault(user.UserId);
            user.TopicsAuthoredCount = topicsAuthored.GetValueOrDefault(user.UserId);
            user.CommentsAuthoredCount = commentsAuthored.GetValueOrDefault(user.UserId);
            user.GlobalChatMessagesCount = globalChatMessages.GetValueOrDefault(user.UserId);
            user.BansReceivedCount = bansReceived.GetValueOrDefault(user.UserId);
            user.GameDropsCount = gameDrops.GetValueOrDefault(user.UserId);
            user.PublicationsAuthoredCount = publicationsAuthored.GetValueOrDefault(user.UserId);
            user.LikesReceivedCount = likesReceived(user.UserId);
        }
    }

    /// <summary>
    /// Likes are polymorphic, so one join per kind of content, summed per user. Game
    /// posts are not counted: their quality signal is the rating.
    /// </summary>
    private async Task<Func<Guid, int>> LikesReceived(List<Guid> userIds)
    {
        var likesOnTopics = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Topic)
            .Join(_dbContext.Topics.Where(t => userIds.Contains(t.AuthorId)),
                l => l.EntityId, t => t.TopicId, (l, t) => t.AuthorId));

        var likesOnPublications = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Publication)
            .Join(_dbContext.Publications.Where(p => userIds.Contains(p.AuthorId)),
                l => l.EntityId, p => p.PublicationId, (l, p) => p.AuthorId));

        var likesOnComments = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Comment)
            .Join(_dbContext.Comments.Where(c => userIds.Contains(c.AuthorId)),
                l => l.EntityId, c => c.CommentId, (l, c) => c.AuthorId));

        var likesOnMessages = await CountByUser(_dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Message)
            .Join(_dbContext.Messages.Where(m => userIds.Contains(m.UserId)),
                l => l.EntityId, m => m.MessageId, (l, m) => m.UserId));

        return userId => likesOnTopics.GetValueOrDefault(userId) +
                         likesOnPublications.GetValueOrDefault(userId) +
                         likesOnComments.GetValueOrDefault(userId) +
                         likesOnMessages.GetValueOrDefault(userId);
    }

    private async Task PopulateSubscribers(List<GeneralUser> usersList, List<Guid> userIds)
    {
        var subscriberSummaries = await SubscriberSummaryQuery(userIds)
            .ToDictionaryAsync(x => x.UserId);

        foreach (var user in usersList)
        {
            var subscribers = subscriberSummaries.GetValueOrDefault(user.UserId);
            user.Subscribers = subscribers?.Preview ?? [];
            user.SubscribersByCategory = new SubscribersByCategory
            {
                Games = subscribers?.GameSubscribers ?? 0,
                Blogs = subscribers?.BlogSubscribers ?? 0,
                Topics = subscribers?.TopicSubscribers ?? 0,
            };
        }
    }

    /// <summary>One profile's subscribers: a count per category and a capped preview.</summary>
    private sealed class ProfileSubscriberSummary
    {
        public Guid UserId { get; init; }
        public int GameSubscribers { get; init; }
        public int BlogSubscribers { get; init; }
        public int TopicSubscribers { get; init; }
        public List<SubscriberInfo> Preview { get; init; } = [];
    }

    /// <summary>
    /// The total and a capped preview taken in SQL (ROW_NUMBER per target). The Join
    /// keeps both over the same rows: the filter of the subscriber reaches it.
    /// </summary>
    /// <remarks>
    /// Not <see cref="SubscriberSummaries.ByTarget" />, which games and blogs share:
    /// a profile counts by category and previews the subscriber's settings, a game
    /// or blog counts distinct subscribers and previews names.
    /// </remarks>
    private IQueryable<ProfileSubscriberSummary> SubscriberSummaryQuery(List<Guid> userIds) =>
        _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.User && userIds.Contains(s.TargetId))
            .Join(_dbContext.Users, s => s.SubscriberId, u => u.UserId,
                (s, u) => new
                {
                    s.TargetId,
                    s.SubscriptionId,
                    s.SubscriberId,
                    s.Settings,
                    u.Username,
                    u.LastActivityUtc,
                })
            .GroupBy(x => x.TargetId)
            .Select(g => new ProfileSubscriberSummary
            {
                UserId = g.Key,
                // One count per profile line. The preview below is capped before
                // anything knows about categories, so a line drawn from it can be
                // short or empty while the category is full; these are what the
                // line reports. Npgsql turns each into
                // count(*) FILTER (WHERE "Settings" & <bit> <> 0) inside the
                // GROUP BY that is already running over exactly these rows —
                // verified against the generated SQL, so no extra statement and
                // no correlated subquery. Not distinct-counted: two rows for one
                // subscriber is a race artefact, and a category count that
                // disagreed with a distinct total would be the worse lie.
                GameSubscribers = g.Count(x =>
                    (x.Settings & SubscriptionSettings.AuthorGameEvents) != 0),
                BlogSubscribers = g.Count(x =>
                    (x.Settings & SubscriptionSettings.AuthorBlogEvents) != 0),
                TopicSubscribers = g.Count(x =>
                    (x.Settings & SubscriptionSettings.AuthorTopicEvents) != 0),
                // Never-active subscribers sort last; a plain DESC in Postgres
                // would put their nulls first.
                Preview = g.OrderByDescending(x => x.LastActivityUtc != null)
                    .ThenByDescending(x => x.LastActivityUtc)
                    .ThenBy(x => x.SubscriptionId)
                    .Take(SubscriptionPolicy.PreviewCap)
                    .Select(x => new SubscriberInfo
                    {
                        Username = x.Username,
                        LastActivityUtc = x.LastActivityUtc,
                        Settings = x.Settings,
                    })
                    .ToList(),
            });

    private async Task PopulateUsernameHistory(List<GeneralUser> usersList, List<Guid> userIds)
    {
        var usernameHistoryData = await _dbContext.UsernameHistories
            .Where(h => userIds.Contains(h.UserId))
            .OrderByDescending(h => h.ChangedUtc)
            .SelectSpliced(h => new { h.UserId, Entry = UsernameHistoryRepository.Projection.Splice(h) })
            .ToListAsync();

        var usernameHistoryDict = usernameHistoryData
            .GroupBy(h => h.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Entry).ToList());

        foreach (var user in usersList)
        {
            user.UsernameHistory = usernameHistoryDict.TryGetValue(user.UserId, out var history) ? history : [];
        }
    }

    private static Task<Dictionary<Guid, int>> CountByUser(IQueryable<Guid> userIdOfEachRow) =>
        userIdOfEachRow
            .GroupBy(userId => userId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);
}
