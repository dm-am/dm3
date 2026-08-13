using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Messaging.Features.Search;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;

namespace DM.Infrastructure.Persistence.Repositories.Search;

/// <summary>
/// Postgres tsvector-backed unified search over messages and game posts.
/// UNION ALL of {global chat} + {participant chats} + {readable game posts},
/// with access re-derived from the caller's identity on every page.
/// </summary>
internal class MessageSearchRepository : IMessageSearchRepository
{
    private const string SearchConfig = SearchTextConfiguration.Name;

    // Must match the [private] strip used by the Post.SearchVector generated
    // column (DmDbContext) and PostRepository — private text is never previewed.
    private const string PrivateBlockPattern = SearchSnippet.PrivateBlockPattern;

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
    /// Fills in the preview of a page: a window around the match, with the match
    /// marked.
    /// </summary>
    /// <remarks>
    /// A second pass over the page rather than part of the projection above, and
    /// not for tidiness. ts_headline re-parses the document it is given, and in the
    /// select list of the ranked query it would run for every message that matched
    /// anywhere the reader can see - to be thrown away by the paging a moment
    /// later. Here it runs once per row a reader will actually see.
    ///
    /// The window is what makes the preview worth reading. Cut from the beginning
    /// instead, it showed the first two hundred characters and almost never the
    /// words that were searched for; and it could not have found them by looking,
    /// because the search matches by lexeme - a query for "странник" matches
    /// "странников", which no substring of the query occurs in.
    /// </remarks>
    private async Task Preview(MessageSearchHit[] page, string query, CancellationToken ct)
    {
        if (page.Length == 0)
        {
            return;
        }

        // A filter with no words in it - by author, by date - matches every row
        // equally, and there is nothing to build a window around. The fallback
        // strips the private block a second time: the projection already removed
        // it, the column cannot fail and a process can.
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var row in page)
            {
                row.SnippetSegments = SearchSnippet.Plain(row.Snippet);
            }

            return;
        }

        var messageIds = page.Where(h => h.SourceType != GameSourceType).Select(h => h.Id).ToArray();
        var postIds = page.Where(h => h.SourceType == GameSourceType).Select(h => h.Id).ToArray();

        var headlines = new Dictionary<Guid, string>();

        if (messageIds.Length > 0)
        {
            foreach (var row in await _dbContext.Messages
                .Where(m => messageIds.Contains(m.MessageId))
                .Select(m => new
                {
                    m.MessageId,
                    Headline = EF.Functions.WebSearchToTsQuery(SearchConfig, query)
                        .GetResultHeadline(SearchConfig, EF.Property<string>(m, "SearchText"), SearchSnippet.HeadlineOptions),
                })
                .ToArrayAsync(ct))
            {
                headlines[row.MessageId] = row.Headline;
            }
        }

        if (postIds.Length > 0)
        {
            foreach (var row in await _dbContext.Posts
                .Where(p => postIds.Contains(p.PostId))
                .Select(p => new
                {
                    p.PostId,
                    Headline = EF.Functions.WebSearchToTsQuery(SearchConfig, query)
                        .GetResultHeadline(SearchConfig, EF.Property<string>(p, "SearchText"), SearchSnippet.HeadlineOptions),
                })
                .ToArrayAsync(ct))
            {
                headlines[row.PostId] = row.Headline;
            }
        }

        foreach (var row in page)
        {
            row.SnippetSegments = headlines.TryGetValue(row.Id, out var headline)
                ? SearchSnippet.Highlight(headline)
                : SearchSnippet.Plain(row.Snippet);
        }
    }

    /// <inheritdoc />
    public async Task<CursorResult<MessageSearchHit>> Search(
        Guid userId, MessageSearchQuery query, CancellationToken ct = default)
    {
        var limit = query.EffectiveLimit;
        var freeText = query.Text?.Trim() ?? "";
        var hasText = freeText.Length > 0;
        // The from: operator names one author, so this is an equality on the login,
        // not a pattern: ILIKE reads "_" and "%" in it as wildcards and cannot use
        // the lower("Username") index either. Lowered once, here, for all three
        // branches below.
        var fromLower = query.FromUsername?.ToLowerInvariant();
        var after = query.After;
        var before = query.Before;
        var globalChatId = DbChat.GlobalChatId;

        // Keyset cursor decodes to "older than (ts, id)".
        DateTimeOffset? cursorTs = null;
        var cursorId = Guid.Empty;
        if (!string.IsNullOrEmpty(query.Cursor) && _cursorService.TryDecode(query.Cursor, out var cd))
        {
            cursorTs = cd.TimestampUtc;
            cursorId = cd.EntityId;
        }

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

        // ── Global chat (readable by everyone) ──
        if (includeGlobal)
        {
            var q = _dbContext.Messages.Where(m => !m.IsRemoved && m.ChatId == globalChatId);
            if (hasText)
                q = q.Where(m => EF.Property<NpgsqlTsVector>(m, "SearchVector")
                    .Matches(EF.Functions.WebSearchToTsQuery(SearchConfig, freeText)));
            if (!string.IsNullOrEmpty(fromLower))
                q = q.Where(m => m.Author.Username.ToLower() == fromLower);
            if (after.HasValue) q = q.Where(m => m.CreatedUtc >= after.Value);
            if (before.HasValue) q = q.Where(m => m.CreatedUtc <= before.Value);
            if (cursorTs.HasValue)
                q = q.Where(m => m.CreatedUtc <= cursorTs.Value &&
                                 (m.CreatedUtc < cursorTs.Value || m.MessageId.CompareTo(cursorId) < 0));

            branches.Add(q.Select(m => new MessageSearchHit
            {
                SourceType = "global",
                SourceId = m.ChatId,
                SourceTitle = null,
                Id = m.MessageId,
                CreatedUtc = m.CreatedUtc,
                Snippet = EF.Property<string>(m, "SearchText")
            }));
        }

        // ── Participant chats (direct/group), reusing the participation gate ──
        if (includeChats)
        {
            var q = _dbContext.Messages.Where(m =>
                !m.IsRemoved &&
                m.ChatId != globalChatId &&
                m.Chat.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId));
            if (chatIds != null)
                q = q.Where(m => chatIds.Contains(m.ChatId));
            if (hasText)
                q = q.Where(m => EF.Property<NpgsqlTsVector>(m, "SearchVector")
                    .Matches(EF.Functions.WebSearchToTsQuery(SearchConfig, freeText)));
            if (!string.IsNullOrEmpty(fromLower))
                q = q.Where(m => m.Author.Username.ToLower() == fromLower);
            if (after.HasValue) q = q.Where(m => m.CreatedUtc >= after.Value);
            if (before.HasValue) q = q.Where(m => m.CreatedUtc <= before.Value);
            if (cursorTs.HasValue)
                q = q.Where(m => m.CreatedUtc <= cursorTs.Value &&
                                 (m.CreatedUtc < cursorTs.Value || m.MessageId.CompareTo(cursorId) < 0));

            branches.Add(q.Select(m => new MessageSearchHit
            {
                SourceType = "chat",
                SourceId = m.ChatId,
                SourceTitle = m.Chat.Title,
                Id = m.MessageId,
                CreatedUtc = m.CreatedUtc,
                Snippet = EF.Property<string>(m, "SearchText")
            }));
        }

        // ── Game posts, reusing the canonical room read gate ──
        if (includeGames)
        {
            var q = _dbContext.Rooms
                .Where(GameAccessibilityFilters.RoomAvailable(userId))
                .SelectMany(r => r.Posts)
                .Where(p => !p.IsRemoved);
            if (gameIds != null)
                q = q.Where(p => gameIds.Contains(p.Room.GameId));
            if (hasText)
                q = q.Where(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                    .Matches(EF.Functions.WebSearchToTsQuery(SearchConfig, freeText)));
            if (!string.IsNullOrEmpty(fromLower))
                q = q.Where(p => p.Author.Username.ToLower() == fromLower);
            if (after.HasValue) q = q.Where(p => p.CreatedUtc >= after.Value);
            if (before.HasValue) q = q.Where(p => p.CreatedUtc <= before.Value);
            if (cursorTs.HasValue)
                q = q.Where(p => p.CreatedUtc <= cursorTs.Value &&
                                 (p.CreatedUtc < cursorTs.Value || p.PostId.CompareTo(cursorId) < 0));

            branches.Add(q.Select(p => new MessageSearchHit
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
            }));
        }

        if (branches.Count == 0)
        {
            return new CursorResult<MessageSearchHit> { Data = Array.Empty<MessageSearchHit>() };
        }

        var combined = branches.Aggregate((acc, next) => acc.Concat(next));

        var rows = await combined
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
