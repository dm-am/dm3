using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using DbTicket = DM.Infrastructure.Persistence.Entities.Moderation.Ticket;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class TicketRepository : ITicketRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public TicketRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetTickets(TicketStatus? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.Tickets.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query
            .OrderByDescending(t => t.CreatedUtc)
            .ProjectTo<Ticket>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetModeratorTickets(Guid moderatorId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Where(t => t.AssignedModeratorId == moderatorId)
            .OrderByDescending(t => t.CreatedUtc)
            .ProjectTo<Ticket>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Ticket>> GetUserTickets(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedUtc)
            .ProjectTo<Ticket>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ticket?> Get(Guid ticketId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Where(t => t.TicketId == ticketId)
            .ProjectTo<Ticket>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Ticket> Create(CreateTicketEntity entity, CancellationToken ct = default)
    {
        var ticket = new DbTicket
        {
            TicketId = entity.TicketId,
            UserId = entity.ReporterUserId,
            TargetId = entity.TargetUserId,
            EntityId = entity.EntityId,
            EntityType = entity.EntityType ?? "",
            Status = entity.Status,
            CreatedUtc = entity.CreatedUtc,
            Description = entity.Description,
            Comment = entity.Comment
        };

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(ticket.TicketId, ct) ?? throw new InvalidOperationException("Failed to retrieve created ticket");
    }

    /// <inheritdoc />
    public async Task<Ticket> Update(UpdateTicketEntity entity, CancellationToken ct = default)
    {
        var ticket = await _dbContext.Tickets.FindAsync(new object[] { entity.TicketId }, ct);
        if (ticket == null)
        {
            throw new InvalidOperationException("Ticket not found");
        }

        if (entity.AssignedModeratorId.HasValue)
        {
            ticket.AssignedModeratorId = entity.AssignedModeratorId;
        }

        if (entity.Status.HasValue)
        {
            ticket.Status = entity.Status.Value;
        }

        if (entity.ResolvedUtc.HasValue)
        {
            ticket.ResolvedUtc = entity.ResolvedUtc;
        }

        if (entity.Answer != null)
        {
            ticket.Answer = entity.Answer;
        }

        if (entity.WarningId.HasValue)
        {
            ticket.WarningId = entity.WarningId;
        }

        if (entity.BanId.HasValue)
        {
            ticket.BanId = entity.BanId;
        }

        ticket.UpdatedUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await Get(ticket.TicketId, ct) ?? throw new InvalidOperationException("Failed to retrieve updated ticket");
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
