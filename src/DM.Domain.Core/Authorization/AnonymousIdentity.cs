using System;
using System.Collections.Generic;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// The empty user id and what it actually means: nobody is signed in.
/// </summary>
/// <remarks>
/// Two habits of this codebase meet on <see cref="Guid.Empty"/>. Every
/// repository filter takes the reader's id and is handed the empty one when
/// there is no reader, and the guest subject carries the empty id as its own
/// <see cref="IAuthorizationSubject.UserId"/>. A field that nobody filled in
/// holds the empty id too, because that is what a Guid starts as.
///
/// So the empty id is not one identity among many - it is the absence of one,
/// and every equality test that treats it as an identity answers "yes" for a
/// visitor who is nobody against a field that says nothing. That is how a
/// projection which forgot to fill the game master handed the master's sight
/// of every [private] block to anonymous readers.
///
/// Nothing here is a policy decision; it is the one place that says the empty
/// id may not be compared. Whoever holds ids that will be compared against a
/// reader passes them through <see cref="WithoutAnonymous"/> on the way in,
/// and whoever needs a reader's id for a comparison asks
/// <see cref="Of(IAuthorizationSubject?)"/> for it and gets null when there is
/// no reader.
/// </remarks>
public static class AnonymousIdentity
{
    /// <summary>The user id that stands for "no reader".</summary>
    public static readonly Guid UserId = Guid.Empty;

    /// <summary>Whether the given id is the absence of an identity.</summary>
    public static bool Is(Guid userId) => userId == UserId;

    /// <summary>
    /// The id the subject may be compared by, or null when the subject has no
    /// identity to compare with - it is not signed in, or it carries the empty
    /// id, which is the same thing said twice.
    /// </summary>
    public static Guid? Of(IAuthorizationSubject? subject) =>
        subject is { IsAuthenticated: true, UserId: var userId } && !Is(userId)
            ? userId
            : null;

    /// <summary>
    /// The given ids without the empty one and without repetitions - the form
    /// a set of ids has to be in before a reader is matched against it.
    /// </summary>
    public static IReadOnlyCollection<Guid> WithoutAnonymous(IEnumerable<Guid>? userIds)
    {
        if (userIds is null) return Array.Empty<Guid>();

        var kept = new List<Guid>();
        foreach (var userId in userIds)
        {
            if (Is(userId) || kept.Contains(userId)) continue;
            kept.Add(userId);
        }

        return kept.Count == 0 ? Array.Empty<Guid>() : kept;
    }
}
