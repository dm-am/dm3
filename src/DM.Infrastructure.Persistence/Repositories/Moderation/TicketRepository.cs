using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Tickets;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbTicket = DM.Infrastructure.Persistence.Entities.Moderation.Ticket;
using DbTicketComment = DM.Infrastructure.Persistence.Entities.Moderation.TicketComment;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class TicketRepository : ITicketRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public TicketRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetTickets(PagingQuery query,
        TicketStatus? status = null,
        IReadOnlyCollection<TicketSubtype>? subtypes = null, Guid? exceptTargetUserId = null,
        CancellationToken ct = default)
    {
        var tickets = Queue(subtypes, exceptTargetUserId);

        if (status.HasValue)
        {
            tickets = tickets.Where(t => t.Status == status.Value);
        }

        // The intake queue only grows, so the page is taken in the database:
        // the whole of it used to come back on every open of the moderation
        // screen, and the day it stops fitting in a response is a day nobody
        // scheduled. And so is the order: a queue sorted page by page would put
        // an old ticket waiting for moderation behind the closed ones of the
        // first page. The stored numbers of the statuses are the order of the
        // queue: waiting for moderation, waiting for the user, closed, spam.
        var total = await tickets.CountAsync(ct);
        var page = await tickets
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.CreatedUtc)
            .ThenBy(t => t.TicketId)
            .Skip(query.Skip)
            .Take(query.Take)
            .ProjectToTicket()
            .ToListAsync(ct);

        return (page, PagingResult.Create(total, query.Skip + 1, query.Take));
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Ticket> tickets, PagingResult paging)> GetUserTickets(Guid userId,
        PagingQuery query,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        var tickets = _dbContext.Tickets.Where(t => t.UserId == userId);

        if (status.HasValue)
        {
            tickets = tickets.Where(t => t.Status == status.Value);
        }

        if (subtype.HasValue)
        {
            tickets = tickets.Where(t => t.Subtype == subtype.Value);
        }

        // The pile of a long-standing member only grows: the filters narrow it,
        // the page bounds it.
        var total = await tickets.CountAsync(ct);
        var page = await tickets
            .OrderByDescending(t => t.CreatedUtc)
            .Skip(query.Skip)
            .Take(query.Take)
            .ProjectToTicket()
            .ToListAsync(ct);

        return (page, PagingResult.Create(total, query.Skip + 1, query.Take));
    }

    /// <inheritdoc />
    public async Task<Ticket?> Get(Guid ticketId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Where(t => t.TicketId == ticketId)
            .ProjectToTicket()
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ticket?> GetByTrackingToken(string token, CancellationToken ct = default)
    {
        // The token is the credential; an empty token must never match a row
        // (authenticated tickets store a null token).
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        return await _dbContext.Tickets
            .Where(t => t.TrackingToken == token)
            .ProjectToTicket()
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TicketComment>> GetComments(Guid ticketId, CancellationToken ct = default)
    {
        // The identifier breaks the tie of two comments written in one moment, so
        // the numbers of a conversation do not change between two readings of it.
        return await _dbContext.TicketComments
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedUtc)
            .ThenBy(c => c.TicketCommentId)
            .ProjectToTicketComment()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> Create(CreateTicketEntity entity, CancellationToken ct = default)
    {
        var ticket = new DbTicket
        {
            TicketId = entity.TicketId,
            UserId = entity.ReporterUserId,
            TargetId = entity.TargetUserId,
            GuestEmail = entity.GuestEmail,
            TrackingToken = entity.TrackingToken,
            EntityId = entity.EntityId,
            EntityType = entity.EntityType ?? "",
            Status = entity.Status,
            Subtype = entity.Subtype,
            CreatedUtc = entity.CreatedUtc,
            Description = entity.Description,
            Comment = entity.Comment
        };

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(ticket.TicketId, ct) ?? throw new InvalidOperationException("Failed to retrieve created ticket");
    }

    /// <inheritdoc />
    /// <remarks>
    /// The status is moved first, by an UPDATE that holds the condition "still
    /// open" in its WHERE, and that statement is also what takes the lock of the
    /// ticket row (DATA_STORAGE.md, "Транзакция — не снимок"). A closing that
    /// landed after the service read the ticket leaves it touching no row, and the
    /// comment is not written. Every other comment to the same ticket waits on
    /// that lock until this transaction ends, so the count read after it is the
    /// count the insert lands on: the ceiling holds without a second lock.
    ///
    /// A comment that ends the ticket closes it in the same statement, naming its
    /// author and its moment as the closing: the last word and the closing land
    /// together or not at all, and of two of them sent at once the second finds
    /// the ticket closed, as with <see cref="Close" />.
    /// </remarks>
    public async Task<(TicketCommentOutcome outcome, TicketComment? comment)> AddComment(
        CreateTicketCommentEntity comment, TicketStatus statusAfter, int maxComments,
        CancellationToken ct = default)
    {
        var outcome = TicketCommentOutcome.Added;
        var now = _dateTimeProvider.Now;
        var closes = statusAfter is TicketStatus.Closed or TicketStatus.Spam;

        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            outcome = TicketCommentOutcome.Added;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            var moved = await _dbContext.Tickets
                .Where(t => t.TicketId == comment.TicketId)
                .Where(t => t.Status == TicketStatus.WaitingForModeration || t.Status == TicketStatus.WaitingForUser)
                .ExecuteUpdateAsync(set =>
                {
                    set.SetProperty(t => t.Status, statusAfter);
                    set.SetProperty(t => t.UpdatedUtc, now);
                    if (closes)
                    {
                        set.SetProperty(t => t.ResolvedById, comment.AuthorId);
                        set.SetProperty(t => t.ResolvedUtc, now);
                    }
                }, cancellation);
            if (moved == 0)
            {
                outcome = TicketCommentOutcome.Closed;
                await transaction.RollbackAsync(cancellation);
                return;
            }

            var count = await _dbContext.TicketComments.CountAsync(c => c.TicketId == comment.TicketId, cancellation);
            if (count >= maxComments)
            {
                outcome = TicketCommentOutcome.Full;
                await transaction.RollbackAsync(cancellation);
                return;
            }

            _dbContext.TicketComments.Add(new DbTicketComment
            {
                TicketCommentId = comment.TicketCommentId,
                TicketId = comment.TicketId,
                AuthorId = comment.AuthorId,
                Text = comment.Text,
                CreatedUtc = comment.CreatedUtc
            });
            await _dbContext.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
        }, ct);

        if (outcome != TicketCommentOutcome.Added)
        {
            return (outcome, null);
        }

        var written = await _dbContext.TicketComments
            .Where(c => c.TicketCommentId == comment.TicketCommentId)
            .ProjectToTicketComment()
            .FirstAsync(ct);
        return (outcome, written);
    }

    /// <inheritdoc />
    /// <remarks>
    /// One UPDATE with the condition "still open" in its WHERE: of two closings
    /// sent together the second waits for the first, finds the ticket closed and
    /// touches nothing, and its moderator is refused instead of believing he
    /// closed it.
    /// </remarks>
    public async Task<bool> Close(Guid ticketId, TicketStatus status, Guid resolvedById, DateTimeOffset resolvedUtc,
        CancellationToken ct = default)
    {
        var closed = await _dbContext.Tickets
            .Where(t => t.TicketId == ticketId)
            .Where(t => t.Status == TicketStatus.WaitingForModeration || t.Status == TicketStatus.WaitingForUser)
            .ExecuteUpdateAsync(set => set
                .SetProperty(t => t.Status, status)
                .SetProperty(t => t.ResolvedById, resolvedById)
                .SetProperty(t => t.ResolvedUtc, resolvedUtc)
                .SetProperty(t => t.UpdatedUtc, _dateTimeProvider.Now), ct);

        return closed > 0;
    }

    /// <inheritdoc />
    public async Task<Dictionary<TicketStatus, int>> GetTicketCounts(
        IReadOnlyCollection<TicketSubtype>? subtypes = null, Guid? exceptTargetUserId = null,
        CancellationToken ct = default)
    {
        return await Queue(subtypes, exceptTargetUserId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
    }

    /// <summary>
    /// The tickets of a queue: of the given categories, without those that name
    /// the reader as the violator. One formula for the list and its counters, so
    /// that a counter never counts a ticket the list does not show.
    /// </summary>
    private IQueryable<DbTicket> Queue(IReadOnlyCollection<TicketSubtype>? subtypes, Guid? exceptTargetUserId)
    {
        var tickets = _dbContext.Tickets.AsQueryable();

        if (subtypes != null)
        {
            tickets = tickets.Where(t => subtypes.Contains(t.Subtype));
        }

        if (exceptTargetUserId.HasValue)
        {
            tickets = tickets.Where(t => t.TargetId == null || t.TargetId != exceptTargetUserId.Value);
        }

        return tickets;
    }
}
