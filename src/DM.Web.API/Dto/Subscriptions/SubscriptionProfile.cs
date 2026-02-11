using AutoMapper;
using DM.Services.Community.BusinessProcesses.Subscriptions;

namespace DM.Web.API.Dto.Subscriptions;

/// <inheritdoc />
internal class SubscriptionProfile : Profile
{
    /// <inheritdoc />
    public SubscriptionProfile()
    {
        CreateMap<SubscriptionDto, Subscription>();
    }
}
