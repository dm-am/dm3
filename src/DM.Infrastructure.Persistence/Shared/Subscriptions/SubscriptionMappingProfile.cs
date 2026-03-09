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
        CreateMap<SubscriptionEntity, Subscription>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.SubscriptionId));
    }
}
