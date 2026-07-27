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
    public async Task<IEnumerable<Ticket>> GetTickets(TicketStatus? status = null,
        IReadOnlyCollection<TicketSubtype>? subtypes = null, CancellationToken ct = default)
    {
        var query = _dbContext.Tickets.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (subtypes != null)
        {
            query = query.Where(t => subtypes.Contains(t.Subtype));
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
    public async Task<IEnumerable<Ticket>> GetUserTickets(Guid userId,
        TicketStatus? status = null, TicketSubtype? subtype = null, CancellationToken ct = default)
    {
        var query = _dbContext.Tickets.Where(t => t.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (subtype.HasValue)
        {
            query = query.Where(t => t.Subtype == subtype.Value);
        }

        return await query
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
    public async Task<TicketDetails?> GetDetails(Guid ticketId, CancellationToken ct = default)
    {
        return await _dbContext.Tickets
            .Where(t => t.TicketId == ticketId)
            .ProjectTo<TicketDetails>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<TicketDetails?> GetByTrackingToken(string token, CancellationToken ct = default)
    {
        // The token is the credential; an empty token must never match a row
        // (authenticated tickets store a null token).
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        return await _dbContext.Tickets
            .Where(t => t.TrackingToken == token)
            .ProjectTo<TicketDetails>(_mapper.ConfigurationProvider)
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
    public async Task<Dictionary<TicketStatus, int>> GetTicketCounts(
        IReadOnlyCollection<TicketSubtype>? subtypes = null, CancellationToken ct = default)
    {
        var query = _dbContext.Tickets.AsQueryable();

        if (subtypes != null)
        {
            query = query.Where(t => subtypes.Contains(t.Subtype));
        }

        return await query
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
    }
}
