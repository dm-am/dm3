using Riok.Mapperly.Abstractions;
using DomainSubscription = DM.Domain.Core.Subscriptions.Subscription;

namespace DM.Web.API.Features.Personal.Subscriptions;

/// <summary>
/// Compile-time mapper for subscriptions
/// </summary>
[Mapper]
internal partial class SubscriptionMapper
{
    /// <summary>
    /// Domain subscription to its response DTO. The subscriber id stays
    /// behind: the endpoint always answers about the current user.
    /// </summary>
    [MapperIgnoreSource(nameof(DomainSubscription.SubscriberId))]
    public partial Subscription ToSubscription(DomainSubscription subscription);
}
