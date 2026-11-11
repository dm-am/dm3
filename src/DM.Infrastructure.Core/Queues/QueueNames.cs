namespace DM.Infrastructure.Core.Queues;

/// <summary>
/// The names both ends of the queue read: the producers that store outbox rows,
/// the workers that drain them, the metrics that label them and the listener of
/// pushes.
/// </summary>
/// <remarks>
/// One home, because a typo on one end leaves rows that nobody drains. The queue
/// names are the ones the broker queues had, so the metric labels do not change.
/// The push channel is a plain lower-case identifier, so LISTEN needs no quoting.
/// </remarks>
public static class QueueNames
{
    /// <summary>
    /// The queue of events, drained by the notification worker.
    /// </summary>
    public const string Notifications = "dm.notifications";

    /// <summary>
    /// The queue of letters, drained by the mail worker.
    /// </summary>
    public const string Mail = "dm.mail.sending";

    /// <summary>
    /// The database notification channel pushes travel on to the listener of
    /// every API instance.
    /// </summary>
    public const string PushChannel = "dm_notifications_sent";
}
