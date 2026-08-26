using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Shared.Subscriptions;

/// <summary>
/// What a list of blogs or games needs to say about its subscribers: how many
/// there are, whether the viewer is one of them, and a capped preview of names.
/// </summary>
/// <param name="Count">
/// Distinct subscribers, not subscription rows. The pair is unique in the schema
/// now, so the two forms agree; the distinct one stays because it is what the
/// column means, and because the count has to keep meaning that if the rows ever
/// arrive from an import rather than from the subscribe path.
/// </param>
/// <param name="ViewerSubscribed">The viewer is among them</param>
/// <param name="Preview">
/// Up to <see cref="SubscriptionPolicy.PreviewCap" /> names, most recently
/// active first
/// </param>
internal sealed record SubscriberSummary(int Count, bool ViewerSubscribed, List<string> Preview);

/// <summary>
/// One row of the summary statement, keyed by the target it describes.
/// </summary>
internal sealed class SubscriberSummaryRow
{
    public Guid TargetId { get; set; }
    public int Count { get; set; }
    public bool ViewerSubscribed { get; set; }
    public List<string> Preview { get; set; } = [];
}

/// <summary>
/// The one statement behind <see cref="SubscriberSummary" />.
/// </summary>
/// <remarks>
/// The three fields are everything the consumers ever asked the subscriber list
/// for: two counts and a Contains(viewerId). Loading the ids to answer them read
/// every subscription row of every entity on the page.
///
/// EF and Npgsql translate this to a single SELECT: a GROUP BY for the
/// aggregates, LEFT JOIN'ed to a ROW_NUMBER() OVER (PARTITION BY TargetId)
/// subquery for the preview (the window form rather than LATERAL because the
/// inner order is by a column of the joined Users row). The viewer flag becomes
/// an EXISTS over the same (TargetType, TargetId) index with an equality on
/// SubscriberId - no join, so unlike a conditional count over the navigation it
/// does not re-scan Users per group.
///
/// Blogs and games asked the same question with two copies of this text, each
/// carrying a comment pointing at the other. Whoever answers it next names a
/// target type instead.
/// </remarks>
internal static class SubscriberSummaries
{
    public static async Task<Dictionary<Guid, SubscriberSummary>> ByTarget(
        IQueryable<Subscription> subscriptions,
        SubscriptionTargetType targetType,
        HashSet<Guid> targetIds,
        Guid viewerId,
        CancellationToken ct) =>
        (await Query(subscriptions, targetType, targetIds, viewerId).ToListAsync(ct))
        .ToDictionary(
            x => x.TargetId,
            x => new SubscriberSummary(x.Count, x.ViewerSubscribed, x.Preview));

    /// <summary>
    /// The statement itself, apart from the dictionary it is read into: a shape
    /// this narrow is worth being able to read the SQL of.
    /// </summary>
    internal static IQueryable<SubscriberSummaryRow> Query(
        IQueryable<Subscription> subscriptions,
        SubscriptionTargetType targetType,
        HashSet<Guid> targetIds,
        Guid viewerId) =>
        subscriptions
            // EF.Constant, not the bare variable: the two copies this replaced
            // named the type inline, so the discriminator reached Postgres as a
            // literal and the planner could use the column statistics for that
            // one value. A captured argument would arrive as a parameter and
            // quietly change how the statement is planned.
            .Where(s => s.TargetType == EF.Constant(targetType) && targetIds.Contains(s.TargetId))
            .GroupBy(s => s.TargetId)
            .Select(g => new SubscriberSummaryRow
            {
                TargetId = g.Key,
                Count = g.Select(s => s.SubscriberId).Distinct().Count(),
                ViewerSubscribed = g.Any(s => s.SubscriberId == viewerId),
                // Subscribers who have never been active must sort last, and a
                // plain DESC in Postgres puts nulls first.
                Preview = g.OrderByDescending(s => s.Subscriber.LastActivityUtc != null)
                    .ThenByDescending(s => s.Subscriber.LastActivityUtc)
                    .ThenBy(s => s.SubscriptionId)
                    .Take(SubscriptionPolicy.PreviewCap)
                    .Select(s => s.Subscriber.Username)
                    .ToList(),
            });
}
