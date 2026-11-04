using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Messaging.Features.Search;
using DM.Domain.Core.Chats;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

namespace DM.Infrastructure.Persistence.Repositories.Search;

/// <summary>
/// Postgres tsvector-backed unified search over messages and game posts.
/// UNION ALL of {global chat} + {participant chats} + {readable game posts},
/// with access re-derived from the caller's identity on every page.
/// </summary>
internal class MessageSearchRepository : IMessageSearchRepository
{
    /// <summary>The one branch of this search that reads a different table.</summary>
    private const string GameSourceType = "game";

    private readonly DmDbContext _dbContext;
    private readonly ICursorService _cursorService;

    public MessageSearchRepository(DmDbContext dbContext, ICursorService cursorService)
    {
        _dbContext = dbContext;
        _cursorService = cursorService;
    }

    /// <summary>
    /// Fills in the preview of a page: a window around the match, with the match marked.
    /// </summary>
    private async Task Preview(MessageSearchHit[] page, string query, CancellationToken ct)
    {
        var messageIds = page.Where(h => h.SourceType != GameSourceType).Select(h => h.Id).ToArray();
        var postIds = page.Where(h => h.SourceType == GameSourceType).Select(h => h.Id).ToArray();

        var headlines = await SearchSnippet.Headlines(query, ct,
            (messageIds, _dbContext.Messages
                .Where(m => messageIds.Contains(m.MessageId))
                .Select(m => new SearchSnippet.Body { Id = m.MessageId, Text = EF.Property<string>(m, "SearchText") })),
            (postIds, _dbContext.Posts
                .Where(p => postIds.Contains(p.PostId))
                .Select(p => new SearchSnippet.Body { Id = p.PostId, Text = EF.Property<string>(p, "SearchText") })));

        foreach (var row in page)
        {
            row.SnippetSegments = headlines.TryGetValue(row.Id, out var headline)
                ? SearchSnippet.Highlight(headline)
                : SearchSnippet.Plain(row.Snippet);
        }
    }

    /// <summary>
    /// What a search asks of every source it reads: the words, the author, the
    /// dates and the place the walk stopped at. Read once from the query, so the
    /// three sources ask it alike.
    /// </summary>
    /// <param name="FreeText">The words, trimmed; empty for a filter with none.</param>
    /// <param name="FromLower">
    /// The from: operator names one author, so this is an equality on the login,
    /// not a pattern: ILIKE reads "_" and "%" in it as wildcards and cannot use
    /// the lower("Username") index either. Lowered once, here, for every source.
    /// </param>
    /// <param name="After">The earliest moment a hit may be written at.</param>
    /// <param name="Before">The latest moment a hit may be written at.</param>
    /// <param name="CursorTs">The keyset cursor decodes to "older than (ts, id)": its ts.</param>
    /// <param name="CursorId">Its id, which orders the hits of one moment.</param>
    private sealed record Criteria(
        string FreeText,
        string? FromLower,
        DateTimeOffset? After,
        DateTimeOffset? Before,
        DateTimeOffset? CursorTs,
        Guid CursorId)
    {
        public bool HasText => FreeText.Length > 0;
    }

    /// <inheritdoc />
    public async Task<CursorResult<MessageSearchHit>> Search(
        Guid userId, MessageSearchQuery query, CancellationToken ct = default)
    {
        // Keyset cursor decodes to "older than (ts, id)".
        DateTimeOffset? cursorTs = null;
        var cursorId = Guid.Empty;
        if (!string.IsNullOrEmpty(query.Cursor) && _cursorService.TryDecode(query.Cursor, out var cd))
        {
            cursorTs = cd.TimestampUtc;
            cursorId = cd.EntityId;
        }

        var criteria = new Criteria(
            query.Text?.Trim() ?? "", query.FromUsername?.ToLowerInvariant(), query.After, query.Before, cursorTs, cursorId);

        // Which sources are in scope. Empty scope list = everything accessible.
        var scopes = query.Scopes;
        var restricted = scopes.Count > 0;
        var includeGlobal = !restricted || scopes.Any(s => s.Type == SearchSourceType.Global);
        var chatScopes = scopes.Where(s => s.Type == SearchSourceType.Chat).ToList();
        var gameScopes = scopes.Where(s => s.Type == SearchSourceType.Game).ToList();
        var includeChats = !restricted || chatScopes.Count > 0;
        var includeGames = !restricted || gameScopes.Count > 0;

        var chatIds = await ResolveIds(chatScopes, isChat: true, ct);
        var gameIds = await ResolveIds(gameScopes, isChat: false, ct);

        var branches = new List<IQueryable<MessageSearchHit>>();
        if (includeGlobal) branches.Add(GlobalChatHits(query, criteria));
        if (includeChats) branches.Add(ChatHits(userId, chatIds, criteria));
        if (includeGames) branches.Add(GamePostHits(userId, gameIds, criteria));

        if (branches.Count == 0)
        {
            return new CursorResult<MessageSearchHit> { Data = Array.Empty<MessageSearchHit>() };
        }

        var combined = branches.Aggregate((acc, next) => acc.Concat(next));
        return await PageOf(combined, query.EffectiveLimit, criteria.FreeText, ct);
    }

    /// <summary>The messages of the global chat (readable by everyone).</summary>
    private IQueryable<MessageSearchHit> GlobalChatHits(MessageSearchQuery query, Criteria criteria)
    {
        var globalChatId = WellKnownChats.GlobalChatId;
        var q = _dbContext.Messages.Where(m => !m.IsRemoved && m.ChatId == globalChatId);
        // The authors the reader hides in the general chat stay hidden in its
        // search (MessageSearchQuery.HiddenAuthorIds).
        if (query.HiddenAuthorIds is { Count: > 0 })
        {
            var hiddenAuthors = query.HiddenAuthorIds.ToArray();
            q = q.Where(m => !hiddenAuthors.Contains(m.UserId));
        }

        return Matching(q, criteria).Select(m => new MessageSearchHit
        {
            SourceType = "global",
            SourceId = m.ChatId,
            SourceTitle = null,
            Id = m.MessageId,
            CreatedUtc = m.CreatedUtc,
            Snippet = EF.Property<string>(m, "SearchText")
        });
    }

    /// <summary>
    /// The messages of the reader's correspondences (direct and group), through
    /// the participation gate. The gate carries the reader's cleared history too:
    /// a search is one more way of reading the conversation, and what he cleared
    /// for himself is not found for him.
    /// </summary>
    private IQueryable<MessageSearchHit> ChatHits(Guid userId, List<Guid>? chatIds, Criteria criteria)
    {
        var globalChatId = WellKnownChats.GlobalChatId;
        var q = _dbContext.Messages.Where(m =>
            !m.IsRemoved &&
            m.ChatId != globalChatId &&
            m.Chat.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId &&
                                      (l.HistoryClearedUtc == null || m.CreatedUtc > l.HistoryClearedUtc)));
        if (chatIds != null)
            q = q.Where(m => chatIds.Contains(m.ChatId));

        return Matching(q, criteria).Select(m => new MessageSearchHit
        {
            SourceType = "chat",
            SourceId = m.ChatId,
            SourceTitle = m.Chat.Title,
            Id = m.MessageId,
            CreatedUtc = m.CreatedUtc,
            Snippet = EF.Property<string>(m, "SearchText")
        });
    }

    /// <summary>The game posts the reader may read, through the canonical room read gate.</summary>
    private IQueryable<MessageSearchHit> GamePostHits(Guid userId, List<Guid>? gameIds, Criteria criteria)
    {
        var q = _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .SelectMany(r => r.Posts)
            .Where(p => !p.IsRemoved);
        if (gameIds != null)
            q = q.Where(p => gameIds.Contains(p.Room.GameId));

        return Matching(q, criteria).Select(p => new MessageSearchHit
        {
            SourceType = GameSourceType,
            SourceId = p.Room.GameId,
            SourceTitle = p.Room.Game.Title,
            Id = p.PostId,
            CreatedUtc = p.CreatedUtc,
            // The projected visible text, which the vector of this row is built
            // from as well: a preview can show nothing the index refused.
            // Snippet from the SAME [private]-stripped expression as the
            // index — private content is never previewed.
            Snippet = EF.Property<string>(p, "SearchText")
        });
    }

    /// <summary>The messages a search asks for: its words, its author, its dates, past its cursor.</summary>
    private static IQueryable<DbMessage> Matching(IQueryable<DbMessage> q, Criteria c)
    {
        if (c.HasText)
            q = q.Where(m => EF.Property<NpgsqlTsVector>(m, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery(SearchTextConfiguration.Name, c.FreeText)));
        if (!string.IsNullOrEmpty(c.FromLower))
            q = q.Where(m => m.Author.Username.ToLower() == c.FromLower);
        if (c.After.HasValue) q = q.Where(m => m.CreatedUtc >= c.After.Value);
        if (c.Before.HasValue) q = q.Where(m => m.CreatedUtc <= c.Before.Value);
        if (c.CursorTs.HasValue)
            q = q.Where(m => m.CreatedUtc <= c.CursorTs.Value &&
                             (m.CreatedUtc < c.CursorTs.Value || m.MessageId.CompareTo(c.CursorId) < 0));
        return q;
    }

    /// <summary>The game posts a search asks for, by the same criteria as the messages.</summary>
    private static IQueryable<DbPost> Matching(IQueryable<DbPost> q, Criteria c)
    {
        if (c.HasText)
            q = q.Where(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                .Matches(EF.Functions.WebSearchToTsQuery(SearchTextConfiguration.Name, c.FreeText)));
        if (!string.IsNullOrEmpty(c.FromLower))
            q = q.Where(p => p.Author.Username.ToLower() == c.FromLower);
        if (c.After.HasValue) q = q.Where(p => p.CreatedUtc >= c.After.Value);
        if (c.Before.HasValue) q = q.Where(p => p.CreatedUtc <= c.Before.Value);
        if (c.CursorTs.HasValue)
            q = q.Where(p => p.CreatedUtc <= c.CursorTs.Value &&
                             (p.CreatedUtc < c.CursorTs.Value || p.PostId.CompareTo(c.CursorId) < 0));
        return q;
    }

    /// <summary>
    /// One page of the hits of every source, newest first, with its preview and
    /// the cursor of the next page.
    /// </summary>
    private async Task<CursorResult<MessageSearchHit>> PageOf(
        IQueryable<MessageSearchHit> hits, int limit, string freeText, CancellationToken ct)
    {
        var rows = await hits
            .OrderByDescending(h => h.CreatedUtc)
            .ThenByDescending(h => h.Id)
            .Take(limit + 1)
            .ToArrayAsync(ct);

        var hasMore = rows.Length > limit;
        var page = rows.Take(limit).ToArray();
        await Preview(page, freeText, ct);

        string? nextCursor = null;
        if (hasMore && page.Length > 0)
        {
            var last = page[^1];
            nextCursor = _cursorService.CreateBeforeCursor(last.Id, last.CreatedUtc);
        }

        // Forward only. The cursor decodes to "older than (timestamp, id)" and
        // there is no reverse walk over the three branches, so there is no
        // previous-page cursor to hand out - and an envelope must not report a page
        // it cannot produce. It did: HasPrev was true from the second page on while
        // PrevCursor stayed null, which reads to any client as "there is a page
        // back, ask me for it" with nothing to ask with.
        return new CursorResult<MessageSearchHit>
        {
            Data = page,
            NextCursor = nextCursor,
            PrevCursor = null,
            HasNext = hasMore,
            HasPrev = false
        };
    }

    /// <summary>
    /// Resolve scope tokens to concrete container ids. Returns null when the
    /// scope kind is unrestricted (search everything accessible). An empty list
    /// means the caller asked for specific ids that all failed to resolve —
    /// the branch then yields nothing, which is the intended "reject" behavior.
    /// </summary>
    private async Task<List<Guid>?> ResolveIds(List<SearchScope> scopes, bool isChat, CancellationToken ct)
    {
        if (scopes.Count == 0) return null;

        var ids = scopes.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToList();
        var raws = scopes.Where(s => s.Id is null && !string.IsNullOrWhiteSpace(s.RawId))
            .Select(s => s.RawId!).ToList();

        if (raws.Count > 0)
        {
            if (isChat)
            {
                ids.AddRange(await _dbContext.Chats
                    .Where(c => c.PublicId != null && raws.Contains(c.PublicId))
                    .Select(c => c.ChatId)
                    .ToListAsync(ct));
            }
            else
            {
                ids.AddRange(await _dbContext.Games
                    .Where(g => raws.Contains(g.PublicId))
                    .Select(g => g.GameId)
                    .ToListAsync(ct));
            }
        }

        return ids;
    }

}
