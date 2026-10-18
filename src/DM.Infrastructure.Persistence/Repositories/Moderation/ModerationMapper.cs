using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.ProfileNotes;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Features.Warnings;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DbTicket = DM.Infrastructure.Persistence.Entities.Moderation.Ticket;
using DbTicketComment = DM.Infrastructure.Persistence.Entities.Moderation.TicketComment;
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
    /// EF projection to the domain warning. The address and the place are not
    /// columns - both are resolved per read by IWarningEntityResolver, because
    /// they change without the warning being touched. Only the snapshot is
    /// stored.
    /// </summary>
    /// <remarks>
    /// The profile of a warning on a profile comes without its picture: the
    /// pinned avatar is usually a deleted upload, behind the filter every
    /// navigation to an upload goes through, and WarningRepository reads it
    /// past the filter for the rows of the page. Nothing of the profile is given
    /// for a removed account, as nothing of a removed account is shown anywhere.
    /// </remarks>
    public static IQueryable<Warning> ProjectToWarning(this IQueryable<DbWarning> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbWarning, Warning>>(w => new Warning
        {
            WarningId = w.WarningId,
            EntityId = w.EntityId,
            EntityType = w.EntityType,
            CreatedUtc = w.CreatedUtc,
            Text = w.Text,
            Points = w.Points,
            OriginalPoints = w.OriginalPoints,
            // The name alone, not the whole user: the second signature is a line
            // of text under the warning wherever it is shown, and the open log
            // shows it to strangers.
            PointsChangedByUsername = w.PointsChangedBy == null ? null : w.PointsChangedBy.Username,
            PointsChangedUtc = w.PointsChangedUtc,
            PointsChangeText = w.PointsChangeText,
            EntitySnapshot = w.EntitySnapshot,
            Profile = w.EntityType == WarningEntityType.Profile && !w.TargetUser.IsRemoved
                ? new WarningProfile { Status = w.ProfileStatusSnapshot, Info = w.EntitySnapshot }
                : null,
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
    /// EF projection to the domain ticket: the one shape every ticket read
    /// takes, the lists, the ticket page and the guest tracking page alike
    /// </summary>
    /// <remarks>
    /// The reporter is a whole user, for the [online] mark of the signature, and
    /// null both for a guest and for an account removed since: IsGuest, read off
    /// the column and not off the navigation, tells the two apart. The closing
    /// moderator is a name, the one thing the closing line says of him.
    /// </remarks>
    public static IQueryable<Ticket> ProjectToTicket(this IQueryable<DbTicket> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbTicket, Ticket>>(t => new Ticket
        {
            TicketId = t.TicketId,
            ReporterUserId = t.UserId,
            Reporter = GeneralUserProjections.Projection.Splice(t.Author),
            IsGuest = t.UserId == null,
            TargetUserId = t.TargetId,
            GuestEmail = t.GuestEmail,
            TrackingToken = t.TrackingToken,
            EntityId = t.EntityId,
            EntityType = t.EntityType,
            Status = t.Status,
            Subtype = t.Subtype,
            CreatedUtc = t.CreatedUtc,
            Description = t.Description,
            Comment = t.Comment,
            CommentsCount = t.TicketComments.Count(),
            ResolvedUtc = t.ResolvedUtc,
            ResolvedByUsername = t.ResolvedBy != null ? t.ResolvedBy.Username : null
        }));

    /// <summary>
    /// EF projection to a comment of the conversation of a ticket. The author is
    /// null for the guest and for a removed account alike; IsGuest is read off
    /// the column, as on the ticket
    /// </summary>
    public static IQueryable<TicketComment> ProjectToTicketComment(this IQueryable<DbTicketComment> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbTicketComment, TicketComment>>(c => new TicketComment
        {
            TicketCommentId = c.TicketCommentId,
            TicketId = c.TicketId,
            Author = GeneralUserProjections.Projection.Splice(c.Author),
            IsGuest = c.AuthorId == null,
            CreatedUtc = c.CreatedUtc,
            Text = c.Text
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
