using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Subscriptions;

/// <summary>
/// Request to update subscription settings
/// </summary>
public class UpdateSubscriptionRequest
{
    /// <summary>
    /// New notification settings
    /// </summary>
    public SubscriptionSettings Settings { get; set; }
}
