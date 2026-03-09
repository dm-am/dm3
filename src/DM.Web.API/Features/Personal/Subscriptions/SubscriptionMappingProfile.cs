using AutoMapper;
using DM.Domain.Core.Subscriptions;

namespace DM.Web.API.Features.Personal.Subscriptions;

/// <inheritdoc />
internal class SubscriptionMappingProfile : Profile
{
    /// <inheritdoc />
    public SubscriptionMappingProfile()
    {
        CreateMap<DM.Domain.Core.Subscriptions.Subscription, Subscription>();
    }
}
