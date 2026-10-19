using System.Net;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// Whether the current viewer reads places of one kind, by the read rule the
/// place itself answers its own reads with
/// </summary>
/// <remarks>
/// One implementation per <see cref="ReadablePlaceKind" />, in the module that owns
/// the place. A place the viewer may not read and one that is not there answer
/// alike: both are left out, so a refusal reads as an absence.
/// </remarks>
public interface IPlaceReading
{
    /// <summary>
    /// The kind of place this reading answers for
    /// </summary>
    ReadablePlaceKind Kind { get; }

    /// <summary>
    /// Which of the places the current viewer reads
    /// </summary>
    /// <param name="placeIds">Identifiers of places of <see cref="Kind" /></param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlySet<Guid>> ReadableAsync(IReadOnlyCollection<Guid> placeIds, CancellationToken ct = default);
}

/// <summary>
/// A kind of place whose reading is asked of the module that owns it
/// </summary>
public enum ReadablePlaceKind
{
    /// <summary>
    /// A game: its discussion and its reviews
    /// </summary>
    Game = 1,

    /// <summary>
    /// A blog: its discussion
    /// </summary>
    Blog = 2,

    /// <summary>
    /// A publication of a blog: its discussion
    /// </summary>
    Publication = 3,

    /// <summary>
    /// A post of a game room: the reviews of it
    /// </summary>
    Post = 4
}

/// <summary>
/// A place to ask the viewer's reading of
/// </summary>
/// <param name="Kind">The kind of the place</param>
/// <param name="Id">The identifier of the place</param>
public sealed record ReadablePlace(ReadablePlaceKind Kind, Guid Id);

/// <summary>
/// The answer of a reading made by the place's own read
/// </summary>
public static class PlaceReading
{
    /// <summary>
    /// The places whose own read lets the viewer through: one refused, absent or
    /// gone is left out.
    /// </summary>
    /// <param name="placeIds">Identifiers of the places</param>
    /// <param name="read">The read of one place, gated as its own reads are, on the token it is handed</param>
    /// <param name="ct">Cancellation token, handed to every read</param>
    public static async Task<IReadOnlySet<Guid>> ThroughOwnRead(
        IEnumerable<Guid> placeIds, Func<Guid, CancellationToken, Task> read, CancellationToken ct)
    {
        var readable = new HashSet<Guid>();
        foreach (var placeId in placeIds.Distinct())
        {
            try
            {
                await read(placeId, ct);
                readable.Add(placeId);
            }
            catch (HttpException refusal) when (
                HiddenContainerRefusal.IsAbsence(refusal) ||
                refusal.StatusCode == HttpStatusCode.Forbidden)
            {
                // Left out: refused and absent read alike.
            }
        }

        return readable;
    }
}
