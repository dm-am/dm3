using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Warnings;
using Microsoft.EntityFrameworkCore;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
/// <remarks>
/// Three kinds are answered here: forum topics, comments of any discussion, and
/// chat messages. Game posts and game room chat are not among them and must not
/// be added — game content is outside moderation, so no warning names it.
///
/// Every address is site-relative and spelled the way the client router spells
/// it. The client builds the same permalinks from its own routes; this is the
/// one place the server needs them, and a warning whose object cannot be
/// addressed answers with no address rather than with a broken one.
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
    /// One edit-history row: which entity was edited, and when. The three edit
    /// tables name those two columns differently, so each caller projects into
    /// this shape and the aggregate below is written once.
    /// </summary>
    private sealed class EditMoment
    {
        public Guid Owner { get; set; }
        public DateTimeOffset Moment { get; set; }
    }

    /// <summary>
    /// Latest edit per entity, for the whole page in one query. Absent from the
    /// result means never edited.
    /// </summary>
    private static async Task<Dictionary<Guid, DateTimeOffset>> LastEdits(
        IQueryable<EditMoment> moments, CancellationToken ct) =>
        await moments.ToDictionaryAsync(m => m.Owner, m => m.Moment, ct);

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
        // three tables that can own one. The fourth owner kind — a publication —
        // has no warn button on any of its screens.
        var comments = await _dbContext.Comments
            .Where(c => ids.Contains(c.CommentId))
            .Select(c => new { c.CommentId, OwnerId = c.EntityId })
            .ToListAsync(ct);

        var owners = comments.Select(c => c.OwnerId).Distinct().ToList();

        var topicOwners = await _dbContext.Topics
            .Where(t => owners.Contains(t.TopicId))
            .Select(t => new { t.TopicId, t.Board.Alias, t.TopicNumber })
            .ToListAsync(ct);
        var blogOwners = await _dbContext.Blogs
            .Where(b => owners.Contains(b.BlogId))
            .Select(b => new { b.BlogId, b.PublicId })
            .ToListAsync(ct);
        var gameOwners = await _dbContext.Games
            .Where(g => owners.Contains(g.GameId))
            .Select(g => new { g.GameId, g.PublicId })
            .ToListAsync(ct);

        var editedAt = await LastEdits(_dbContext.CommentEdits
            .Where(e => ids.Contains(e.CommentId))
            .GroupBy(e => e.CommentId)
            .Select(g => new EditMoment { Owner = g.Key, Moment = g.Max(e => e.EditedUtc) }), ct);

        var addresses = new Dictionary<Guid, string?>();
        foreach (var comment in comments)
        {
            var anchor = $"#comment-{comment.CommentId}";
            var topic = topicOwners.FirstOrDefault(t => t.TopicId == comment.OwnerId);
            if (topic != null)
            {
                addresses[comment.CommentId] = $"/forum/{topic.Alias}/{topic.TopicNumber}{anchor}";
                continue;
            }

            var blog = blogOwners.FirstOrDefault(b => b.BlogId == comment.OwnerId);
            if (blog != null)
            {
                addresses[comment.CommentId] = $"/blogs/{blog.PublicId}/comments{anchor}";
                continue;
            }

            var game = gameOwners.FirstOrDefault(g => g.GameId == comment.OwnerId);
            addresses[comment.CommentId] = game != null
                ? $"/game/{game.PublicId}/comments{anchor}"
                : null;
        }

        Fill(requests, states, addresses, editedAt);
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
            .Where(t => ids.Contains(t.TopicId))
            .Select(t => new { t.TopicId, t.Board.Alias, t.TopicNumber })
            .ToListAsync(ct);

        var editedAt = await LastEdits(_dbContext.TopicEdits
            .Where(e => ids.Contains(e.TopicId))
            .GroupBy(e => e.TopicId)
            .Select(g => new EditMoment { Owner = g.Key, Moment = g.Max(e => e.EditedUtc) }), ct);

        var addresses = topics.ToDictionary(
            t => t.TopicId,
            t => (string?)$"/forum/{t.Alias}/{t.TopicNumber}");

        Fill(requests, states, addresses, editedAt);
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
            .Where(m => ids.Contains(m.MessageId))
            .Select(m => new { m.MessageId, m.ChatId })
            .ToListAsync(ct);

        var editedAt = await LastEdits(_dbContext.MessageEdits
            .Where(e => ids.Contains(e.MessageId))
            .GroupBy(e => e.MessageId)
            .Select(g => new EditMoment { Owner = g.Key, Moment = g.Max(e => e.ModifiedUtc) }), ct);

        // Global chat is the one chat with a public page and the one chat with a
        // warn button. A direct or group message has no address a moderator could
        // follow, and a game room chat is not moderated at all — both answer with
        // the snapshot and no link.
        var addresses = messages.ToDictionary(
            m => m.MessageId,
            m => m.ChatId == DbChat.GlobalChatId ? (string?)$"/global-chat#msg-{m.MessageId}" : null);

        Fill(requests, states, addresses, editedAt);
    }

    /// <summary>
    /// An entity missing from <paramref name="addresses"/> is one nothing found:
    /// deleted, or never there. It answers with no address rather than failing the
    /// read, and the warning still shows the snapshot taken when it was issued.
    /// </summary>
    private static void Fill(
        IEnumerable<WarningEntityRequest> requests,
        Dictionary<Guid, WarningEntityState> states,
        IReadOnlyDictionary<Guid, string?> addresses,
        IReadOnlyDictionary<Guid, DateTimeOffset> editedAt)
    {
        foreach (var request in requests)
        {
            states[request.WarningId] = new WarningEntityState
            {
                Url = addresses.TryGetValue(request.EntityId, out var url) ? url : null,
                EditedAfterWarning = editedAt.TryGetValue(request.EntityId, out var moment) &&
                    moment > request.IssuedUtc
            };
        }
    }
}
