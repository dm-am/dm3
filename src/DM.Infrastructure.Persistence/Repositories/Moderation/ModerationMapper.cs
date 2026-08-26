using System;
using System.Linq;
using DM.Domain.Moderation.Features.ProfileNotes;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Features.Warnings;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DbTicket = DM.Infrastructure.Persistence.Entities.Moderation.Ticket;
using DbModeratedProfileNote = DM.Infrastructure.Persistence.Entities.Account.ModeratedProfileNote;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <summary>
/// Projection formulas for the moderation DTOs. Written out rather than
/// generated: every shape here either carries users through the shared
/// <see cref="GeneralUserProjections"/> formula or flattens usernames off
/// nullable navigations, and both are things Mapperly cannot inline into a
/// queryable.
/// </summary>
internal static class ModerationMapper
{
    /// <summary>
    /// EF projection to the domain warning. The address and the "edited
    /// since" mark are not columns - both are resolved per read by
    /// IWarningEntityResolver, because they change without the warning
    /// being touched. Only the snapshot is stored.
    /// </summary>
    public static IQueryable<Warning> ProjectToWarning(this IQueryable<DbWarning> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbWarning, Warning>>(w => new Warning
        {
            WarningId = w.WarningId,
            EntityId = w.EntityId,
            EntityType = w.EntityType,
            CreatedUtc = w.CreatedUtc,
            Text = w.Text,
            Points = w.Points,
            IsRemoved = w.IsRemoved,
            EntitySnapshot = w.EntitySnapshot,
            TargetUser = GeneralUserProjections.Projection.Splice(w.TargetUser),
            Author = GeneralUserProjections.Projection.Splice(w.Author)
        }));

    /// <summary>
    /// EF projection to the domain ban
    /// </summary>
    public static IQueryable<Ban> ProjectToBan(this IQueryable<DbBan> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbBan, Ban>>(b => new Ban
        {
            BanId = b.BanId,
            TargetUserId = b.TargetUserId,
            StartedUtc = b.StartedUtc,
            EndedUtc = b.EndedUtc,
            Comment = b.Comment,
            AccessRestrictionPolicy = b.AccessRestrictionPolicy,
            IsVoluntary = b.IsVoluntary,
            LiftedUtc = b.LiftedUtc,
            LiftedByUserId = b.LiftedByUserId,
            LiftReason = b.LiftReason,
            TargetUser = GeneralUserProjections.Projection.Splice(b.TargetUser),
            Author = GeneralUserProjections.Projection.Splice(b.Author)
        }));

    /// <summary>
    /// EF projection to the domain ticket (list shape - no thread). The
    /// member list repeats in the details projection below on purpose: the
    /// navigations must be dereferenced inside the Select for EF to join
    /// them, so the two shapes cannot share a compiled formula.
    /// </summary>
    public static IQueryable<Ticket> ProjectToTicket(this IQueryable<DbTicket> query) =>
        query.Select(t => new Ticket
        {
            TicketId = t.TicketId,
            ReporterUserId = t.UserId,
            ReporterUsername = t.Author != null ? t.Author.Username : null,
            TargetUsername = t.Target != null ? t.Target.Username : null,
            GuestEmail = t.GuestEmail,
            TrackingToken = t.TrackingToken,
            EntityId = t.EntityId,
            EntityType = t.EntityType,
            Status = t.Status,
            Subtype = t.Subtype,
            CreatedUtc = t.CreatedUtc,
            Description = t.Description,
            Comment = t.Comment,
            AssignedModeratorUsername = t.AssignedModerator != null ? t.AssignedModerator.Username : null,
            ResolvedUtc = t.ResolvedUtc,
            Answer = t.Answer,
            HasWarning = t.WarningId.HasValue,
            HasBan = t.BanId.HasValue
        });

    /// <summary>
    /// EF projection to the ticket details - the base shape plus the
    /// conversation thread, oldest response first
    /// </summary>
    public static IQueryable<TicketDetails> ProjectToTicketDetails(this IQueryable<DbTicket> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbTicket, TicketDetails>>(t => new TicketDetails
        {
            TicketId = t.TicketId,
            ReporterUserId = t.UserId,
            ReporterUsername = t.Author != null ? t.Author.Username : null,
            TargetUsername = t.Target != null ? t.Target.Username : null,
            GuestEmail = t.GuestEmail,
            TrackingToken = t.TrackingToken,
            EntityId = t.EntityId,
            EntityType = t.EntityType,
            Status = t.Status,
            Subtype = t.Subtype,
            CreatedUtc = t.CreatedUtc,
            Description = t.Description,
            Comment = t.Comment,
            AssignedModeratorUsername = t.AssignedModerator != null ? t.AssignedModerator.Username : null,
            ResolvedUtc = t.ResolvedUtc,
            Answer = t.Answer,
            HasWarning = t.WarningId.HasValue,
            HasBan = t.BanId.HasValue,
            Responses = t.Responses
                .OrderBy(r => r.CreatedUtc)
                .Select(r => new TicketResponseItem
                {
                    Author = GeneralUserProjections.Projection.Splice(r.Author),
                    Text = r.Text,
                    CreatedUtc = r.CreatedUtc,
                    IsFromModerator = r.IsFromModerator
                })
                .ToList()
        }));

    /// <summary>
    /// EF projection to the moderator's profile note
    /// </summary>
    public static IQueryable<ModeratedProfileNote> ProjectToModeratedProfileNote(
        this IQueryable<DbModeratedProfileNote> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbModeratedProfileNote, ModeratedProfileNote>>(
            n => new ModeratedProfileNote
            {
                Id = n.ModeratedProfileNoteId,
                Text = n.Text,
                CreatedUtc = n.CreatedUtc,
                ModifiedUtc = n.ModifiedUtc,
                User = GeneralUserProjections.Projection.Splice(n.User),
                Author = GeneralUserProjections.Projection.Splice(n.Author)
            }));

}
