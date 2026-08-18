using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;

namespace DM.Domain.Personal.Features.Profiles;

/// <inheritdoc />
internal class UserAvatarUploadAuthorizer : IUploadTargetAuthorizer
{
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public UserAvatarUploadAuthorizer(IIdentityProvider identityProvider) =>
        _identityProvider = identityProvider;

    /// <inheritdoc />
    public UploadType Type => UploadType.UserAvatar;

    /// <summary>
    /// An avatar belongs to whoever uploads it.
    /// </summary>
    /// <remarks>
    /// The endpoint takes the target from the query string, so a request could name
    /// another user. Nothing in the product ever sends one — the caller's own id is
    /// what the service falls back to — so a mismatch is refused rather than
    /// silently rewritten: a client that asks to change someone else's avatar
    /// should hear no, not get its own changed.
    ///
    /// Moderators are not exempt. Replacing another user's avatar is not a
    /// moderation action anywhere in the product; removing one is, and that goes
    /// through the delete endpoint, which does carry the Moderator branch.
    /// </remarks>
    public Task EnsureAllowedAsync(Guid targetId)
    {
        if (targetId != _identityProvider.Current.User.UserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// An avatar is shown wherever its owner is named, so it is served from a
    /// prefix the bucket answers to anybody. Nothing is withheld here that the
    /// public address does not already give away.
    /// </remarks>
    public Task EnsureReadAllowedAsync(Guid targetId) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    /// Nobody but the owner and Moderator+, which is what the upload's own
    /// intention already says. A user is not an entity with editors.
    /// </remarks>
    public Task<bool> MayDetachAsync(Guid targetId) => Task.FromResult(false);
}
