using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Administration;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Moderation.Tickets;

/// <inheritdoc />
internal class TicketRepository : ITicketRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public TicketRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetTickets(TicketStatus? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.Tickets
            .Include(t => t.Author)
            .Include(t => t.Target)
            .Include(t => t.AssignedModerator)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query
            .OrderByDescending(t => t.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetModeratorTickets(Guid moderatorId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Include(t => t.Author)
            .Include(t => t.Target)
            .Where(t => t.AssignedModeratorId == moderatorId)
            .OrderByDescending(t => t.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetUserTickets(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Include(t => t.Target)
            .Include(t => t.AnswerAuthor)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedUtc)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ticket?> Get(Guid ticketId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Include(t => t.Author)
            .Include(t => t.Target)
            .Include(t => t.AssignedModerator)
            .Include(t => t.AnswerAuthor)
            .Include(t => t.Warning)
            .Include(t => t.Ban)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> Create(Ticket ticket, CancellationToken ct = default)
    {
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync(ct);
        return ticket;
    }

    /// <inheritdoc />
    public async Task<Ticket> Update(Ticket ticket, CancellationToken ct = default)
    {
        _dbContext.Tickets.Update(ticket);
        await _dbContext.SaveChangesAsync(ct);
        return ticket;
    }

    /// <inheritdoc />
    public async Task<Dictionary<TicketStatus, int>> GetTicketCounts(CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
    }
}
