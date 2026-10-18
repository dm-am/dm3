using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Likes;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;

namespace DM.Infrastructure.Persistence.Repositories.Messaging;

/// <inheritdoc />
internal class MessageRepository : IMessageRepository
{
    private readonly DmDbContext _dbContext;
    private readonly ICursorService _cursorService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public MessageRepository(
        DmDbContext dbContext,
        ICursorService cursorService,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _cursorService = cursorService;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
    }

    /// <summary>
    /// The chat a cursor read pages through, and whose messages it leaves out.
    /// </summary>
    /// <param name="ChatId">The chat</param>
    /// <param name="HiddenAuthors">
    /// Authors the reader asked not to see (CursorQuery.HiddenAuthorIds), or null
    /// </param>
    /// <param name="HistoryClearedUtc">
    /// Moment the reader cleared the history up to (CursorQuery.HistoryClearedUtc), or null
    /// </param>
    private sealed record ChatScope(
        Guid ChatId, IReadOnlyCollection<Guid>? HiddenAuthors, DateTimeOffset? HistoryClearedUtc = null);

    private IQueryable<DbMessage> ChatMessages(ChatScope chat)
    {
        var chatId = chat.ChatId;
        var messages = _dbContext.Messages.Where(m => !m.IsRemoved && m.ChatId == chatId);
        if (chat.HiddenAuthors is { Count: > 0 })
        {
            var hiddenAuthors = chat.HiddenAuthors.ToArray();
            messages = messages.Where(m => !hiddenAuthors.Contains(m.UserId));
        }

        if (chat.HistoryClearedUtc is { } clearedUtc)
        {
            messages = messages.Where(m => m.CreatedUtc > clearedUtc);
        }

        return messages;
    }

    // Messages are paged by the total order (CreatedUtc, MessageId). CreatedUtc
    // alone is not an order: two messages can share a timestamp, and then the
    // page boundary — which is what becomes the cursor — is whichever row the
    // plan happened to return, so the next page repeats or skips rows.
    //
    // The leading CreatedUtc bound is redundant as a predicate and load-bearing
    // as an index range: PostgreSQL cannot turn the OR form alone into one, and
    // without it every page scans the whole chat and sorts it. With it the
    // cursor lands in the index condition of IX_Messages_ChatId_CreatedUtc_MessageId
    // and the scan stops at the page.
    private static IQueryable<DbMessage> Older(IQueryable<DbMessage> messages,
        DateTimeOffset timestampUtc, Guid messageId) => messages
        .Where(m => m.CreatedUtc <= timestampUtc &&
            (m.CreatedUtc < timestampUtc || m.MessageId.CompareTo(messageId) < 0));

    private static IQueryable<DbMessage> Newer(IQueryable<DbMessage> messages,
        DateTimeOffset timestampUtc, Guid messageId) => messages
        .Where(m => m.CreatedUtc >= timestampUtc &&
            (m.CreatedUtc > timestampUtc || m.MessageId.CompareTo(messageId) > 0));

    private static IQueryable<DbMessage> OldestLast(IQueryable<DbMessage> messages) => messages
        .OrderByDescending(m => m.CreatedUtc).ThenByDescending(m => m.MessageId);

    private static IQueryable<DbMessage> OldestFirst(IQueryable<DbMessage> messages) => messages
        .OrderBy(m => m.CreatedUtc).ThenBy(m => m.MessageId);

    private Task<Anchor?> ChatMessageAnchor(ChatScope chat, Guid messageId, CancellationToken ct) =>
        ChatMessages(chat with { HiddenAuthors = null })
            .Where(m => m.MessageId == messageId)
            .Select(m => new Anchor(m.MessageId, m.CreatedUtc))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Position of a message in the total page order
    /// </summary>
    private sealed record Anchor(Guid MessageId, DateTimeOffset CreatedUtc);

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<Message?> Get(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        var message = await _dbContext.Messages
            .Where(m => !m.IsRemoved && m.MessageId == messageId)
            // A message the reader cleared from his history is gone for him here
            // as well: reached by its identifier it could still be read, edited,
            // deleted or liked.
            .Where(m => m.Chat.UserLinks.Any(l => !l.IsRemoved && l.UserId == userId &&
                                                  (l.HistoryClearedUtc == null || m.CreatedUtc > l.HistoryClearedUtc)))
            .ProjectToMessage()
            .FirstOrDefaultAsync(ct);

        await FillLikes(message, ct);
        return message;
    }

    /// <inheritdoc />
    public async Task<Message?> GetGlobalChatMessage(Guid messageId, CancellationToken ct = default)
    {
        var message = await _dbContext.Messages
            .Where(m => !m.IsRemoved && m.MessageId == messageId)
            .Where(m => m.Chat.Type == ChatType.Global)
            .ProjectToMessage()
            .FirstOrDefaultAsync(ct);

        await FillLikes(message, ct);
        return message;
    }

    /// <inheritdoc />
    public async Task<CursorResult<Message>> GetWithCursor(Guid chatId, CursorQuery query, CancellationToken ct = default)
    {
        var limit = query.EffectiveLimit;
        var chat = new ChatScope(chatId, query.HiddenAuthorIds, query.HistoryClearedUtc);

        // If we have a cursor, decode it
        if (!string.IsNullOrEmpty(query.Cursor) && _cursorService.TryDecode(query.Cursor, out var cursorData))
        {
            return cursorData.Direction == CursorDirection.Before
                ? await GetBefore(chat, cursorData.EntityId, cursorData.TimestampUtc, limit, ct)
                : await GetAfter(chat, cursorData.EntityId, cursorData.TimestampUtc, limit, ct);
        }

        // If we have aroundEntityId, get messages around that message
        if (query.AroundEntityId.HasValue)
        {
            return await GetAround(chat, query.AroundEntityId.Value, limit, ct);
        }

        // If we have nearTimestampUtc, get messages near that timestamp
        if (query.NearTimestampUtc.HasValue)
        {
            return await GetNearTimestamp(chat, query.NearTimestampUtc.Value, limit, ct);
        }

        // Default: get latest messages (newest first, then reverse for display)
        return await GetLatest(chat, limit, ct);
    }

    private async Task<CursorResult<Message>> GetLatest(ChatScope chat, int limit, CancellationToken ct)
    {
        var messages = await OldestLast(ChatMessages(chat))
            .Take(limit + 1) // +1 to check if there are more
            .ProjectToMessage()
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;

        return await CreateCursorResult(ForDisplay(messages, limit), hasPrev: hasMore, hasNext: false, ct);
    }

    // The page arrives newest first and is displayed oldest first. Reversing it
    // keeps the exact order the database produced; re-sorting by CreatedUtc alone
    // would throw the tie-break away again, this time in memory.
    private static Message[] ForDisplay(Message[] newestFirst, int limit) =>
        newestFirst.Take(limit).Reverse().ToArray();

    private async Task<CursorResult<Message>> GetBefore(ChatScope chat, Guid messageId, DateTimeOffset timestampUtc, int limit, CancellationToken ct)
    {
        var messages = await OldestLast(Older(ChatMessages(chat), timestampUtc, messageId))
            .Take(limit + 1)
            .ProjectToMessage()
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;

        // Check if there are messages after
        var hasNext = await Newer(ChatMessages(chat), timestampUtc, messageId).AnyAsync(ct);

        return await CreateCursorResult(ForDisplay(messages, limit), hasPrev: hasMore, hasNext: hasNext, ct);
    }

    private async Task<CursorResult<Message>> GetAfter(ChatScope chat, Guid messageId, DateTimeOffset timestampUtc, int limit, CancellationToken ct)
    {
        var messages = await OldestFirst(Newer(ChatMessages(chat), timestampUtc, messageId))
            .Take(limit + 1)
            .ProjectToMessage()
            .ToArrayAsync(ct);

        var hasMore = messages.Length > limit;
        var result = messages.Take(limit).ToArray();

        // Check if there are messages before
        var hasPrev = await Older(ChatMessages(chat), timestampUtc, messageId).AnyAsync(ct);

        return await CreateCursorResult(result, hasPrev: hasPrev, hasNext: hasMore, ct);
    }

    /// <summary>
    /// Get messages around a specific message
    /// </summary>
    private async Task<CursorResult<Message>> GetAround(ChatScope chat, Guid messageId, int limit, CancellationToken ct = default)
    {
        var referenceMessage = await ChatMessageAnchor(chat, messageId, ct);

        if (referenceMessage == null)
        {
            return await CreateCursorResult(Array.Empty<Message>(), false, false, ct);
        }

        // The anchor takes one of the limit slots, so the halves share limit - 1.
        // Two halves of limit / 2 plus the anchor returned limit + 1 for every even
        // limit — 51 messages on the default page, past the documented maximum of
        // 100 at the top. The remainder goes to the newer side: jumping to a message
        // is a jump into a conversation you then read forward.
        var beforeCount = (limit - 1) / 2;
        var afterCount = limit - 1 - beforeCount;

        // Relative to the whole anchor, not to its timestamp: comparing timestamps
        // alone dropped every message sharing the target's timestamp out of both
        // halves, so they vanished from the window entirely.
        var before = await OldestLast(
                Older(ChatMessages(chat), referenceMessage.CreatedUtc, referenceMessage.MessageId))
            .Take(beforeCount + 1)
            .ProjectToMessage()
            .ToArrayAsync(ct);

        var target = await ChatMessages(chat)
            .Where(m => m.MessageId == messageId)
            .ProjectToMessage()
            .FirstOrDefaultAsync(ct);

        var after = await OldestFirst(
                Newer(ChatMessages(chat), referenceMessage.CreatedUtc, referenceMessage.MessageId))
            .Take(afterCount + 1)
            .ProjectToMessage()
            .ToArrayAsync(ct);

        var hasPrev = before.Length > beforeCount;
        var hasNext = after.Length > afterCount;

        var result = new List<Message>();
        result.AddRange(ForDisplay(before, beforeCount));
        if (target != null) result.Add(target);
        result.AddRange(after.Take(afterCount));

        return await CreateCursorResult(result.ToArray(), hasPrev, hasNext, ct);
    }

    /// <summary>
    /// Get messages near a specific timestamp
    /// </summary>
    private async Task<CursorResult<Message>> GetNearTimestamp(ChatScope chat, DateTimeOffset timestampUtc, int limit, CancellationToken ct = default)
    {
        // Find the first message on or after the timestamp. The tie-break makes the
        // anchor deterministic: ordered by timestamp alone, the same request could
        // pick a different message of a same-instant group and return a different
        // window each time.
        var nearestMessage = await OldestFirst(ChatMessages(chat).Where(m => m.CreatedUtc >= timestampUtc))
            .Select(m => (Guid?)m.MessageId)
            .FirstOrDefaultAsync(ct);

        // No messages after, get the last message before
        nearestMessage ??= await OldestLast(ChatMessages(chat).Where(m => m.CreatedUtc < timestampUtc))
            .Select(m => (Guid?)m.MessageId)
            .FirstOrDefaultAsync(ct);

        if (nearestMessage == null)
        {
            return await CreateCursorResult(Array.Empty<Message>(), false, false, ct);
        }

        return await GetAround(chat, nearestMessage.Value, limit, ct);
    }

    /// <summary>
    /// The one funnel every paged read returns through, and the one place their
    /// likes are filled in: a page assembled here is a page the reader sees whole.
    /// </summary>
    private async Task<CursorResult<Message>> CreateCursorResult(
        Message[] messages, bool hasPrev, bool hasNext, CancellationToken ct)
    {
        await FillLikes(messages, ct);

        string? prevCursor = null;
        string? nextCursor = null;

        if (messages.Length > 0)
        {
            var firstMessage = messages[0];
            var lastMessage = messages[^1];

            if (hasPrev)
            {
                prevCursor = _cursorService.CreateBeforeCursor(firstMessage.Id, firstMessage.CreatedUtc);
            }

            if (hasNext)
            {
                nextCursor = _cursorService.CreateAfterCursor(lastMessage.Id, lastMessage.CreatedUtc);
            }
        }

        return new CursorResult<Message>
        {
            Data = messages,
            PrevCursor = prevCursor,
            NextCursor = nextCursor,
            HasPrev = hasPrev,
            HasNext = hasNext
        };
    }

    // ═══ LIKES ═══

    /// <summary>
    /// Single-message overload of the backfill below.
    /// </summary>
    private Task FillLikes(Message? message, CancellationToken ct) =>
        message is null
            ? Task.CompletedTask
            : FillLikes([message], ct);

    /// <summary>
    /// Backfill <see cref="Message.Likes"/> for a page of messages. What an
    /// empty list costs here is the heart on the page: the client replaces the
    /// message it holds with the one the answer brings back, so a like landed in
    /// the table and went out again on the screen a moment later.
    /// </summary>
    private Task FillLikes(IReadOnlyCollection<Message> messages, CancellationToken ct) =>
        LikeBackfill.Fill(_dbContext, messages, LikeEntityType.Message,
            m => m.Id, (m, likes) => m.Likes = likes,
            "DM.Messaging.MessageLikes", "DM.Messaging.MessageLikers", ct);

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<Message> Create(CreateMessageEntity message, UpdateChatLastMessageEntity updateChat, CancellationToken ct = default)
    {
        // The strategy wrapper is required because the API host configures
        // EnableRetryOnFailure, and the three statements below are one unit: a
        // message that exists while the chat still names an older last line is a
        // state nothing afterwards repairs.
        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            // The chat row is taken first so the pointer below decides against a
            // state no other writer can move until commit. Two messages sent to one
            // chat at the same moment used to run with nothing between them, and the
            // pointer was assigned unconditionally by each of them.
            await _dbContext.Database.ExecuteSqlRawAsync(
                """SELECT "ChatId" FROM "Chats" WHERE "ChatId" = {0} FOR UPDATE""",
                [updateChat.ChatId], cancellation);

            _dbContext.Messages.Add(new DbMessage
            {
                MessageId = message.MessageId,
                ChatId = message.ChatId,
                UserId = message.UserId,
                Text = message.Text,
                CreatedUtc = message.CreatedUtc,
                IsRemoved = message.IsRemoved,
                GlobalChatEventId = message.GlobalChatEventId
            });
            await _dbContext.SaveChangesAsync(cancellation);

            // The message just inserted, and not the identifier the caller put in
            // the chat update beside it. The two say the same thing today, and the
            // statement below compares the chat's current line against the pair
            // (CreatedUtc, MessageId): fed a timestamp from one message and an
            // identifier from another, that comparison answers about a message
            // that does not exist, and the pointer it writes names a row whose
            // timestamp it never checked.
            await MoveLastMessageForward(
                updateChat.ChatId, message.MessageId, message.CreatedUtc, cancellation);

            await transaction.CommitAsync(cancellation);
        }, ct);

        // No backfill of likes here, and it is not an omission: this reads back
        // the row inserted a line above, and nothing can have liked a message
        // that did not exist until then. The empty list is the true answer.
        return await _dbContext.Messages
            .Where(m => m.MessageId == message.MessageId)
            .ProjectToMessage()
            .FirstAsync(ct);
    }

    /// <summary>
    /// Point the chat at the new message unless it already names a newer one.
    /// </summary>
    /// <remarks>
    /// CreatedUtc is stamped before the row lock above is taken, so the message that
    /// commits last is not necessarily the newest one, and an unconditional assignment
    /// sent the conversation's last line backwards: the list of chats reordered around
    /// it and the preview showed a message that is not the last one.
    ///
    /// Kept out of the insert on purpose: one statement doing both would move the
    /// pointer for every message. Written as SQL because the tie-break is over an
    /// identifier: two messages can share a timestamp, and Guid carries no ordering
    /// operator in C# to express that with. The pair comparison is the same order the
    /// successor query in <see cref="Delete" /> reads by, so creation and deletion
    /// agree on which of two simultaneous messages is the newer.
    /// </remarks>
    private Task MoveLastMessageForward(
        Guid chatId, Guid messageId, DateTimeOffset createdUtc, CancellationToken ct) =>
        _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Chats" SET "LastMessageId" = {1}
            WHERE "ChatId" = {0}
              AND ("LastMessageId" IS NULL OR EXISTS (
                  SELECT 1 FROM "Messages" m
                  WHERE m."MessageId" = "Chats"."LastMessageId"
                    AND (m."CreatedUtc", m."MessageId") < ({2}, {1})))
            """,
            [chatId, messageId, createdUtc], ct);

    /// <inheritdoc />
    public async Task<Message> Update(UpdateMessageEntity update)
    {
        var dbMessage = await _dbContext.Messages.FindAsync(update.MessageId);
        if (dbMessage == null)
        {
            throw new InvalidOperationException($"Message {update.MessageId} not found");
        }

        if (update.Text != null)
            dbMessage.Text = update.Text;
        if (update.IsRemoved.HasValue)
            dbMessage.IsRemoved = update.IsRemoved.Value;

        // Modification tracking is handled via Edit history, not inline
        // ModifiedUtc, and nothing used to write that history: the table stayed
        // empty, so every message ever edited looked untouched, and the warning
        // that asks whether the text changed since it was issued read "no" for
        // all of them. The tracker decides what counts: a request re-sending the
        // text the message already holds is not an edit.
        if (update.EditorUserId != Guid.Empty &&
            _dbContext.Entry(dbMessage).Property(m => m.Text).IsModified)
        {
            _dbContext.MessageEdits.Add(new Entities.Messaging.MessageEdit
            {
                MessageEditId = _guidFactory.Create(),
                MessageId = dbMessage.MessageId,
                EditorUserId = update.EditorUserId,
                ModifiedUtc = _dateTimeProvider.Now
            });
        }

        await _dbContext.SaveChangesAsync();
        var updated = await _dbContext.Messages
            .TagWith("DM.Community.UpdatedMessage")
            .Where(m => m.MessageId == update.MessageId)
            .ProjectToMessage()
            .FirstAsync();

        // An edited message keeps the likes it had, and this answer is what the
        // page puts in place of the line it was showing.
        await FillLikes(updated, CancellationToken.None);
        return updated;
    }

    /// <inheritdoc />
    public async Task Delete(Guid messageId, Guid deletedByUserId, CancellationToken ct = default)
    {
        // Which chat a message belongs to never changes, so the address of the lock
        // below can be read before the transaction: what a read taken ahead of a
        // lock must not do is decide anything the lock protects, and neither the
        // pointer nor the successor is read here. A message that is not there - or
        // is already removed, the soft-delete filter being part of this read -
        // leaves nothing to do at all.
        var chatId = await _dbContext.Messages
            .Where(m => m.MessageId == messageId)
            .Select(m => (Guid?)m.ChatId)
            .FirstOrDefaultAsync(ct);
        if (chatId == null)
        {
            return;
        }

        // Both writes or neither. Separately, a refusal between them left the chat
        // pointing at a message that is gone: the conversation shows its own deleted
        // last line in every list, and nothing recomputes the pointer afterwards.
        //
        // Through the strategy because the API host configures EnableRetryOnFailure and
        // a retrying strategy refuses a transaction opened by hand. Both reads are
        // inside the block so a retry sees the state it is retrying against; both
        // writes are set-based, so the change tracker takes no part.
        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            // The same chat row a send takes, and taken first for the same reason:
            // the pointer written below is decided by reading the messages of the
            // chat, and a read cannot see the message another transaction has
            // inserted but not committed. Without the wait, a deletion of the last
            // line and a send arriving together left the chat naming the older of
            // the two - the send set the pointer, then the deletion, which had
            // counted the chat before that message existed, wrote its own answer
            // over it. The lock is the first statement, so both reads below are
            // taken after the wait and see what the send committed.
            await _dbContext.Database.ExecuteSqlRawAsync(
                """SELECT "ChatId" FROM "Chats" WHERE "ChatId" = {0} FOR UPDATE""",
                [chatId.Value], cancellation);

            // Read again under the lock rather than carried in from above: the
            // pointer is exactly what the wait may have moved.
            var messageInfo = await _dbContext.Messages
                .Where(m => m.MessageId == messageId)
                .Select(m => new { m.ChatId, m.Chat.LastMessageId })
                .FirstOrDefaultAsync(cancellation);

            // Mark message as removed
            await _dbContext.Messages
                .Where(m => m.MessageId == messageId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.IsRemoved, true)
                    .SetProperty(m => m.DeletedByUserId, deletedByUserId)
                    .SetProperty(m => m.DeletedUtc, _dateTimeProvider.Now), cancellation);

            // If this was the last message in chat, update LastMessageId
            if (messageInfo?.LastMessageId == messageId)
            {
                // The tie-break is what matters here: ordered by CreatedUtc alone, two
                // messages sharing the chat's last timestamp made LastMessageId whichever
                // one the plan returned. Removed messages were already excluded — the
                // global soft-delete filter applies to _dbContext.Messages too, so the
                // message flagged just above was never a candidate.
                var newLastMessageId = await OldestLast(ChatMessages(new ChatScope(messageInfo.ChatId, null)))
                    .Select(m => (Guid?)m.MessageId)
                    .FirstOrDefaultAsync(cancellation);

                await _dbContext.Chats
                    .Where(c => c.ChatId == messageInfo.ChatId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastMessageId, newLastMessageId), cancellation);
            }

            await transaction.CommitAsync(cancellation);
        }, ct);
    }
}
