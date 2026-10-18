using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class BanRepository : IBanRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BanRepository(DmDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetUserBans(Guid userId, CancellationToken ct = default)
    {
        var bans = await _dbContext.Bans
            .Where(b => b.TargetUserId == userId)
            .OrderByDescending(b => b.StartedUtc)
            .ProjectToBan()
            .ToListAsync(ct);

        return await WithLifters(bans, ct);
    }

    /// <summary>
    /// Reads the moderators who lifted the bans of a page, in one read.
    /// </summary>
    /// <remarks>
    /// The row keeps the lifter's identifier with no navigation behind it, and a
    /// foreign key only to sign a lift was not worth a change of the schema. A
    /// removed account stays behind the soft-delete filter, so the lift is signed
    /// by nobody, the way a removed author is signed everywhere else.
    /// </remarks>
    private async Task<List<Ban>> WithLifters(List<Ban> bans, CancellationToken ct)
    {
        var lifterIds = bans
            .Where(b => b.LiftedByUserId.HasValue)
            .Select(b => b.LiftedByUserId!.Value)
            .Distinct()
            .ToList();
        if (lifterIds.Count == 0)
        {
            return bans;
        }

        var lifters = await _dbContext.Users
            .Where(u => lifterIds.Contains(u.UserId))
            .ProjectToGeneralUser()
            .ToDictionaryAsync(u => u.UserId, ct);

        foreach (var ban in bans)
        {
            ban.LiftedBy = ban.LiftedByUserId is { } lifterId &&
                           lifters.TryGetValue(lifterId, out var lifter)
                ? lifter
                : null;
        }

        return bans;
    }

    /// <inheritdoc />
    public async Task<Ban?> GetActiveBan(Guid userId, CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        return await _dbContext.Bans
            .Where(b => b.TargetUserId == userId && b.LiftedUtc == null && b.StartedUtc <= now && b.EndedUtc > now)
            .OrderByDescending(b => b.EndedUtc)
            .ProjectToBan()
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ban?> Get(Guid banId, CancellationToken ct = default)
    {
        return await _dbContext.Bans
            .Where(b => b.BanId == banId)
            .ProjectToBan()
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ban> Create(CreateBanEntity entity, CancellationToken ct = default)
    {
        var ban = new DbBan
        {
            BanId = entity.BanId,
            TargetUserId = entity.TargetUserId,
            AuthorId = entity.AuthorId,
            StartedUtc = entity.StartedUtc,
            EndedUtc = entity.EndedUtc,
            Comment = entity.Comment,
            AccessRestrictionPolicy = entity.AccessRestrictionPolicy,
            IsVoluntary = entity.IsVoluntary
        };

        _dbContext.Bans.Add(ban);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(ban.BanId, ct) ?? throw new InvalidOperationException("Failed to retrieve created ban");
    }

    /// <inheritdoc />
    public async Task Lift(Guid banId, Guid liftedByUserId, DateTimeOffset liftedUtc, string? reason,
        CancellationToken ct = default)
    {
        var ban = await _dbContext.Bans.FindAsync(new object[] { banId }, ct);
        if (ban != null)
        {
            // Lifting a ban used to leave no trace at all: the reason reached the
            // service and was dropped, so nobody could tell who had lifted what.
            // Then the trace was written and soft-deleted in the same call, which
            // hid the row from every query that could have read it back.
            ban.LiftedByUserId = liftedByUserId;
            ban.LiftedUtc = liftedUtc;
            ban.LiftReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ban>> GetAllActiveBans(CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        return await _dbContext.Bans
            .Where(b => b.LiftedUtc == null && b.StartedUtc <= now && b.EndedUtc > now)
            .OrderByDescending(b => b.StartedUtc)
            .ProjectToBan()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The entries are two selections of the same table put together in the
    /// database (<see cref="Log" />), so the order and the page are the
    /// database's, and a page costs a read of its moments, two of the moments
    /// around them for the numbers, and one of the bans on it, however long the
    /// list.
    /// </remarks>
    public async Task<(IReadOnlyList<BanLogEntry> Entries, int TotalCount)> GetBanHistory(
        Guid? targetUserId, int skip, int take, CancellationToken ct = default)
    {
        var entries = Log(Listed(targetUserId));
        var totalCount = await entries.CountAsync(ct);
        var page = await NewestFirst(entries)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        var numbers = await Numbers(page, ct);

        // The ban of an account removed since, or issued by one, keeps its entries
        // and is signed by nobody, as a removed author is everywhere. The issuer
        // is a required navigation, joined to an account row the filter of
        // removed accounts hides, so the bans are read with that filter lifted:
        // the count and the numbers above count every entry, and a page holds
        // every entry they count, or it comes up short and a number leads to an
        // entry that is not on it.
        var banIds = page.Select(e => e.BanId).Distinct().ToList();
        var pageBans = await _dbContext.Bans
            .KeepingRemovedAuthors()
            .Where(b => banIds.Contains(b.BanId))
            .ProjectToBan()
            .ToListAsync(ct);
        var byId = (await WithLifters(pageBans, ct)).ToDictionary(b => b.BanId);

        // Bans are lifted and never deleted, so every entry finds its ban; one
        // that went between the two reads is left off the page rather than
        // failing it.
        return (page
            .Where(e => byId.ContainsKey(e.BanId))
            .Select(e => new BanLogEntry
            {
                Number = numbers[(e.BanId, e.Kind)],
                Kind = e.Kind,
                OccurredUtc = e.OccurredUtc,
                Ban = byId[e.BanId]
            })
            .ToList(), totalCount);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The entry is the one the whole list counts to by its number, oldest first.
    /// Above it in the list stand the entries of later moments and, at its own
    /// moment, the ones the list puts first: the lifts over the issues, and among
    /// entries of one kind those of a lesser identifier. The last group is read
    /// in the order the database gives it rather than compared here, so the
    /// position is counted by the very order the page is cut by.
    /// </remarks>
    public async Task<int?> GetBanHistoryPosition(Guid? targetUserId, int number, CancellationToken ct = default)
    {
        if (number < 1)
        {
            return null;
        }

        var entry = await OldestFirst(Log(_dbContext.Bans))
            .Skip(number - 1)
            .FirstOrDefaultAsync(ct);
        if (entry == null)
        {
            return null;
        }

        if (targetUserId is { } target &&
            !await _dbContext.Bans.AnyAsync(b => b.BanId == entry.BanId && b.TargetUserId == target, ct))
        {
            return null;
        }

        var entries = Log(Listed(targetUserId));
        var above = await entries.CountAsync(e =>
            e.OccurredUtc > entry.OccurredUtc ||
            e.OccurredUtc == entry.OccurredUtc && e.Kind > entry.Kind, ct);
        var sameMoment = await entries
            .Where(e => e.OccurredUtc == entry.OccurredUtc && e.Kind == entry.Kind)
            .OrderBy(e => e.BanId)
            .Select(e => e.BanId)
            .ToListAsync(ct);
        return above + sameMoment.IndexOf(entry.BanId);
    }

    /// <summary>
    /// The numbers of the entries of a page, keyed by the ban and the kind.
    /// </summary>
    /// <remarks>
    /// An entry's number is one more than the count of the entries of the whole
    /// list that happened before it: every entry earlier than the earliest moment
    /// of the page, and then the entries between the page's earliest and latest
    /// moments, oldest first. The second read is the page itself for the list of
    /// everybody, and for the list of one person the entries of everybody
    /// written between the first and the last of his.
    /// </remarks>
    private async Task<Dictionary<(Guid BanId, BanLogEntryKind Kind), int>> Numbers(
        IReadOnlyCollection<LogEntry> page, CancellationToken ct)
    {
        if (page.Count == 0)
        {
            return [];
        }

        var earliest = page.Min(e => e.OccurredUtc);
        var latest = page.Max(e => e.OccurredUtc);
        var log = Log(_dbContext.Bans);
        var earlier = await log.CountAsync(e => e.OccurredUtc < earliest, ct);
        var between = await OldestFirst(log.Where(e => e.OccurredUtc >= earliest && e.OccurredUtc <= latest))
            .Select(e => new { e.BanId, e.Kind })
            .ToListAsync(ct);

        return between
            .Select((e, index) => (e.BanId, e.Kind, Number: earlier + index + 1))
            .ToDictionary(e => (e.BanId, e.Kind), e => e.Number);
    }

    /// <summary>
    /// The bans of one person, or of everybody.
    /// </summary>
    private IQueryable<DbBan> Listed(Guid? targetUserId) => targetUserId is { } target
        ? _dbContext.Bans.Where(b => b.TargetUserId == target)
        : _dbContext.Bans;

    /// <summary>
    /// Every issue and every lift of the given bans, an entry each at the moment
    /// it happened: two selections of the table put together in the database.
    /// </summary>
    /// <remarks>
    /// Both selections project the same shape, a class set member by member: the
    /// list filters and orders on its members, and a member of a positional
    /// record is not translated. The bans are narrowed before the projection.
    /// </remarks>
    private static IQueryable<LogEntry> Log(IQueryable<DbBan> bans) => bans
        .Select(b => new LogEntry { BanId = b.BanId, Kind = BanLogEntryKind.Issued, OccurredUtc = b.StartedUtc })
        .Concat(bans
            .Where(b => b.LiftedUtc != null)
            .Select(b => new LogEntry
            {
                BanId = b.BanId,
                Kind = BanLogEntryKind.Lifted,
                OccurredUtc = b.LiftedUtc!.Value
            }));

    /// <summary>
    /// The order of the list. At the same moment the lift goes first: newest
    /// first, and a lift is the later word on a ban. The identifier last, so
    /// that entries of one moment keep their places from one page to the next.
    /// </summary>
    private static IOrderedQueryable<LogEntry> NewestFirst(IQueryable<LogEntry> entries) => entries
        .OrderByDescending(e => e.OccurredUtc)
        .ThenByDescending(e => e.Kind)
        .ThenBy(e => e.BanId);

    /// <summary>
    /// The order the numbers count in: the list's own, reversed.
    /// </summary>
    private static IOrderedQueryable<LogEntry> OldestFirst(IQueryable<LogEntry> entries) => entries
        .OrderBy(e => e.OccurredUtc)
        .ThenBy(e => e.Kind)
        .ThenByDescending(e => e.BanId);

    /// <summary>
    /// An entry of the list as the database lists it: whose ban, what happened,
    /// when.
    /// </summary>
    private sealed class LogEntry
    {
        public Guid BanId { get; set; }

        public BanLogEntryKind Kind { get; set; }

        public DateTimeOffset OccurredUtc { get; set; }
    }
}
