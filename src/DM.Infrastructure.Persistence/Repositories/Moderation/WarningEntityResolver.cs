using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Warnings;
using Microsoft.EntityFrameworkCore;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
/// <remarks>
/// Three kinds are answered here: forum topics, comments of any discussion (a
/// topic, a game, a blog, a publication), and chat messages. Game posts and game
/// room chat are not among them and must not be added — game content is outside
/// moderation, so no warning names it.
///
/// Every address is site-relative and spelled the way the client router spells
/// it. The client builds the same permalinks from its own routes; this is the
/// one place the server needs them, and a warning whose object cannot be
/// addressed answers with no address rather than with a broken one.
///
/// The reads lift the soft-delete filter and state every deletion they care
/// about by hand. A warning outlives the text it was issued for, and the log
/// still names where that text stood and when it was written: behind the filter
/// a deleted comment answered with nothing at all. Lifting it lifts it for the
/// owners too, so the topic, the game, the blog and the publication are asked
/// about their own deletion explicitly, and no user row is read by them, so the
/// filter over removed accounts has nothing to guard. The one user row read here
/// is the profile a warning is issued on (CaptureProfile), under the filter.
///
/// Whether a guest could read the text where it stood, which decides whether the
/// open log links to it, is answered with the rules that answer it everywhere
/// else, not with a copy of them: the visibility of a module is
/// <see cref="ModuleVisibility.IsPubliclyVisible" /> over the three fields read
/// here, the discussion of a game is read by whoever sees the game, and a board
/// is open to a guest when its view policy carries the guest flag, the
/// predicate TopicRepository filters the forum by.
/// </remarks>
internal class WarningEntityResolver : IWarningEntityResolver
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
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
                .Where(m => m.MessageId == entityId && m.ChatId == DbChat.GlobalChatId)
                .Select(m => m.Text).FirstOrDefaultAsync(ct),
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
        return states;
    }

    private static List<WarningEntityRequest> Of(
        IEnumerable<WarningEntityRequest> requests, WarningEntityType entityType) =>
        requests.Where(r => r.EntityType == entityType && r.EntityId != Guid.Empty).ToList();

    /// <summary>
    /// What one object of the page is, as the three kinds below answer it.
    /// </summary>
    private sealed record Described(
        string? Url,
        WarningPlaceKind? PlaceKind,
        string? PlaceTitle,
        DateTimeOffset? CreatedUtc,
        WarningLogClosure? LogClosure);

    /// <summary>
    /// Deleted by somebody other than its author, or by nobody the row names:
    /// moderation took it off the site. The open log still shows the text, as the
    /// one the warning was given for, and has no place on the site to link it to.
    /// </summary>
    private static bool RemovedByModeration(bool isRemoved, Guid? deletedByUserId, Guid authorId) =>
        isRemoved && deletedByUserId != authorId;

    /// <summary>
    /// Why a guest may not read what stands in a game or a blog, or null when he
    /// may. Deletion first: a deleted module is closed whatever else is true.
    /// </summary>
    private static WarningLogClosure? ModuleClosure(
        bool isRemoved, ModuleStatus status, PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        if (isRemoved)
        {
            return WarningLogClosure.PlaceRemoved;
        }

        return ModuleVisibility.IsPubliclyVisible(status, premoderationStatus, draftVisibility)
            ? null
            : WarningLogClosure.PlaceNotPublic;
    }

    /// <summary>
    /// Why a guest may not read what stands in a topic, or null when he may.
    /// </summary>
    private static WarningLogClosure? TopicClosure(bool topicRemoved, BoardAccessPolicy viewPolicy)
    {
        if (topicRemoved)
        {
            return WarningLogClosure.PlaceRemoved;
        }

        return (viewPolicy & BoardAccessPolicy.Guest) != BoardAccessPolicy.None
            ? null
            : WarningLogClosure.BoardClosed;
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
            .Select(c => new
            {
                c.CommentId,
                OwnerId = c.EntityId,
                c.AuthorId,
                c.CreatedUtc,
                c.IsRemoved,
                c.DeletedByUserId
            })
            .ToListAsync(ct);

        var owners = comments.Select(c => c.OwnerId).Distinct().ToList();

        var topicOwners = await _dbContext.Topics
            .IgnoreQueryFilters()
            .Where(t => owners.Contains(t.TopicId))
            .Select(t => new { t.TopicId, t.Title, t.Board.Alias, t.TopicNumber, t.IsRemoved, t.Board.ViewPolicy })
            .ToListAsync(ct);
        var blogOwners = await _dbContext.Blogs
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
        var gameOwners = await _dbContext.Games
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
        var publicationOwners = await _dbContext.Publications
            .IgnoreQueryFilters()
            .Where(p => owners.Contains(p.PublicationId))
            .Select(p => new
            {
                p.PublicationId,
                p.Title,
                p.IsRemoved,
                p.IsPublished,
                BlogPublicId = p.Blog.PublicId,
                BlogRemoved = p.Blog.IsRemoved,
                p.Blog.Status,
                p.Blog.PremoderationStatus,
                p.Blog.DraftVisibility
            })
            .ToListAsync(ct);

        var described = new Dictionary<Guid, Described>();
        foreach (var comment in comments)
        {
            var anchor = $"#comment-{comment.CommentId}";
            var byModeration = RemovedByModeration(comment.IsRemoved, comment.DeletedByUserId, comment.AuthorId);

            var topic = topicOwners.FirstOrDefault(t => t.TopicId == comment.OwnerId);
            if (topic != null)
            {
                described[comment.CommentId] = new Described(
                    comment.IsRemoved || topic.IsRemoved ? null : $"/forum/{topic.Alias}/{topic.TopicNumber}{anchor}",
                    WarningPlaceKind.Topic,
                    topic.Title,
                    comment.CreatedUtc,
                    byModeration ? WarningLogClosure.RemovedByModeration : TopicClosure(topic.IsRemoved, topic.ViewPolicy));
                continue;
            }

            var blog = blogOwners.FirstOrDefault(b => b.BlogId == comment.OwnerId);
            if (blog != null)
            {
                described[comment.CommentId] = new Described(
                    comment.IsRemoved || blog.IsRemoved ? null : $"/blogs/{blog.PublicId}/comments{anchor}",
                    WarningPlaceKind.Blog,
                    blog.Title,
                    comment.CreatedUtc,
                    byModeration
                        ? WarningLogClosure.RemovedByModeration
                        : ModuleClosure(blog.IsRemoved, blog.Status, blog.PremoderationStatus, blog.DraftVisibility));
                continue;
            }

            var game = gameOwners.FirstOrDefault(g => g.GameId == comment.OwnerId);
            if (game != null)
            {
                var closure = byModeration
                    ? WarningLogClosure.RemovedByModeration
                    : ModuleClosure(game.IsRemoved, game.Status, game.PremoderationStatus, game.DraftVisibility);

                described[comment.CommentId] = new Described(
                    comment.IsRemoved || game.IsRemoved ? null : $"/game/{game.PublicId}/comments{anchor}",
                    WarningPlaceKind.Game,
                    game.Title,
                    comment.CreatedUtc,
                    closure);
                continue;
            }

            var publication = publicationOwners.FirstOrDefault(p => p.PublicationId == comment.OwnerId);
            if (publication != null)
            {
                // The publication is the place, under the title its readers know
                // it by. A guest reads it where the blog is open to him and the
                // publication is out; an unpublished one is a draft of its author.
                var closure = byModeration
                    ? WarningLogClosure.RemovedByModeration
                    : publication.IsRemoved
                        ? WarningLogClosure.PlaceRemoved
                        : ModuleClosure(publication.BlogRemoved, publication.Status,
                            publication.PremoderationStatus, publication.DraftVisibility);
                if (closure == null && !publication.IsPublished)
                {
                    closure = WarningLogClosure.PlaceNotPublic;
                }

                described[comment.CommentId] = new Described(
                    comment.IsRemoved || publication.IsRemoved || publication.BlogRemoved
                        ? null
                        : $"/blogs/{publication.BlogPublicId}/feed/{publication.PublicationId}{anchor}",
                    WarningPlaceKind.Publication,
                    publication.Title,
                    comment.CreatedUtc,
                    closure);
                continue;
            }

            // An owner that is gone altogether: no place to name and nowhere to
            // link, the text still shown as the one the warning was given for.
            described[comment.CommentId] = new Described(
                null, null, null, comment.CreatedUtc, WarningLogClosure.NotFound);
        }

        Fill(requests, states, described);
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
                t.DeletedByUserId,
                t.Board.ViewPolicy
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
                RemovedByModeration(t.IsRemoved, t.DeletedByUserId, t.AuthorId)
                    ? WarningLogClosure.RemovedByModeration
                    : TopicClosure(false, t.ViewPolicy)));

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
            m => m.ChatId == DbChat.GlobalChatId
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
            var found = described.TryGetValue(request.EntityId, out var entity);
            states[request.WarningId] = new WarningEntityState
            {
                Url = found ? entity!.Url : null,
                PlaceKind = found ? entity!.PlaceKind : null,
                PlaceTitle = found ? entity!.PlaceTitle : null,
                CreatedUtc = found ? entity!.CreatedUtc : null,
                LogClosure = found ? entity!.LogClosure : WarningLogClosure.NotFound
            };
        }
    }
}
