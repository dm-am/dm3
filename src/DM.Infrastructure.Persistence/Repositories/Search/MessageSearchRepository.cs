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
    private const string SearchConfig = "russian";
    private const int SnippetLength = 280;

    // Must match the [private] strip used by the Post.SearchVector generated
    // column (DmDbContext) and PostRepository — private text is never previewed.
    private const string PrivateBlockPattern = @"\[private(=[^\]]*)?\][\s\S]*?\[/private\]";

    // Snippets are built from the raw stored BBCode, which bypasses the
    // viewer-scoped Display render pipeline (BbConverter). [private] (game
    // posts) is hidden from readers who are not the author / an addressee /
    // a game lead, so its content must never appear in a preview. [mod] is
    // NOT stripped: it is public on read (everyone sees an authored mod
    // block), so previewing it leaks nothing. Strip only [private] from every
    // snippet before it leaves the server, matching the same pattern the
    // Post.SearchVector generated column uses.
    private static readonly Regex PrivateBlockRegex = new(
        PrivateBlockPattern,
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly DmDbContext _dbContext;
    private readonly ICursorService _cursorService;

    public MessageSearchRepository(DmDbContext dbContext, ICursorService cursorService)
    {
        _dbContext = dbContext;
        _cursorService = cursorService;
    }

    /// <inheritdoc />
    public async Task<CursorResult<MessageSearchHit>> Search(
        Guid userId, MessageSearchQuery query, CancellationToken ct = default)
    {
        var limit = query.EffectiveLimit;
        var freeText = query.Text?.Trim() ?? "";
        var hasText = freeText.Length > 0;
        var from = query.FromUsername;
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
            if (!string.IsNullOrEmpty(from))
                q = q.Where(m => EF.Functions.ILike(m.Author.Username, from));
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
                Snippet = m.Text
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
            if (!string.IsNullOrEmpty(from))
                q = q.Where(m => EF.Functions.ILike(m.Author.Username, from));
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
                Snippet = m.Text
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
            if (!string.IsNullOrEmpty(from))
                q = q.Where(p => EF.Functions.ILike(p.Author.Username, from));
            if (after.HasValue) q = q.Where(p => p.CreatedUtc >= after.Value);
            if (before.HasValue) q = q.Where(p => p.CreatedUtc <= before.Value);
            if (cursorTs.HasValue)
                q = q.Where(p => p.CreatedUtc <= cursorTs.Value &&
                                 (p.CreatedUtc < cursorTs.Value || p.PostId.CompareTo(cursorId) < 0));

            branches.Add(q.Select(p => new MessageSearchHit
            {
                SourceType = "game",
                SourceId = p.Room.GameId,
                SourceTitle = p.Room.Game.Title,
                Id = p.PostId,
                CreatedUtc = p.CreatedUtc,
                // Snippet from the SAME [private]-stripped expression as the
                // index — private content is never previewed.
                Snippet = DmDbContext.RegexpReplace(p.GameText, PrivateBlockPattern, " ", "gi")
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
        foreach (var row in page)
        {
            // Strip [private] blocks before previewing: search snippets bypass
            // the viewer-scoped Display render, so raw [private] content would
            // otherwise leak to any searcher. [mod] is public on read, so it is
            // left intact. Done server-side, before truncation.
            row.Snippet = Truncate(StripPrivateBlocks(row.Snippet));
        }

        string? nextCursor = null;
        if (hasMore && page.Length > 0)
        {
            var last = page[^1];
            nextCursor = _cursorService.CreateBeforeCursor(last.Id, last.CreatedUtc);
        }

        return new CursorResult<MessageSearchHit>
        {
            Data = page,
            NextCursor = nextCursor,
            PrevCursor = null,
            HasNext = hasMore,
            HasPrev = cursorTs.HasValue
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

    /// <summary>
    /// Removes [private] blocks from a raw snippet. [private] is hidden from
    /// most readers by the viewer-scoped Display render, which search previews
    /// bypass; stripping here keeps that hidden content out of results. [mod]
    /// is public on read and is intentionally left intact. Collapses the
    /// whitespace the removal leaves behind so previews read cleanly.
    /// </summary>
    private static string StripPrivateBlocks(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var stripped = PrivateBlockRegex.Replace(text, " ");
        return Regex.Replace(stripped, @"\s+", " ");
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.Trim();
        return normalized.Length <= SnippetLength
            ? normalized
            : normalized[..SnippetLength] + "…";
    }
}
