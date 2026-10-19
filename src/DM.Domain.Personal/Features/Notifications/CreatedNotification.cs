namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// One notification as it came out of creation: the request with its audience
/// narrowed to who may actually receive it, and the row that was stored for them.
/// </summary>
/// <remarks>
/// The realtime push needs the row for its identifier, mail and the bots need the
/// request for the realtime-only flag; both carry the filtered audience.
/// </remarks>
/// <param name="Source">The request, with <see cref="CreateNotification.UsersInterested"/> already filtered.</param>
/// <param name="Entity">The stored row, or the unstored one a realtime-only notification produces.</param>
public sealed record CreatedNotification(
    CreateNotification Source,
    CreateNotificationEntity Entity);
