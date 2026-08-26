using System.Linq;
using DM.Domain.Core.Subscriptions;
using Riok.Mapperly.Abstractions;
using SubscriptionEntity = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;

namespace DM.Infrastructure.Persistence.Shared.Subscriptions;

/// <summary>
/// Compile-time mapper for subscriptions. The name of the target is not on
/// the subscription row: it lives in one of four tables the target type
/// chooses between. The two reads that render it resolve it in their own
/// projection (SubscriptionRepository.WithTargetNames); notification fan-out
/// never shows a name, and joining four tables per published event to fill a
/// field nobody reads is what the ignores refuse.
/// </summary>
[Mapper]
public static partial class SubscriptionMapper
{
    /// <summary>
    /// EF projection to the domain DTO
    /// </summary>
    public static partial IQueryable<Subscription> ProjectToSubscription(
        this IQueryable<SubscriptionEntity> query);

    /// <summary>
    /// Tracked entity to the domain DTO, for the write path that already
    /// holds the row
    /// </summary>
    [MapProperty(nameof(SubscriptionEntity.SubscriptionId), nameof(Subscription.Id))]
    [MapperIgnoreTarget(nameof(Subscription.TargetTitle))]
    [MapperIgnoreTarget(nameof(Subscription.TargetUsername))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial Subscription ToSubscription(this SubscriptionEntity subscription);
}
