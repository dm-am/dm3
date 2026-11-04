namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Why a guest could not follow the link to the text a warning was issued for
/// </summary>
/// <remarks>
/// Every closure takes away the link, which would lead a guest to a page he cannot
/// read, or to nothing. A place closed to a guest, or none found, takes away the
/// text and the title of the place as well (<see cref="Warning.LogShowsText" />).
/// A text gone from an open place keeps both: a warning is read for what it was
/// given for. A closed place outranks a deletion, so a deleted text of a closed
/// place stays closed.
/// </remarks>
public enum WarningLogClosure
{
    /// <summary>
    /// The object is not found, or its place is not one a warning addresses
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Deleted by somebody other than its author: moderation took it off the site
    /// </summary>
    RemovedByModeration = 2,

    /// <summary>
    /// The topic, the game, the blog or the publication it stood in is deleted
    /// </summary>
    PlaceRemoved = 3,

    /// <summary>
    /// The game or the blog is not visible to a guest (a draft without the open
    /// preview, or premoderation), or the room of the rated post is closed
    /// </summary>
    PlaceNotPublic = 4
}
