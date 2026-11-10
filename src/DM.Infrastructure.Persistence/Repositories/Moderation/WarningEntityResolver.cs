using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Core.Chats;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
/// <remarks>
/// Every place of WarningEntityType: forum topics, comments of any discussion,
/// messages of the global chat, reviews of games and of posts. Nothing of a game
/// room or a conversation is moderated, and a review of a post is read where its
/// post is. Addresses are spelled the way the
/// client router spells them, and an object that cannot be addressed gets none.
/// The reads lift the soft-delete filter, because a warning outlives its text, and
/// ask each owner about its own deletion. Guest visibility uses
/// <see cref="ModuleVisibility.IsPubliclyVisible" /> and the board's guest flag;
/// a place closed to a guest is named for the open log to ask its viewer about
/// (<see cref="IPlaceReading" />).
/// </remarks>
internal class WarningEntityResolver : IWarningEntityResolver
{
    private readonly DmDbContext _dbContext;

    public WarningEntityResolver(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<string?> CaptureSnapshot(
        WarningEntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        if (entityId == Guid.Empty)
        {
            return null;
        }

        // The very text the moderator was reading when they pressed "warn": the
        // same column the discussion, the topic and the chat are rendered from.
        return entityType switch
        {
            WarningEntityType.Comment => await _dbContext.Comments
                .Where(c => c.CommentId == entityId).Select(c => c.Text).FirstOrDefaultAsync(ct),
            WarningEntityType.Topic => await _dbContext.Topics
                .Where(t => t.TopicId == entityId).Select(t => t.Text).FirstOrDefaultAsync(ct),
            // Messages holds four kinds of conversation in one table, and only
            // the global chat has a warn button and a public page. A direct
            // message, a group chat and a game room chat are none of
            // moderation's business, and a snapshot is a verbatim copy kept for
            // as long as the warning lives — so the filter is here and not left
            // to whoever calls with an identifier.
            WarningEntityType.Message => await _dbContext.Messages
                .Where(m => m.MessageId == entityId && m.ChatId == WellKnownChats.GlobalChatId)
                .Select(m => m.Text).FirstOrDefaultAsync(ct),
            WarningEntityType.GameReview => await _dbContext.GameReviews
                .Where(r => r.GameReviewId == entityId).Select(r => r.Text).FirstOrDefaultAsync(ct),
            WarningEntityType.PostReview => await _dbContext.PostReviews
                .Where(r => r.PostReviewId == entityId).Select(r => r.Text).FirstOrDefaultAsync(ct),
            _ => null
        };
    }

    /// <inheritdoc />
    public async Task<ProfileCapture?> CaptureProfile(Guid userId, CancellationToken ct = default) =>
        // What the profile shows everybody, and of that only what the warning is
        // about: the name, the place, the contacts and the birthday are the
        // person's to erase later, and the open log keeps what it holds for good.
        // The avatar column names only a confirmed upload (UserRepository), so the
        // identifier is all the warning needs to pin the picture.
        await _dbContext.Users
            .Where(u => u.UserId == userId)
            .Select(u => new ProfileCapture
            {
                Status = u.Status,
                Info = u.Info,
                AvatarUploadId = u.AvatarUploadId
            })
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, WarningEntityState>> ResolveStates(
        IReadOnlyCollection<WarningEntityRequest> requests, CancellationToken ct = default)
    {
        var states = new Dictionary<Guid, WarningEntityState>();
        if (requests.Count == 0)
        {
            return states;
        }

        await DescribeComments(Of(requests, WarningEntityType.Comment), states, ct);
        await DescribeTopics(Of(requests, WarningEntityType.Topic), states, ct);
        await DescribeMessages(Of(requests, WarningEntityType.Message), states, ct);
        await DescribeGameReviews(Of(requests, WarningEntityType.GameReview), states, ct);
        await DescribePostReviews(Of(requests, WarningEntityType.PostReview), states, ct);
        return states;
    }

    private static List<WarningEntityRequest> Of(
        IEnumerable<WarningEntityRequest> requests, WarningEntityType entityType) =>
        requests.Where(r => r.EntityType == entityType && r.EntityId != Guid.Empty).ToList();

    private sealed record Described(
        string? Url,
        WarningPlaceKind? PlaceKind,
        string? PlaceTitle,
        DateTimeOffset? CreatedUtc,
        WarningLogClosure? LogClosure,
        ReadablePlace? ClosedPlace = null,
        WarningLogClosure? ClosureForReader = null);

    /// <summary>
    /// A text of a module, closed as <see cref="TextClosure" /> says. A place closed to a guest is
    /// named, and for its reader the text is closed by its deletion alone.
    /// </summary>
    private static Described InModule(string? url, WarningPlaceKind placeKind, string? placeTitle,
        DateTimeOffset? createdUtc, WarningLogClosure? placeClosure, bool removedByModeration, ReadablePlace place) =>
        new(url, placeKind, placeTitle, createdUtc,
            TextClosure(placeClosure, removedByModeration),
            placeClosure == WarningLogClosure.PlaceNotPublic ? place : null,
            TextClosure(null, removedByModeration));

    /// <summary>
    /// Deleted by somebody other than its author, or by nobody the row names:
    /// moderation took it off the site. The open log still shows the text, as the
    /// one the warning was given for, and has no place on the site to link it to.
    /// </summary>
    private static bool RemovedByModeration(bool isRemoved, Guid? deletedByUserId, Guid authorId) =>
        isRemoved && deletedByUserId != authorId;

    /// <summary>
    /// Why a guest may not read what stands in a game or a blog, or null when he
    /// may. Closed first: a deleted module that was closed takes the text of the
    /// open log with it (WarningLogClosure), a deleted open one only the link.
    /// </summary>
    private static WarningLogClosure? ModuleClosure(
        bool isRemoved, ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        if (!ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility))
        {
            return WarningLogClosure.PlaceNotPublic;
        }

        return isRemoved ? WarningLogClosure.PlaceRemoved : null;
    }

    /// <summary>
    /// Why a guest may not read what stands in a topic, or null when he may:
    /// every forum section is open to guests, so only a deleted topic closes it.
    /// </summary>
    private static WarningLogClosure? TopicClosure(bool topicRemoved) =>
        topicRemoved ? WarningLogClosure.PlaceRemoved : null;

    /// <summary>
    /// The closure of a text by its place, and failing that by its deletion: a
    /// text moderation took out of a closed place is closed all the same.
    /// </summary>
    private static WarningLogClosure? TextClosure(WarningLogClosure? placeClosure, bool removedByModeration) =>
        placeClosure ?? (removedByModeration ? WarningLogClosure.RemovedByModeration : null);

    private sealed record CommentRow(
        Guid CommentId, Guid OwnerId, Guid AuthorId, DateTimeOffset CreatedUtc, bool IsRemoved, Guid? DeletedByUserId)
    {
        public string Anchor => $"#comment-{CommentId}";

        public bool ByModeration => RemovedByModeration(IsRemoved, DeletedByUserId, AuthorId);
    }

    private async Task DescribeComments(
        List<WarningEntityRequest> requests, Dictionary<Guid, WarningEntityState> states, CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var ids = requests.Select(r => r.EntityId).Distinct().ToList();

        // A comment names its owner by a bare identifier with no discriminator
        // column, so which discussion it belongs to is answered by asking the
        // four tables that can own one. A publication is one of them: its
        // discussion carries the warn button every discussion does, and a
        // warning given there named no place at all while it was left out.
        var comments = await _dbContext.Comments
            .IgnoreQueryFilters()
            .Where(c => ids.Contains(c.CommentId))
            .Select(c => new CommentRow(
                c.CommentId, c.EntityId, c.AuthorId, c.CreatedUtc, c.IsRemoved, c.DeletedByUserId))
            .ToListAsync(ct);

        var owners = comments.Select(c => c.OwnerId).Distinct().ToList();

        // The four tables are asked in this order, and the first that owns a
        // comment places it.
        var described = new Dictionary<Guid, Described>();
        await CommentsInTopics(comments, owners, described, ct);
        await CommentsInBlogs(comments, owners, described, ct);
        await CommentsInGames(comments, owners, described, ct);
        await CommentsInPublications(comments, owners, described, ct);

        // An owner that is gone altogether: no place to name and nowhere to
        // link, the text still shown as the one the warning was given for.
        foreach (var comment in comments)
        {
            described.TryAdd(comment.CommentId, new Described(
                null, null, null, comment.CreatedUtc, WarningLogClosure.NotFound));
        }

        Fill(requests, states, described);
    }

    private async Task CommentsInTopics(
        List<CommentRow> comments, List<Guid> owners, Dictionary<Guid, Described> described, CancellationToken ct)
    {
        var topics = await _dbContext.Topics
            .IgnoreQueryFilters()
            .Where(t => owners.Contains(t.TopicId))
            .Select(t => new { t.TopicId, t.Title, t.Board.Alias, t.TopicNumber, t.IsRemoved })
            .ToListAsync(ct);

        Place(comments, topics.ToDictionary(t => t.TopicId), described, (comment, topic) => new Described(
            comment.IsRemoved || topic.IsRemoved ? null : $"/forum/{topic.Alias}/{topic.TopicNumber}{comment.Anchor}",
            WarningPlaceKind.Topic,
            topic.Title,
            comment.CreatedUtc,
            TextClosure(TopicClosure(topic.IsRemoved), comment.ByModeration)));
    }

    private async Task CommentsInBlogs(
        List<CommentRow> comments, List<Guid> owners, Dictionary<Guid, Described> described, CancellationToken ct)
    {
        var blogs = await _dbContext.Blogs
            .IgnoreQueryFilters()
            .Where(b => owners.Contains(b.BlogId))
            .Select(b => new
            {
                b.BlogId,
                b.PublicId,
                b.Title,
                b.IsRemoved,
                b.Status,
                b.PremoderationStatus,
                b.DraftVisibility
            })
            .ToListAsync(ct);

        Place(comments, blogs.ToDictionary(b => b.BlogId), described, (comment, blog) => InModule(
            comment.IsRemoved || blog.IsRemoved ? null : $"/blogs/{blog.PublicId}/comments{comment.Anchor}",
            WarningPlaceKind.Blog,
            blog.Title,
            comment.CreatedUtc,
            ModuleClosure(blog.IsRemoved, blog.Status, blog.PremoderationStatus, blog.DraftVisibility),
            comment.ByModeration,
            new ReadablePlace(ReadablePlaceKind.Blog, blog.BlogId)));
    }

    private async Task CommentsInGames(
        List<CommentRow> comments, List<Guid> owners, Dictionary<Guid, Described> described, CancellationToken ct)
    {
        var games = await _dbContext.Games
            .IgnoreQueryFilters()
            .Where(g => owners.Contains(g.GameId))
            .Select(g => new
            {
                g.GameId,
                g.PublicId,
                g.Title,
                g.IsRemoved,
                g.Status,
                g.PremoderationStatus,
                g.DraftVisibility
            })
            .ToListAsync(ct);

        Place(comments, games.ToDictionary(g => g.GameId), described, (comment, game) => InModule(
            comment.IsRemoved || game.IsRemoved ? null : $"/game/{game.PublicId}/comments{comment.Anchor}",
            WarningPlaceKind.Game,
            game.Title,
            comment.CreatedUtc,
            ModuleClosure(game.IsRemoved, game.Status, game.PremoderationStatus, game.DraftVisibility),
            comment.ByModeration,
            new ReadablePlace(ReadablePlaceKind.Game, game.GameId)));
    }

    /// <summary>
    /// Describes each comment whose owner is among <paramref name="owners" />, unless a
    /// table asked before it placed the comment already.
    /// </summary>
    private static void Place<TOwner>(
        List<CommentRow> comments,
        Dictionary<Guid, TOwner> owners,
        Dictionary<Guid, Described> described,
        Func<CommentRow, TOwner, Described> describe)
    {
        foreach (var comment in comments)
        {
            if (owners.TryGetValue(comment.OwnerId, out var owner))
            {
                described.TryAdd(comment.CommentId, describe(comment, owner));
            }
        }
    }

    private async Task CommentsInPublications(
        List<CommentRow> comments, List<Guid> owners, Dictionary<Guid, Described> described, CancellationToken ct)
    {
        var publications = await _dbContext.Publications
            .IgnoreQueryFilters()
            .Where(p => owners.Contains(p.PublicationId))
            .Select(p => new
            {
                p.PublicationId,
                p.Title,
                p.IsRemoved,
                BlogPublicId = p.Blog.PublicId,
                BlogRemoved = p.Blog.IsRemoved,
                p.Blog.Status,
                p.Blog.PremoderationStatus,
                p.Blog.DraftVisibility
            })
            .ToListAsync(ct);

        foreach (var comment in comments)
        {
            var publication = publications.FirstOrDefault(p => p.PublicationId == comment.OwnerId);
            if (publication == null)
            {
                continue;
            }

            // The publication is the place, under the title its readers know
            // it by. A guest reads it where the blog is open to him.
            var placeClosure = ModuleClosure(publication.BlogRemoved || publication.IsRemoved, publication.Status,
                publication.PremoderationStatus, publication.DraftVisibility);

            described.TryAdd(comment.CommentId, InModule(
                comment.IsRemoved || publication.IsRemoved || publication.BlogRemoved
                    ? null
                    : $"/blogs/{publication.BlogPublicId}/feed/{publication.PublicationId}{comment.Anchor}",
                WarningPlaceKind.Publication,
                publication.Title,
                comment.CreatedUtc,
                placeClosure,
                comment.ByModeration,
                new ReadablePlace(ReadablePlaceKind.Publication, publication.PublicationId)));
        }
    }

    private async Task DescribeTopics(
        List<WarningEntityRequest> requests, Dictionary<Guid, WarningEntityState> states, CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var ids = requests.Select(r => r.EntityId).Distinct().ToList();

        var topics = await _dbContext.Topics
            .IgnoreQueryFilters()
            .Where(t => ids.Contains(t.TopicId))
            .Select(t => new
            {
                t.TopicId,
                t.Title,
                t.Board.Alias,
                t.TopicNumber,
                t.AuthorId,
                t.CreatedUtc,
                t.IsRemoved,
                t.DeletedByUserId
            })
            .ToListAsync(ct);

        // The topic is both the object and its place: deleted by its author it is
        // still shown under its own title, deleted by moderation it is not.
        var described = topics.ToDictionary(
            t => t.TopicId,
            t => new Described(
                t.IsRemoved ? null : $"/forum/{t.Alias}/{t.TopicNumber}",
                WarningPlaceKind.Topic,
                t.Title,
                t.CreatedUtc,
                TextClosure(null, RemovedByModeration(t.IsRemoved, t.DeletedByUserId, t.AuthorId))));

        Fill(requests, states, described);
    }

    private async Task DescribeMessages(
        List<WarningEntityRequest> requests, Dictionary<Guid, WarningEntityState> states, CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var ids = requests.Select(r => r.EntityId).Distinct().ToList();

        var messages = await _dbContext.Messages
            .IgnoreQueryFilters()
            .Where(m => ids.Contains(m.MessageId))
            .Select(m => new { m.MessageId, m.ChatId, m.UserId, m.CreatedUtc, m.IsRemoved, m.DeletedByUserId })
            .ToListAsync(ct);

        // Global chat is the one chat with a public page and the one chat with a
        // warn button. A direct or group message has no address a moderator could
        // follow, and a game room chat is not moderated at all — both answer with
        // no place, no address and nothing for the open log.
        var described = messages.ToDictionary(
            m => m.MessageId,
            m => m.ChatId == WellKnownChats.GlobalChatId
                ? new Described(
                    m.IsRemoved ? null : $"/global-chat#msg-{m.MessageId}",
                    WarningPlaceKind.GlobalChat,
                    null,
                    m.CreatedUtc,
                    RemovedByModeration(m.IsRemoved, m.DeletedByUserId, m.UserId)
                        ? WarningLogClosure.RemovedByModeration
                        : null)
                : new Described(null, null, null, null, WarningLogClosure.NotFound));

        Fill(requests, states, described);
    }

    /// <summary>
    /// A game as a place: what its address and title are, and whether a guest reads it.
    /// </summary>
    private sealed record GamePlace(
        string PublicId,
        string Title,
        bool IsRemoved,
        ModuleStatus Status,
        PremoderationStatus PremoderationStatus,
        DraftVisibility DraftVisibility)
    {
        public WarningLogClosure? Closure(bool contentRemoved = false) =>
            ModuleClosure(IsRemoved || contentRemoved, Status, PremoderationStatus, DraftVisibility);
    }

    private async Task<Dictionary<Guid, GamePlace>> GamePlaces(IEnumerable<Guid> gameIds, CancellationToken ct)
    {
        var ids = gameIds.Distinct().ToList();
        return await _dbContext.Games
            .IgnoreQueryFilters()
            .Where(g => ids.Contains(g.GameId))
            .Select(g => new
            {
                g.GameId,
                Place = new GamePlace(g.PublicId, g.Title, g.IsRemoved, g.Status, g.PremoderationStatus, g.DraftVisibility)
            })
            .ToDictionaryAsync(g => g.GameId, g => g.Place, ct);
    }

    private async Task DescribeGameReviews(
        List<WarningEntityRequest> requests, Dictionary<Guid, WarningEntityState> states, CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var ids = requests.Select(r => r.EntityId).Distinct().ToList();
        var reviews = await _dbContext.GameReviews
            .IgnoreQueryFilters()
            .Where(r => ids.Contains(r.GameReviewId))
            .Select(r => new { r.GameReviewId, r.GameId, r.AuthorId, r.CreatedUtc, r.IsRemoved, r.DeletedByUserId })
            .ToListAsync(ct);
        var games = await GamePlaces(reviews.Select(r => r.GameId), ct);

        // A review whose game is gone altogether is left out, and the log answers it as one nothing found.
        Fill(requests, states, reviews.Where(r => games.ContainsKey(r.GameId)).ToDictionary(
            r => r.GameReviewId,
            r =>
            {
                var game = games[r.GameId];
                return InModule(
                    r.IsRemoved || game.IsRemoved ? null : $"/game/{game.PublicId}/reviews",
                    WarningPlaceKind.Game,
                    game.Title,
                    r.CreatedUtc,
                    game.Closure(),
                    RemovedByModeration(r.IsRemoved, r.DeletedByUserId, r.AuthorId),
                    new ReadablePlace(ReadablePlaceKind.Game, r.GameId));
            }));
    }

    /// <summary>
    /// The review of a post, placed at its game and read where its post is: a post of a
    /// room closed to a guest closes the review with it.
    /// </summary>
    private async Task DescribePostReviews(
        List<WarningEntityRequest> requests, Dictionary<Guid, WarningEntityState> states, CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var ids = requests.Select(r => r.EntityId).Distinct().ToList();
        var reviews = await _dbContext.PostReviews
            .IgnoreQueryFilters()
            .Where(r => ids.Contains(r.PostReviewId))
            .Select(r => new
            {
                r.PostReviewId,
                r.PostId,
                r.GameId,
                r.AuthorId,
                r.CreatedUtc,
                r.IsRemoved,
                r.DeletedByUserId,
                PostRemoved = r.Post.IsRemoved || r.Post.Room.IsRemoved,
                RoomOpen = r.Post.Room.AccessType == RoomAccessType.Open
            })
            .ToListAsync(ct);
        var games = await GamePlaces(reviews.Select(r => r.GameId), ct);

        // A review whose game is gone altogether is left out, and the log answers it as one nothing found.
        Fill(requests, states, reviews.Where(r => games.ContainsKey(r.GameId)).ToDictionary(
            r => r.PostReviewId,
            r =>
            {
                var game = games[r.GameId];
                var placeClosure = r.RoomOpen ? game.Closure(r.PostRemoved) : WarningLogClosure.PlaceNotPublic;
                // Read where its post is, so the post is the place a viewer is asked about.
                return InModule(
                    r.IsRemoved || r.PostRemoved || game.IsRemoved
                        ? null
                        : $"/game/{game.PublicId}/post-reviews#review-{r.PostReviewId}",
                    WarningPlaceKind.Game,
                    game.Title,
                    r.CreatedUtc,
                    placeClosure,
                    RemovedByModeration(r.IsRemoved, r.DeletedByUserId, r.AuthorId),
                    new ReadablePlace(ReadablePlaceKind.Post, r.PostId));
            }));
    }

    /// <summary>
    /// An entity missing from <paramref name="described"/> is one nothing found:
    /// never there, or gone from the table. It answers with no address and no
    /// place rather than failing the read; the warning still shows the snapshot
    /// taken when it was issued, in the open log and in moderation alike.
    /// </summary>
    private static void Fill(
        IEnumerable<WarningEntityRequest> requests,
        Dictionary<Guid, WarningEntityState> states,
        IReadOnlyDictionary<Guid, Described> described)
    {
        foreach (var request in requests)
        {
            described.TryGetValue(request.EntityId, out var entity);
            states[request.WarningId] = new WarningEntityState
            {
                Url = entity?.Url,
                PlaceKind = entity?.PlaceKind,
                PlaceTitle = entity?.PlaceTitle,
                CreatedUtc = entity?.CreatedUtc,
                LogClosure = entity is null ? WarningLogClosure.NotFound : entity.LogClosure,
                ClosedPlace = entity?.ClosedPlace,
                ClosureForReader = entity?.ClosureForReader
            };
        }
    }
}
