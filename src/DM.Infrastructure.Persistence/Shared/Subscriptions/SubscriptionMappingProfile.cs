using AutoMapper;
using DM.Domain.Core.Subscriptions;
using SubscriptionEntity = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;

namespace DM.Infrastructure.Persistence.Shared.Subscriptions;

/// <summary>
/// Mapping profile for subscriptions
/// </summary>
public class SubscriptionMappingProfile : Profile
{
    /// <summary>
    /// Configure subscription mappings
    /// </summary>
    public SubscriptionMappingProfile()
    {
        // The name of the target is not on the subscription row: it lives in one of
        // four tables the target type chooses between. The two reads that render it
        // resolve it in their own projection (SubscriptionRepository.WithTargetNames);
        // notification fan-out never shows a name, and joining four tables per
        // published event to fill a field nobody reads is what these ignores refuse.
        CreateMap<SubscriptionEntity, Subscription>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.SubscriptionId))
            .ForMember(d => d.TargetTitle, opt => opt.Ignore())
            .ForMember(d => d.TargetUsername, opt => opt.Ignore());
    }
}
