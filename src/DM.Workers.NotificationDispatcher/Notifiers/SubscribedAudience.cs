using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Subscriptions;

namespace DM.Workers.NotificationDispatcher.Notifiers;

/// <summary>
/// Who hears about a blog or a game its team has just opened to everybody.
/// </summary>
internal static class SubscribedAudience
{
    /// <summary>
    /// Everyone subscribed to the author or to one of the assistants under the
    /// given setting, minus the team itself.
    /// </summary>
    /// <remarks>
    /// The team is left out because the generators aimed at the team tell them,
    /// and a set is what the removal needs: a subscriber who follows both the
    /// author and an assistant is one recipient, and the same person may sit on
    /// both sides of the subtraction.
    /// </remarks>
    /// <param name="subscriptions">Subscription repository</param>
    /// <param name="authorId">Author or master of the entity</param>
    /// <param name="assistantIds">Assistants of the entity</param>
    /// <param name="requiredSettings">Setting a subscriber has to hold</param>
    /// <returns>Identifiers of the people to notify</returns>
    public static async Task<HashSet<Guid>> OfTeamAsync(
        ISubscriptionRepository subscriptions,
        Guid authorId,
        IReadOnlyCollection<Guid> assistantIds,
        SubscriptionSettings requiredSettings)
    {
        var usersInterested = new HashSet<Guid>();

        var authorSubscriptions = await subscriptions.GetByTargetWithSettingsAsync(
            SubscriptionTargetType.User,
            authorId,
            requiredSettings);
        usersInterested.UnionWith(authorSubscriptions.Select(s => s.SubscriberId));

        foreach (var assistantId in assistantIds)
        {
            var assistantSubscriptions = await subscriptions.GetByTargetWithSettingsAsync(
                SubscriptionTargetType.User,
                assistantId,
                requiredSettings);
            usersInterested.UnionWith(assistantSubscriptions.Select(s => s.SubscriberId));
        }

        usersInterested.Remove(authorId);
        foreach (var assistantId in assistantIds)
        {
            usersInterested.Remove(assistantId);
        }

        return usersInterested;
    }
}
