using System;
using System.Linq;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DM.Infrastructure.Persistence.Repositories.Search;

/// <summary>
/// The bodies the search reads by their visible text, and the projection of that
/// text beside each of them before every write.
/// </summary>
internal static class SearchableBodies
{
    /// <summary>
    /// The bodies whose visible text is projected beside them, and where each one
    /// keeps that body.
    /// </summary>
    /// <remarks>
    /// A table rather than an interface on the entities: what makes a property the
    /// searchable body of a row is what it means, and the surface it is displayed
    /// on is a fact about the feature rather than about the type. The surface
    /// decides which of the other tags are markup ([mod] is on some and text on
    /// others), so a projection made on the wrong one reads as markup a tag the
    /// page shows as text, or the other way round. [private] is a node on every
    /// surface, and the plain text the projection is made of drops it on every
    /// one.
    /// </remarks>
    private static readonly (Type Entity, string Body, BbSurface Surface)[] ProjectedBodies =
    [
        (typeof(Message), nameof(Message.Text), BbSurface.GlobalChatMessage),
        (typeof(Post), nameof(Post.GameText), BbSurface.GamePost),
        (typeof(Topic), nameof(Topic.Text), BbSurface.Comment),
        (typeof(Comment), nameof(Comment.Text), BbSurface.Comment),
    ];

    /// <summary>
    /// Refreshes the projected text of everything about to be written.
    /// </summary>
    /// <remarks>
    /// Called by SaveChanges of the context rather than by an interceptor, because
    /// the context is constructed in five places - three hosts and two test
    /// factories - and an interceptor forgotten in one of them leaves that path
    /// writing rows whose projection is stale or empty, which nothing fails on and
    /// which shows up as a body that cannot be found by its own words.
    /// </remarks>
    internal static void Refresh(ChangeTracker tracker)
    {
        foreach (var (type, body, surface) in ProjectedBodies)
        {
            foreach (var entry in tracker.Entries().Where(e => e.Entity.GetType() == type))
            {
                if (entry.State != EntityState.Added &&
                    !(entry.State == EntityState.Modified && entry.Property(body).IsModified))
                {
                    continue;
                }

                entry.Property("SearchText").CurrentValue =
                    SearchTextProjection.Of(entry.Property(body).CurrentValue as string, surface);
            }
        }
    }
}
