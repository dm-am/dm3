namespace DM.Domain.Core.Authorization;

/// <summary>
/// The empty user id and what it actually means: nobody is signed in.
/// </summary>
/// <remarks>
/// The guest carries the empty id and so does a field nobody filled in, so the
/// empty id must never be compared as an identity: ids to compare against a reader
/// pass through <see cref="WithoutAnonymous"/>, the reader's own through
/// <see cref="Of(IAuthorizationSubject?)"/>.
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
    public static IReadOnlyCollection<Guid> WithoutAnonymous(IEnumerable<Guid>? userIds) =>
        userIds?.Where(userId => !Is(userId)).Distinct().ToArray() ?? [];
}
