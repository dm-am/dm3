using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Subscriptions;

/// <summary>
/// Request to subscribe to a target
/// </summary>
public class SubscribeRequest
{
    /// <summary>
    /// Optional notification settings. If not provided, default settings for the target type will be used.
    /// </summary>
    public SubscriptionSettings? Settings { get; set; }
}
