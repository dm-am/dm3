using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;

namespace DM.Domain.Blog.Tests;

/// <summary>
/// The signed-in reader the blog services ask the identity provider for.
/// </summary>
/// <remarks>
/// A session and a token, because the services under these rules read the user
/// off a successful authentication rather than off a bare identity; the name and
/// the token are constants, since no rule here reads either.
/// </remarks>
internal static class AuthenticatedIdentities
{
    /// <summary>A signed-in identity for the given user.</summary>
    internal static IIdentity Of(Guid userId)
    {
        var user = new AuthenticatedUser { UserId = userId, Username = "testuser" };
        var session = new Session();
        return Identity.Success(user, session, UserSettings.Default, "token");
    }
}
