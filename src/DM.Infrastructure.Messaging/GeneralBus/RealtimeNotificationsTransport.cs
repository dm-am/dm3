namespace DM.Infrastructure.Messaging.GeneralBus;

/// <summary>
/// Information about the realtime notification transport
/// </summary>
public static class RealtimeNotificationsTransport
{
    /// <summary>
    /// MQ exchange the notification dispatcher publishes to and the API consumes
    /// </summary>
    public const string ExchangeName = "dm.notifications.sent";
}
