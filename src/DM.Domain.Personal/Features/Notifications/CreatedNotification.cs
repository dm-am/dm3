namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// One notification as it came out of creation: the request with its audience
/// narrowed to who may actually receive it, and the row that was stored for them.
/// </summary>
/// <remarks>
/// The pair exists because the two halves are needed by different channels and
/// neither half carries the other's part. The realtime push needs the row, for
/// the identifier the store assigned it. Mail and the bots need the request, for
/// the flag that says a notification is realtime-only and must not be written to
/// anybody.
///
/// Before the pair the dispatcher took the row for the push and the original
/// request for mail and bots. That was invisible while nothing narrowed the
/// audience, and became a hole the moment something did: the blacklist filter
/// removed a recipient from the row, and the letter went out to the audience
/// nobody had filtered.
/// </remarks>
/// <param name="Source">The request, with <see cref="CreateNotification.UsersInterested"/> already filtered.</param>
/// <param name="Entity">The stored row, or the unstored one a realtime-only notification produces.</param>
public sealed record CreatedNotification(
    CreateNotification Source,
    CreateNotificationEntity Entity);
