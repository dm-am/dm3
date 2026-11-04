using System.Net;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// The single place where a ban is turned into a decision. Both ban scopes are
/// about speech, and they differ in reach:
/// <list type="bullet">
/// <item><see cref="AccessPolicy.DemocraticBan" /> — the ordinary ban. Silences
/// public speech: the global chat, forum topics and their discussion, and the
/// discussion of other people's games and blogs. Everything the user owns or
/// belongs to stays open, and posts in game rooms, blog publications and direct
/// messages are not affected at all.</item>
/// <item><see cref="AccessPolicy.FullBan" /> — the rare one. Nothing may be
/// sent. It also fails authentication outright, so it should never reach these
/// checks; it is honored here anyway rather than relying on a single gate.</item>
/// </list>
/// </summary>
public static class AccessRestrictions
{
    /// <summary>
    /// Whether the user may add content to a discussion surface.
    /// </summary>
    /// <param name="user">Authorization subject</param>
    /// <param name="inOwnSpace">
    /// True when the surface belongs to the user — a game they lead or were
    /// accepted into, a blog they author or assist. This is what the ordinary
    /// ban leaves open; it grants nothing under a full ban.
    /// </param>
    public static bool MaySpeak(this IAuthorizationSubject user, bool inOwnSpace = false)
    {
        var policy = user.AccessPolicy;

        if (policy.HasFlag(AccessPolicy.FullBan))
        {
            return false;
        }

        return !policy.HasFlag(AccessPolicy.DemocraticBan) || inOwnSpace;
    }

    /// <summary>
    /// Refuses a comment the ban silences, on any of the four commented
    /// surfaces.
    /// </summary>
    /// <remarks>
    /// Not in the intention resolver: the comment target carries neither the game
    /// nor the blog the own-space exemption is about, the calling service loads them.
    /// </remarks>
    /// <param name="user">Authorization subject</param>
    /// <param name="inOwnSpace">Same question as in <see cref="MaySpeak" /></param>
    public static void ThrowIfMayNotComment(this IAuthorizationSubject user, bool inOwnSpace = false)
    {
        if (!user.MaySpeak(inOwnSpace))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalFor(user.AccessPolicy));
        }
    }

    /// <summary>
    /// What the ban in force actually forbids, said in the reader's own words.
    /// </summary>
    /// <remarks>
    /// The two scopes forbid different things and cannot share a sentence.
    /// Naming the scope discloses nothing: the reader is told about his own ban,
    /// already notified and shown on his profile.
    /// </remarks>
    private static string RefusalFor(AccessPolicy policy) =>
        policy.HasFlag(AccessPolicy.FullBan)
            ? "Во время полного бана писать нельзя нигде"
            : "Во время бана нельзя комментировать форум и чужие игры и блоги";
}
