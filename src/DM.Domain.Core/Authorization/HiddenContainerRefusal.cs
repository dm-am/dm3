using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// How a thing read by its own identifier answers when the game or the blog it
/// lives in is not there to the reader.
/// </summary>
/// <remarks>
/// A character, a review or a notepad entry is found by its id before the module
/// around it is asked, and the module answers in its own words: the game or the
/// blog is not found. Passed on as they are, those words tell a stranger that the
/// id is real and what hides it, next to the plain "not found" an id that is
/// nothing gets. So a module that is not there to the reader answers exactly as
/// the thing itself not being there, by the thing's own sentence. A refusal of
/// another kind - the reader sees the module and may not do this - is not about
/// existence and passes unchanged. Comments have the same rule in
/// <see cref="Comments.DiscussionRefusal" />.
/// </remarks>
public static class HiddenContainerRefusal
{
    /// <summary>
    /// Read the module the thing lives in, answering with the thing's own
    /// "not found" where the module is not found or gone.
    /// </summary>
    /// <param name="notFound">The sentence an identifier that is nothing gets</param>
    /// <param name="readContainer">The read of the module, gated as its own reads are</param>
    public static async Task<T> AsNotFound<T>(string notFound, Func<Task<T>> readContainer)
    {
        try
        {
            return await readContainer();
        }
        catch (HttpException refusal) when (IsAbsence(refusal))
        {
            throw new HttpException(HttpStatusCode.NotFound, notFound);
        }
    }

    /// <inheritdoc cref="AsNotFound{T}" />
    public static Task AsNotFound(string notFound, Func<Task> readContainer) =>
        AsNotFound(notFound, async () =>
        {
            await readContainer();
            return true;
        });

    /// <summary>
    /// Whether the refusal says the module is not there, rather than that the
    /// reader may not do something with a module he sees.
    /// </summary>
    /// <param name="refusal">Refusal of the module read</param>
    public static bool IsAbsence(HttpException refusal) =>
        refusal.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone;
}
